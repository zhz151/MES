using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Blazor.Shared;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;
using MES.Core.Constants;
using System.Text.Json;

namespace MES.Blazor.Pages.Configuration;

public partial class DictValueDefinitions
{
    private MudTable<DictValueDefinitionDto>? table;
    private List<DictValueDefinitionDto> _pageItems = new();
    private int _totalCount;
    private string _searchKeyword = string.Empty;
    private bool _isArrowNavSetup;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _loadVersion;
    private bool _resetToFirstPage;
    private int _pageSize = 10;

    // 字典筛选
    private List<(string Key, string Text)> _dictKeyOptions = new();
    private string? _selectedDictKey;
    private bool _isRestoring;

    // 排序状态
    private string sortColumn = "DictKey";
    private bool sortDescending = false;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列选择管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "DictKey",     Label = "字典标识", SortKey = "dictkey",     FilterType = "string", IsRequired = true },
        new() { Key = "Value",       Label = "英文 Key", SortKey = "value",       FilterType = "string", IsRequired = true },
        new() { Key = "DisplayName", Label = "中文显示", SortKey = "displayname", FilterType = "string", IsRequired = true },
        new() { Key = "DisplayOrder",Label = "显示顺序", SortKey = "displayorder",FilterType = null, IsRequired = true },
        new() { Key = "IsEnabled",   Label = "启用",     SortKey = "isenabled",   FilterType = "boolean" },
        new() { Key = "Remark",      Label = "说明",     SortKey = "remark",      FilterType = "string" },
    };

    /// <summary>字典标识 → 中文说明（下拉/表格展示用）；工段/工序由专门配置表管理不在此列</summary>
    private static readonly Dictionary<string, string> DictKeyTexts = new(StringComparer.Ordinal)
    {
        [DictValueDefaults.UrgencyLevelKey] = "紧急度",
        [DictValueDefaults.ProductStatus] = "产类",
        [DictValueDefaults.ProductionFlowKey] = "流转",
        [DictValueDefaults.FlowTargetKey] = "关注目标",
        [DictValueDefaults.ProductionOverviewRowKey] = "汇总行",
        [DictValueDefaults.LiabilityTypeKey] = "责任类别",
        [DictValueDefaults.NcrResponsibilityKey] = "NCR 责任类别",
        [DictValueDefaults.RawMaterialLockRemarkKey] = "原锁备注",
        [DictValueDefaults.ProductionAttentionKey] = "生产关注",
    };

    private static string GetDictKeyText(string dictKey)
        => DictKeyTexts.TryGetValue(dictKey, out var cn) ? $"{cn}（{dictKey}）" : dictKey;

    // ========== 服务端数据加载 ==========

    private async Task<TableData<DictValueDefinitionDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "dictkey";

            // 首次加载覆盖页码
            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

            if (_resetToFirstPage)
            {
                state.Page = 0;
                _resetToFirstPage = false;
            }

            var query = new QueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending
            };

            var filters = new List<FilterDescriptor>();

            // 按字典筛选（equals 精确匹配 DictKey）
            if (!string.IsNullOrEmpty(_selectedDictKey))
            {
                filters.Add(new FilterDescriptor { Field = "DictKey", Operator = "equals", Value = _selectedDictKey });
            }

            // 列头 ExcelFilter 多选（in）
            var columnFiltersJson = SerializeFilters();
            if (columnFiltersJson != null)
            {
                var descriptors = JsonSerializer.Deserialize<List<FilterDescriptor>>(columnFiltersJson);
                if (descriptors is { Count: > 0 })
                    filters.AddRange(descriptors);
            }

            if (filters.Count > 0)
                query.Filters = filters;

            var result = await DictValueDefinitionService.GetPagedAsync(query);

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<DictValueDefinitionDto> { Items = _pageItems, TotalItems = _totalCount };

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPage = state.Page + 1;
            }
            else
            {
                _pageItems = new();
                _totalCount = 0;
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _pageItems = new();
            _totalCount = 0;
        }

        await SavePageStateAsync();
        return new TableData<DictValueDefinitionDto>
        {
            Items = _pageItems,
            TotalItems = _totalCount
        };
    }

    // ========== 排序和搜索 ==========

    private async Task ToggleSort(string sortKey)
    {
        if (sortColumn == sortKey)
            sortDescending = !sortDescending;
        else
        {
            sortColumn = sortKey;
            sortDescending = false;
        }
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        _resetToFirstPage = true;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnDictKeyChanged(string? value)
    {
        _selectedDictKey = string.IsNullOrEmpty(value) ? null : value;
        _restoredPageIndex = 0;
        _isFirstLoad = true;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== ExcelFilter 筛选 ==========

    private string? SerializeFilters()
    {
        if (_columnFilters.Count == 0) return null;
        var descriptors = new List<FilterDescriptor>();
        foreach (var kvp in _columnFilters)
        {
            if (kvp.Value.Count == 0) continue;
            descriptors.Add(new FilterDescriptor
            {
                Field = kvp.Key,
                Operator = "in",
                Values = kvp.Value.ToList()
            });
        }
        return descriptors.Count > 0 ? JsonSerializer.Serialize(descriptors) : null;
    }

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await DictValueDefinitionService.GetFilterContextsAsync();
            if (result.Success && result.Data != null)
            {
                BuildFilterContextOptions(result.Data);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载筛选上下文失败: {ex.Message}", Severity.Warning);
        }
    }

    private void BuildFilterContextOptions(Dictionary<string, List<string>> filterContexts)
    {
        _filterContextOptions.Clear();
        foreach (var kvp in filterContexts)
        {
            _filterContextOptions[kvp.Key] = kvp.Value.Select(v => new ExcelFilterOption
            {
                Value = v,
                Display = kvp.Key switch
                {
                    "DictKey" => GetDictKeyText(v),
                    "IsEnabled" => v == "True" ? "启用" : "隐藏",
                    _ => v
                },
                Count = 0
            }).ToList();
        }
    }

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("dict_value_definitions", null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
        if (table != null) await table.ReloadServerData();
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    // ========== 新增 ==========

    private async Task AddNew()
    {
        if (string.IsNullOrEmpty(_selectedDictKey))
        {
            Snackbar.Add("请先在上方选择要添加值的字典", Severity.Info);
            return;
        }

        var hash = DateTime.Now.Ticks.GetHashCode();
        var newId = hash < 0 ? hash : -hash - 1;
        var newItem = new DictValueDefinitionDto
        {
            Id = newId,
            DictKey = _selectedDictKey,
            Value = "",
            DisplayName = "",
            DisplayOrder = 999,
            IsEnabled = true
        };

        if (_currentPage == 1)
        {
            _pageItems.Insert(0, newItem);
            StartEdit(newItem);
            await InvokeAsync(StateHasChanged);
        }
        else
        {
            _currentPage = 1;
            _restoredPageIndex = 0;
            _isFirstLoad = true;
            if (table != null) await table.ReloadServerData();
            Snackbar.Add("请在首页点击\"新建\"添加记录", Severity.Info);
        }
    }

    // ========== 恢复默认 ==========

    private async Task RestoreDefaults()
    {
        if (string.IsNullOrEmpty(_selectedDictKey)) return;

        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定为字典「{GetDictKeyText(_selectedDictKey)}」恢复缺失的默认值吗？（已存在/已改名的行不会被覆盖）",
            ["ConfirmText"] = "恢复默认",
            ["Color"] = Color.Secondary
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        _isRestoring = true;
        try
        {
            var result = await DictValueDefinitionService.RestoreDefaultsAsync(_selectedDictKey);
            if (result.Success)
            {
                Snackbar.Add(result.Data > 0 ? $"已恢复 {result.Data} 行默认值" : "无缺失默认值", Severity.Success);
                if (table != null) await table.ReloadServerData();
            }
            else
            {
                Snackbar.Add(result.Message ?? "恢复默认失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"恢复默认失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isRestoring = false;
            StateHasChanged();
        }
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("dict_value_definitions", null);
        if (saved.Count > 0)
        {
            foreach (var s in saved)
            {
                var match = _allColumns.FirstOrDefault(c => c.Key == s.Key);
                if (match != null)
                    match.Visible = s.Visible;
            }
            var reordered = new List<ColumnDef>();
            foreach (var s in saved)
            {
                var match = _allColumns.FirstOrDefault(c => c.Key == s.Key);
                if (match != null && !reordered.Contains(match))
                    reordered.Add(match);
            }
            foreach (var c in _allColumns)
            {
                if (!reordered.Contains(c))
                    reordered.Add(c);
            }
            _allColumns = reordered;
        }

        // 字典标识下拉（9 个可配置字典，不含工段/工序专门配置表）
        _dictKeyOptions = DictValueDefaults.DictKeys
            .Select(k => (k, GetDictKeyText(k)))
            .ToList();

        // 恢复排序/搜索状态
        var savedState = await PageState.LoadAsync("dict_value_definitions");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "DictKey";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);
            if (savedState.Extras?.ContainsKey("columnFilters") == true)
            {
                try
                {
                    var raw = savedState.Extras["columnFilters"];
                    var dict = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(raw);
                    if (dict != null)
                        _columnFilters = dict.ToDictionary(kv => kv.Key, kv => new HashSet<string>(kv.Value));
                }
                catch { }
            }
        }

        // 加载筛选上下文（ExcelFilter 下拉选项）
        await LoadFilterContextsAsync();

        if (savedState != null && table != null)
            await table.ReloadServerData();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#dict-value-definitions-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string Value { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string? Remark { get; set; }
    }

    private bool IsNewItem(int id) => id < 0;

    // 英文 Key 格式：字母开头，仅含字母/数字/下划线（存储契约）
    private static bool IsValidValueKey(string key)
        => System.Text.RegularExpressions.Regex.IsMatch(key, "^[A-Za-z][A-Za-z0-9_]*$");

    private void StartEdit(DictValueDefinitionDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new EditCache
        {
            Value = item.Value,
            DisplayName = item.DisplayName,
            DisplayOrder = item.DisplayOrder,
            IsEnabled = item.IsEnabled,
            Remark = item.Remark
        };
    }

    private void CancelEdit(DictValueDefinitionDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);

        if (IsNewItem(item.Id))
        {
            _pageItems.Remove(item);
            StateHasChanged();
        }
    }

    private async Task SaveEdit(DictValueDefinitionDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cache.DisplayName)) errors.Add("中文显示不能为空");
        var valueKey = cache.Value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(valueKey)) errors.Add("英文 Key 不能为空");
        else if (!IsValidValueKey(valueKey)) errors.Add($"英文 Key「{valueKey}」格式不正确：须字母开头，仅含字母/数字/下划线");
        if (cache.DisplayOrder <= 0) errors.Add("显示顺序必须大于0");
        if (errors.Any()) { Snackbar.Add(string.Join("；", errors), Severity.Warning); return; }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var dto = new DictValueDefinitionDto
            {
                Id = IsNewItem(item.Id) ? 0 : item.Id,
                DictKey = item.DictKey,
                Value = valueKey,
                DisplayName = cache.DisplayName,
                DisplayOrder = cache.DisplayOrder,
                IsEnabled = cache.IsEnabled,
                Remark = cache.Remark
            };

            var result = await DictValueDefinitionService.SaveAsync(dto);
            if (result.Success)
            {
                _editingIds.Remove(item.Id);
                _editCache.Remove(item.Id);
                Snackbar.Add("保存成功", Severity.Success);
                if (table != null) await table.ReloadServerData();
            }
            else
            {
                Snackbar.Add(result.Message ?? "保存失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"保存失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSaving = false;
            StateHasChanged();
        }
    }

    // ========== 删除 ==========

    private async Task DeleteItem(DictValueDefinitionDto item)
    {
        if (IsNewItem(item.Id))
        {
            _pageItems.Remove(item);
            StateHasChanged();
            return;
        }

        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除字典「{GetDictKeyText(item.DictKey)}」的值「{item.Value}」配置吗？（删除后该值显示回退到内置默认中文）",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        try
        {
            var result = await DictValueDefinitionService.DeleteAsync(item.Id);
            if (result.Success)
            {
                Snackbar.Add("删除成功", Severity.Success);
                if (table != null) await table.ReloadServerData();
            }
            else
            {
                Snackbar.Add(result.Message ?? "删除失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"删除失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage,
            Extras = extras
        };
        await PageState.SaveAsync("dict_value_definitions", state);
    }
}
