using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Constants;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Core.DTOs.Configuration;

namespace MES.Blazor.Pages.Configuration;

public partial class ProcessDefinitions
{
    private MudTable<ProcessDefinitionDto>? table;
    private List<ProcessDefinitionDto> _pageItems = new();
    private int _totalCount;
    private string _searchKeyword = string.Empty;
    private bool _isArrowNavSetup;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;

    // 排序状态
    private string sortColumn = "ProcessName";
    private bool sortDescending = false;

    // ========== 列选择管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    // 默认工段可选工段（配置表启用工段，失败降级预置 26 工段）
    private List<SectionInfoDto> _sectionOptions = new();

    private async Task LoadSectionOptionsAsync()
    {
        var r = await StandardWorkDayService.GetEnabledSectionsAsync();
        if (r.Success && r.Data is { Count: > 0 })
        {
            _sectionOptions = r.Data;
        }
        else
        {
            _sectionOptions = SectionDefs.PropertyNames
                .Select((k, i) => new SectionInfoDto
                {
                    SectionKey = k,
                    SectionName = SectionDefs.PropertyToName[k],
                    DisplayOrder = i + 1,
                    IsEnabled = true
                })
                .ToList();
        }
    }

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "ProcessName",  Label = "工序名称", SortKey = "processname",  FilterType = null, IsRequired = true },
        new() { Key = "ProcessKey",   Label = "稳定 Key", SortKey = "processkey",   FilterType = null, IsRequired = true },
        new() { Key = "DisplayOrder", Label = "显示顺序", SortKey = "displayorder", FilterType = null, IsRequired = true },
        new() { Key = "IsEnabled",    Label = "启用",     SortKey = "isenabled",    FilterType = null },
        new() { Key = "IsColdRoll",   Label = "冷轧类",   SortKey = "iscoldroll",   FilterType = null },
        new() { Key = "IsColdDraw",   Label = "冷拔类",   SortKey = "iscolddraw",   FilterType = null },
        new() { Key = "DefaultSections", Label = "默认工段", SortKey = "defaultsections", FilterType = null },
        new() { Key = "Remark",       Label = "备注",     SortKey = "remark",       FilterType = null },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<ProcessDefinitionDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "processname";

            // 首次加载覆盖页码
            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

            var query = new QueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending
            };

            var result = await ProcessDefinitionService.GetPagedAsync(query);

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
        return new TableData<ProcessDefinitionDto>
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
        await ColumnPrefs.SaveAsync("process_definitions", null, _allColumns);
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

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        await LoadSectionOptionsAsync();
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("process_definitions", null);
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

        // 恢复排序/搜索状态
        var savedState = await PageState.LoadAsync("process_definitions");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "ProcessName";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);
        }

        if (savedState != null && table != null)
            await table.ReloadServerData();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#process-definitions-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 新增 ==========

    private async Task AddNew()
    {
        // 使用一个临时负 ID 作为新增行标识
        var hash = DateTime.Now.Ticks.GetHashCode();
        var newId = hash < 0 ? hash : -hash - 1;
        var newItem = new ProcessDefinitionDto
        {
            Id = newId,
            ProcessName = "",
            DisplayOrder = 999,
            IsEnabled = true
        };

        // 如果当前在第一页，直接加到 pageItems
        if (_currentPage == 1)
        {
            _pageItems.Insert(0, newItem);
            StartEdit(newItem);
            await InvokeAsync(StateHasChanged);
        }
        else
        {
            // 不在第一页则跳转到第一页
            _currentPage = 1;
            _restoredPageIndex = 0;
            _isFirstLoad = true;
            if (table != null) await table.ReloadServerData();
            Snackbar.Add("请在首页点击\"新建\"添加记录", Severity.Info);
        }
    }

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string ProcessName { get; set; } = string.Empty;
        public string ProcessKey { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsColdRoll { get; set; }
        public bool IsColdDraw { get; set; }

        /// <summary>默认工段（SectionKey 列表，多选勾选绑定）</summary>
        public List<string> DefaultSections { get; set; } = new();

        public string? Remark { get; set; }
    }

    // 编辑中的新增行 ID
    private bool IsNewItem(int id) => id < 0;

    // 稳定 Key 格式：字母开头，仅含字母/数字/下划线（程序识别契约，禁中文/空格/特殊字符）
    private static bool IsValidProcessKey(string key)
        => System.Text.RegularExpressions.Regex.IsMatch(key, "^[A-Za-z][A-Za-z0-9_]*$");

    private void StartEdit(ProcessDefinitionDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new EditCache
        {
            ProcessName = item.ProcessName,
            ProcessKey = item.ProcessKey,
            DisplayOrder = item.DisplayOrder,
            IsEnabled = item.IsEnabled,
            IsColdRoll = item.IsColdRoll,
            IsColdDraw = item.IsColdDraw,
            DefaultSections = item.DefaultSections?.ToList() ?? new List<string>(),
            Remark = item.Remark
        };
    }

    private void CancelEdit(ProcessDefinitionDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);

        // 如果是新增行且取消编辑，移除该行
        if (IsNewItem(item.Id))
        {
            _pageItems.Remove(item);
            StateHasChanged();
        }
    }

    private async Task SaveEdit(ProcessDefinitionDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cache.ProcessName)) errors.Add("工序名称不能为空");
        if (cache.DisplayOrder <= 0) errors.Add("显示顺序必须大于0");

        // 稳定 Key：新增行可手填；留空则按工序名称自动映射（预置 9 种工序）
        var processKey = cache.ProcessKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(processKey))
        {
            processKey = ProcessKeys.ToKey(cache.ProcessName) ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(processKey))
        {
            errors.Add("稳定 Key 不能为空：预置 9 种工序按名称自动映射，全新工序请手动填写");
        }
        else if (!IsValidProcessKey(processKey))
        {
            errors.Add($"稳定 Key「{processKey}」格式不正确：须字母开头，仅含字母/数字/下划线");
        }

        if (errors.Any()) { Snackbar.Add(string.Join("；", errors), Severity.Warning); return; }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var dto = new ProcessDefinitionDto
            {
                Id = IsNewItem(item.Id) ? 0 : item.Id,
                ProcessName = cache.ProcessName,
                ProcessKey = processKey,
                DisplayOrder = cache.DisplayOrder,
                IsEnabled = cache.IsEnabled,
                IsColdRoll = cache.IsColdRoll,
                IsColdDraw = cache.IsColdDraw,
                DefaultSections = cache.DefaultSections.Count > 0 ? cache.DefaultSections : null,
                Remark = cache.Remark
            };

            var result = await ProcessDefinitionService.SaveAsync(dto);
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

    private async Task DeleteItem(ProcessDefinitionDto item)
    {
        // 新增行直接移除
        if (IsNewItem(item.Id))
        {
            _pageItems.Remove(item);
            StateHasChanged();
            return;
        }

        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除工序组\"{item.ProcessName}\" 的定义吗？",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await ProcessDefinitionService.DeleteAsync(item.Id);
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
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage
        };
        await PageState.SaveAsync("process_definitions", state);
    }
}
