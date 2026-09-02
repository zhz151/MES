using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Blazor.Services.Payroll;
using MES.Blazor.Shared;
using MES.Core.DTOs.Payroll;
using MES.Core.Models;

namespace MES.Blazor.Pages.Payroll;

/// <summary>
/// 成检计件类别（2026-09-03 引入）列表页：类别 = 成检项目(InspectionItem 单选) + 基准价 + 维档系数。
/// 列显隐 + 全字段排序 + 模糊搜索（成检项目/备注，服务端内存过滤）+ 成检项目/启停筛选，
/// 页内附带「试算匹配」展开区（POST match-price 按一条成检验证类别单价）。
/// </summary>
public partial class FinalInspectionCategories
{
    private MudTable<PieceRateFinalInspectionCategoryListItemDto>? table;
    private List<PieceRateFinalInspectionCategoryListItemDto> _pageItems = new();
    private int _totalCount;
    private string _searchKeyword = string.Empty;
    private bool _isArrowNavSetup;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 20;
    private int _loadVersion;
    private bool _resetToFirstPage;

    /// <summary>成检项目筛选（空=全部；InspectionItem 枚举名）</summary>
    private string? _itemFilter;
    private List<PieceRateCategoryOptionItemDto> _itemOptions = new();

    /// <summary>启停筛选（空=全部；"true"/"false"）</summary>
    private string? _activeFilter;

    // ========== 试算匹配区状态 ==========
    private List<PieceRateCategoryOptionItemDto> _lengthStatusOptions = new();
    private List<PieceRateCategoryOptionItemDto> _stateOptions = new();
    private string? _trialItemKey;
    private string? _trialLengthStatus;
    private string? _trialSpecialState;
    private string? _trialPlantGrade;
    private decimal? _trialOuterDiameter;
    private decimal? _trialWallThickness;
    private decimal? _trialLength;
    private int? _trialInspectionCount;
    private string? _trialEquipmentName;
    private bool _matchLoading;
    private bool _matchDone;
    private PieceRateFinalInspectionMatchResultDto? _matchResult;

    // 排序状态（默认按成检项目列升序）
    private string sortColumn = "ItemKeyChinese";
    private bool sortDescending;

    /// <summary>列显隐/顺序偏好版本（改列结构时 +1 强制新默认生效）</summary>
    private const string ColumnPrefsVersion = "v1";

    [Inject] private PieceRateFinalInspectionCategoryService Service { get; set; } = null!;
    [Inject] private PieceRateFinalInspectionCategoryImportService ImportService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private ColumnPrefsService ColumnPrefs { get; set; } = null!;
    [Inject] private PageStateService PageState { get; set; } = null!;

    private const string PrefsKey = "payroll_final_inspection_category";

    // ========== 列选择管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns => _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "ItemKeyChinese", Label = "成检项目",  SortKey = "itemkeychinese", IsRequired = true },
        new() { Key = "BasePrice",      Label = "基准价",    SortKey = "baseprice" },
        new() { Key = "Unit",           Label = "单位",      SortKey = "unit" },
        new() { Key = "TierCount",      Label = "维档数",    SortKey = "tiercount" },
        new() { Key = "IsActive",       Label = "是否启用",  SortKey = "isactive" },
        new() { Key = "Remark",         Label = "备注",      SortKey = "remark" },
        new() { Key = "UpdatedTime",    Label = "更新时间",  SortKey = "updatedtime" },
        new() { Key = "CreatedTime",    Label = "创建时间",  SortKey = "createdtime" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<PieceRateFinalInspectionCategoryListItemDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "createdtime";

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

            var query = new PieceRateFinalInspectionCategoryQueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending,
                ItemKey = string.IsNullOrWhiteSpace(_itemFilter) ? null : _itemFilter,
                Unit = null,
                IsActive = string.IsNullOrEmpty(_activeFilter) ? null : bool.Parse(_activeFilter!)
            };

            var result = await Service.GetPagedAsync(query);

            if (version != _loadVersion)
                return new TableData<PieceRateFinalInspectionCategoryListItemDto> { Items = _pageItems, TotalItems = _totalCount };

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
        return new TableData<PieceRateFinalInspectionCategoryListItemDto>
        {
            Items = _pageItems,
            TotalItems = _totalCount
        };
    }

    // ========== 排序、搜索、筛选 ==========

    private async Task ToggleSort(string colKey)
    {
        var sortKey = _allColumns.FirstOrDefault(c => c.Key == colKey)?.SortKey;
        if (string.IsNullOrEmpty(sortKey)) return;
        if (sortColumn == colKey)
            sortDescending = !sortDescending;
        else
        {
            sortColumn = colKey;
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

    private async Task OnItemFilterChanged(string? value)
    {
        _itemFilter = value;
        _resetToFirstPage = true;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnActiveFilterChanged(string? value)
    {
        _activeFilter = value;
        _resetToFirstPage = true;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnUp(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnDown(ColumnDef col) => await SaveColumnPrefs();

    private async Task SaveColumnPrefs()
        => await ColumnPrefs.SaveAsync(PrefsKey, ColumnPrefsVersion, _allColumns);

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();

        var saved = await ColumnPrefs.LoadAsync(PrefsKey, ColumnPrefsVersion);
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

        var savedState = await PageState.LoadAsync(PrefsKey);
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "ItemKeyChinese";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);
        }

        // 选项源（成检项目/长度状态/特殊制造状态）来自 options 端点
        var optionsResult = await Service.GetOptionsAsync();
        if (optionsResult.Success && optionsResult.Data != null)
        {
            _itemOptions = optionsResult.Data.Items;
            _lengthStatusOptions = optionsResult.Data.LengthStatuses;
            _stateOptions = optionsResult.Data.States;
            // 将当前已保存的成检项目筛选加入候选（防止历史值无候选）
            if (!string.IsNullOrEmpty(_itemFilter) &&
                _itemOptions.All(o => !string.Equals(o.Key, _itemFilter, StringComparison.Ordinal)))
            {
                _itemOptions.Add(new PieceRateCategoryOptionItemDto
                {
                    Key = _itemFilter!,
                    Name = _itemFilter!
                });
            }
        }

        if (savedState != null && table != null)
            await table.ReloadServerData();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#final-inspection-category-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 导出 / 批量导入 / 新增编辑删除 ==========

    private async Task ExportAll()
    {
        try
        {
            var bytes = await ImportService.ExportAllAsync();
            await DownloadBytesAsync(bytes, $"成检类别全量_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            Snackbar.Add("已导出全量成检类别标准", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"导出失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task OpenImportDialog(string kind)
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true };
        var parameters = new DialogParameters { ["Kind"] = kind };
        var title = kind == "tier" ? "批量导入 - 成检维档系数" : "批量导入 - 成检类别定义";
        var dialog = await DialogService.ShowAsync<PieceRateFinalInspectionCategoryImportDialog>(title, parameters, options);
        var result = await dialog.Result;
        if (!result.Canceled && (bool?)result.Data == true && table != null)
            await table.ReloadServerData();
    }

    private async Task DownloadBytesAsync(byte[] data, string fileName)
    {
        var base64 = Convert.ToBase64String(data);
        await JS.InvokeVoidAsync("downloadFile", base64, fileName);
    }

    private void AddNew() => Navigation.NavigateTo("/payroll/final-inspection-categories/create");

    private void EditItem(PieceRateFinalInspectionCategoryListItemDto item)
        => Navigation.NavigateTo($"/payroll/final-inspection-categories/edit/{item.Id}");

    private async Task DeleteItem(PieceRateFinalInspectionCategoryListItemDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除类别 [{item.ItemKeyChinese}] 吗？维档将一并删除。",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        try
        {
            var result = await Service.DeleteAsync(item.Id);
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

    // ========== 试算匹配 ==========

    private async Task StartMatchAsync()
    {
        if (string.IsNullOrWhiteSpace(_trialItemKey))
        {
            Snackbar.Add("请先选择成检项目", Severity.Warning);
            return;
        }
        _matchLoading = true;
        _matchDone = false;
        _matchResult = null;
        try
        {
            var request = new PieceRateFinalInspectionMatchRequest
            {
                ItemKey = _trialItemKey!,
                LengthStatus = string.IsNullOrWhiteSpace(_trialLengthStatus) ? null : _trialLengthStatus,
                SpecialState = string.IsNullOrWhiteSpace(_trialSpecialState) ? null : _trialSpecialState,
                PlantGrade = string.IsNullOrWhiteSpace(_trialPlantGrade) ? null : _trialPlantGrade.Trim(),
                OuterDiameter = _trialOuterDiameter,
                WallThickness = _trialWallThickness,
                // 长度状态 Range/NonFixed 未填长度 → 默认 6000mm（业务规约 2026-09-03：范围尺/非定尺折算参与 Length 档）
                Length = _trialLength ?? DefaultLengthByStatus(_trialLengthStatus),
                InspectionCount = _trialInspectionCount,
                EquipmentName = string.IsNullOrWhiteSpace(_trialEquipmentName) ? null : _trialEquipmentName.Trim()
            };
            var result = await Service.MatchPriceAsync(request);
            _matchDone = true;
            if (result.Success)
            {
                // Data == null 表示该项目未定价
                _matchResult = result.Data;
            }
            else
            {
                Snackbar.Add(result.Message ?? "试算失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"试算失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _matchLoading = false;
        }
    }

    /// <summary>试算长度缺省值：长度状态为范围尺/非定尺（枚举名英文 Key）且未填长度 → 6000mm；否则 null（不兜底）。</summary>
    private static decimal? DefaultLengthByStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        var s = status.Trim();
        var isNonFixed = string.Equals(s, "Range", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "NonFixed", StringComparison.OrdinalIgnoreCase);
        return isNonFixed ? 6000m : null;
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
        await PageState.SaveAsync(PrefsKey, state);
    }
}
