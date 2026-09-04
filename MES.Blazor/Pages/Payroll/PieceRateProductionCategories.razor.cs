using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Blazor.Services.Payroll;
using MES.Blazor.Shared;
using MES.Core.Constants;
using MES.Core.DTOs.Payroll;
using MES.Core.Models;

namespace MES.Blazor.Pages.Payroll;

/// <summary>
/// 生产计件类别（2026-09-02 两表模型）列表页：类别 = 工段 × 工序/产类/阶段约束 + 基准价 + 维档系数。
/// 列显隐 + 全字段排序 + 模糊搜索（自动组合名/工段/备注，由服务端内存过滤）+ 工段/启停筛选。
/// </summary>
public partial class PieceRateProductionCategories
{
    private MudTable<PieceRateProductionCategoryListItemDto>? table;
    private List<PieceRateProductionCategoryListItemDto> _pageItems = new();
    private int _totalCount;
    private string _searchKeyword = string.Empty;
    private bool _isArrowNavSetup;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 20;
    private int _loadVersion;
    private bool _resetToFirstPage;

    /// <summary>工段筛选（空=全部）</summary>
    private string? _sectionFilter;
    private List<PieceRateCategoryOptionItemDto> _sectionOptions = new();

    /// <summary>启停筛选（空=全部；"true"/"false"）</summary>
    private string? _activeFilter;

    // 排序状态
    private string sortColumn = "SectionKey";
    private bool sortDescending;

    /// <summary>列显隐/顺序偏好版本（改列结构时 +1 强制新默认生效）</summary>
    private const string ColumnPrefsVersion = "v1";

    [Inject] private PieceRateProductionCategoryService Service { get; set; } = null!;
    [Inject] private PieceRateCategoryImportService ImportService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private ColumnPrefsService ColumnPrefs { get; set; } = null!;
    [Inject] private PageStateService PageState { get; set; } = null!;

    private const string PrefsKey = "payroll_piece_rate_category";

    // ========== 列选择管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns => _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "DisplayName",   Label = "自动组合名", SortKey = "displayname",  IsRequired = true },
        new() { Key = "SectionKey",    Label = "工段",      SortKey = "sectionkey",                       },
        new() { Key = "BasePrice",     Label = "基准价",    SortKey = "baseprice" },
        new() { Key = "Unit",          Label = "单位",      SortKey = "unit" },
        new() { Key = "TierCount",     Label = "维档数",    SortKey = "tiercount" },
        new() { Key = "IsActive",      Label = "是否启用",  SortKey = "isactive" },
        new() { Key = "Remark",        Label = "备注",      SortKey = "remark" },
        new() { Key = "UpdatedTime",   Label = "更新时间",  SortKey = "updatedtime" },
        new() { Key = "CreatedTime",   Label = "创建时间",  SortKey = "createdtime" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<PieceRateProductionCategoryListItemDto>> LoadDataFromServer(TableState state)
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

            var query = new PieceRateProductionCategoryQueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending,
                SectionKey = string.IsNullOrWhiteSpace(_sectionFilter) ? null : _sectionFilter,
                IsActive = string.IsNullOrEmpty(_activeFilter) ? null : bool.Parse(_activeFilter!)
            };

            var result = await Service.GetPagedAsync(query);

            if (version != _loadVersion)
                return new TableData<PieceRateProductionCategoryListItemDto> { Items = _pageItems, TotalItems = _totalCount };

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
        return new TableData<PieceRateProductionCategoryListItemDto>
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

    private async Task OnSectionFilterChanged(string? value)
    {
        _sectionFilter = value;
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
            sortColumn = savedState.SortBy ?? "SectionKey";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);
        }

        // 工段筛选下拉选项（启用工段，来自 options 端点）
        var optionsResult = await Service.GetOptionsAsync();
        if (optionsResult.Success && optionsResult.Data != null)
        {
            _sectionOptions = optionsResult.Data.Sections;
            // 将当前已保存的工段筛选加入候选（防止历史值无候选）
            if (!string.IsNullOrEmpty(_sectionFilter) &&
                _sectionOptions.All(o => !string.Equals(o.Key, _sectionFilter, StringComparison.Ordinal)))
            {
                _sectionOptions.Add(new PieceRateCategoryOptionItemDto
                {
                    Key = _sectionFilter!,
                    Name = _sectionFilter!
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
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#piece-rate-category-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 导出 / 批量导入 / 新增编辑删除 ==========

    private async Task ExportAll()
    {
        try
        {
            var bytes = await ImportService.ExportAllAsync();
            await DownloadBytesAsync(bytes, $"计件类别全量_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            Snackbar.Add("已导出全量类别标准（类别 + 维档双表）", Severity.Success);
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
        var title = kind == "tier" ? "批量导入 - 维档系数" : "批量导入 - 类别定义";
        var dialog = await DialogService.ShowAsync<PieceRateCategoryImportDialog>(title, parameters, options);
        var result = await dialog.Result;
        if (!result.Canceled && (bool?)result.Data == true && table != null)
            await table.ReloadServerData();
    }

    private async Task DownloadBytesAsync(byte[] data, string fileName)
    {
        var base64 = Convert.ToBase64String(data);
        await JS.InvokeVoidAsync("downloadFile", base64, fileName);
    }

    private void AddNew() => Navigation.NavigateTo("/payroll/piece-rate-categories/create");

    private void EditItem(PieceRateProductionCategoryListItemDto item)
        => Navigation.NavigateTo($"/payroll/piece-rate-categories/edit/{item.Id}");

    private async Task DeleteItem(PieceRateProductionCategoryListItemDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除类别 [{item.DisplayName}] 吗？维档将一并删除。",
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

    // ========== 模拟测算（按产量记录点选计价，2026-09-04） ==========

    private MudTable<PieceRateProductionTrialRecordDto>? _trialTable;
    private List<PieceRateProductionTrialRecordDto> _trialPageItems = new();
    private int _trialTotalCount;
    private string _trialSourceKey = nameof(PieceRateProductionTrialSource.ProductionRecord);
    private string _trialKeyword = string.Empty;
    private bool _trialDataLoaded;
    private PieceRateProductionTrialRecordDto? _selectedRecord;
    private bool _pricingLoading;
    private bool _pricingDone;
    private string? _pricingError;
    private PieceRateProductionMatchResultDto? _matchResult;

    /// <summary>产量源选项（4 类：生产记录/入缸/完工/过程检验）</summary>
    private readonly List<PieceRateCategoryOptionItemDto> _trialSources = new()
    {
        new() { Key = nameof(PieceRateProductionTrialSource.ProductionRecord), Name = "生产记录" },
        new() { Key = nameof(PieceRateProductionTrialSource.PicklingIn), Name = "去油酸洗入缸" },
        new() { Key = nameof(PieceRateProductionTrialSource.PicklingOut), Name = "去油酸洗完工" },
        new() { Key = nameof(PieceRateProductionTrialSource.ProcessInspection), Name = "过程检验" }
    };

    /// <summary>展开面板首次展开时加载候选记录</summary>
    private async Task OnTrialExpandedChanged(bool expanded)
    {
        if (expanded && !_trialDataLoaded && _trialTable != null)
        {
            _trialDataLoaded = true;
            await _trialTable.ReloadServerData();
        }
    }

    private async Task OnTrialSourceChanged(string value)
    {
        _trialSourceKey = value ?? nameof(PieceRateProductionTrialSource.ProductionRecord);
        _trialDataLoaded = true;
        if (_trialTable != null) await _trialTable.ReloadServerData();
    }

    private async Task OnTrialKeywordChanged(string value)
    {
        _trialKeyword = value ?? string.Empty;
        if (_trialTable != null) await _trialTable.ReloadServerData();
    }

    /// <summary>候选产量记录服务端分页（默认记录日期降序）</summary>
    private async Task<TableData<PieceRateProductionTrialRecordDto>> LoadTrialRecords(TableState state)
    {
        try
        {
            var query = new PieceRateProductionTrialRecordQuery
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_trialKeyword) ? null : _trialKeyword,
                Source = _trialSourceKey
            };
            var result = await Service.GetTrialRecordsAsync(query);
            if (result.Success && result.Data != null)
            {
                _trialPageItems = result.Data.Items;
                _trialTotalCount = result.Data.TotalCount;
            }
            else
            {
                _trialPageItems = new();
                _trialTotalCount = 0;
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载产量记录失败: {ex.Message}", Severity.Error);
            _trialPageItems = new();
            _trialTotalCount = 0;
        }
        return new TableData<PieceRateProductionTrialRecordDto>
        {
            Items = _trialPageItems,
            TotalItems = _trialTotalCount
        };
    }

    /// <summary>行「试算」：按该真实产量记录计价（与月结采集同 Mapper 映射单源）</summary>
    private async Task PriceRecordAsync(PieceRateProductionTrialRecordDto record)
    {
        _selectedRecord = record;
        _pricingLoading = true;
        _pricingDone = false;
        _pricingError = null;
        _matchResult = null;
        try
        {
            var result = await Service.MatchProductionRecordAsync(record.SourceKey, record.Id);
            _pricingDone = true;
            if (result.Success)
            {
                // Data == null 表示该记录未定价
                _matchResult = result.Data;
            }
            else
            {
                _pricingError = result.Message ?? "计价失败";
            }
        }
        catch (Exception ex)
        {
            _pricingError = $"计价失败: {ex.Message}";
        }
        finally
        {
            _pricingLoading = false;
        }
    }

    /// <summary>整行金额缺失提示：记录缺支数/重量折算基数（按结算单位细化）</summary>
    private static string TrialSimulatedAmountHint(string? unit)
        => unit switch
        {
            PieceRateUnitKeys.PerTon => "该记录缺重量(kg)，无法折算整行金额",
            PieceRateUnitKeys.PerPiece => "该记录缺支数，无法折算整行金额",
            PieceRateUnitKeys.PerHead => "该记录缺支数，无法折算整行金额（元/头 = 支数 × 平头数 × 单价）",
            PieceRateUnitKeys.PerKm => "该记录无长度维（生产记录行），元/千米单位无法折算整行金额",
            _ => "该记录缺数量，无法折算整行金额"
        };

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
