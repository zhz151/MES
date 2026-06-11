using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Constants;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Blazor.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages.Configuration;

public partial class ConfigParameters
{
    private MudTable<ConfigParameterDto>? table;
    private List<ConfigParameterDto> _pageItems = new();
    private int _totalCount;
    private string _searchKeyword = string.Empty;
    private bool _isArrowNavSetup;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;

    // ========== 分类名映射（英文→中文） ==========

    private static readonly Dictionary<string, string> CategoryDisplayMap = new()
    {
        // 用料计划域
        ["MaterialPlanRatio"] = "用料计划-比率",
        ["MaterialPlanStatus"] = "用料计划-状态阈值",
        ["DimensionTolerance"] = "用料计划-尺寸公差",
        ["LengthDefault"] = "用料计划-长度默认值",
        ["ReworkRatio"] = "用料计划-返工比率",
        // 批次域
        ["ProcessingDiscount"] = "批次-加工折扣",
        ["ProductionThreshold"] = "批次-生产阈值",
        // 产能排程域
        ["DateBucket"] = "产能排程-时间桶",
        ["ProductionCapacity"] = "产能排程-日产能",
        // 仓库域
        ["WarehouseThreshold"] = "仓库-完成阈值",
        // 交期排程域
        ["WorkOrderDays"] = "交期排程-工单天数",
        ["UrgencyThreshold"] = "交期排程-紧急度阈值",
        // 合同域
        ["ContractWeight"] = "合同-重量校验",
        // 质量域
        ["SequenceJump"] = "质量-工序跳号",
        // 通用域
        ["DefaultValue"] = "通用-默认值",
    };

    public static string GetCategoryDisplay(string category)
    {
        return CategoryDisplayMap.TryGetValue(category, out var display) ? display : category;
    }

    // 排序状态
    private string sortColumn = "Category";
    private bool sortDescending = false;

    // ========== 列选择管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "Category",   Label = "参数分类",   SortKey = "category",   FilterType = null, IsRequired = true },
        new() { Key = "ParamKey",   Label = "参数键",     SortKey = "paramkey",   FilterType = null, IsRequired = true },
        new() { Key = "ParamValue", Label = "参数值",     SortKey = "paramvalue", FilterType = null, IsRequired = true },
        new() { Key = "Remark",     Label = "用途说明",   SortKey = "remark",     FilterType = null },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<ConfigParameterDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "category";

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

            var result = await ConfigParameterService.GetPagedAsync(query);

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

        return new TableData<ConfigParameterDto>
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
        // 支持中文分类名搜索：将中文名映射回英文
        _searchKeyword = ExpandSearchKeyword(_searchKeyword);
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    /// <summary>
    /// 将用户输入的中文分类名扩展为英文，使搜索能命中数据库中的英文分类名
    /// </summary>
    private static string ExpandSearchKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return keyword;

        // 对每个中英文映射，如果关键字包含中文名，则追加上对应的英文分类名
        var extraTerms = new List<string>();
        foreach (var kvp in CategoryDisplayMap)
        {
            if (keyword.Contains(kvp.Value))
                extraTerms.Add(kvp.Key);
        }
        return extraTerms.Count > 0
            ? keyword + " " + string.Join(" ", extraTerms)
            : keyword;
    }

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("config_parameters", null, _allColumns);
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
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("config_parameters", null);
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
        var savedState = await PageState.LoadAsync("config_parameters");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "Category";
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
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#config-parameters-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 新增 ==========

    private async Task AddNew()
    {
        // 使用一个临时负 ID 作为新增行标识
        var hash = DateTime.Now.Ticks.GetHashCode();
        var newId = hash < 0 ? hash : -hash - 1;
        var newItem = new ConfigParameterDto
        {
            Id = newId,
            Category = "",
            ParamKey = "",
            ParamValue = 0
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
            // 重新加载后通过状态保持新增模式较复杂，提示用户手动操作
            Snackbar.Add("请在首页点击\"新建\"添加记录", Severity.Info);
        }
    }

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string Category { get; set; } = string.Empty;
        public string ParamKey { get; set; } = string.Empty;
        public decimal ParamValue { get; set; }
        public string? Remark { get; set; }
    }

    // 编辑中的新增行 ID
    private bool IsNewItem(int id) => id < 0;

    private void StartEdit(ConfigParameterDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new EditCache
        {
            Category = item.Category,
            ParamKey = item.ParamKey,
            ParamValue = item.ParamValue,
            Remark = item.Remark
        };
    }

    private void CancelEdit(ConfigParameterDto item)
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

    private async Task SaveEdit(ConfigParameterDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cache.Category)) errors.Add("参数分类不能为空");
        if (string.IsNullOrWhiteSpace(cache.ParamKey)) errors.Add("参数键不能为空");
        if (errors.Any()) { Snackbar.Add(string.Join("；", errors), Severity.Warning); return; }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var dto = new ConfigParameterDto
            {
                Id = IsNewItem(item.Id) ? 0 : item.Id,
                Category = cache.Category,
                ParamKey = cache.ParamKey,
                ParamValue = cache.ParamValue,
                Remark = cache.Remark
            };

            var result = await ConfigParameterService.SaveAsync(dto);
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

    private async Task DeleteItem(ConfigParameterDto item)
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
            ["ContentText"] = $"确定要删除参数\"{item.Category} / {item.ParamKey}\" 吗？",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await ConfigParameterService.DeleteAsync(item.Id);
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
        await PageState.SaveAsync("config_parameters", state);
    }
}
