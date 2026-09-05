using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Core.Models;
using MES.Blazor.Helpers;
using MES.Blazor.Shared;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using MES.Shared.Constants;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Shared;

namespace MES.Blazor.Pages.Quality;

[Authorize(Roles = Roles.Policies.QualityView)]
public partial class Ncrs
{
    [Inject] private NcrService NcrService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private PageStateService PageState { get; set; } = null!;
    [Inject] private ColumnPrefsService ColumnPrefs { get; set; } = null!;
    [Inject] private DictValueDefinitionService DictValueDefinitionService { get; set; } = null!;
    [Inject] private HttpClient Http { get; set; } = null!;

    private MudTable<NcrDto>? table;
    private List<NcrDto> _pageItems = new();
    private int _totalCount;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;
    private string sortColumn = "reportdate";
    private bool sortDescending = true;
    private bool _isFirstLoad = true;
    private int _restoredPageIndex;
    private int _loadVersion;
    private bool _resetToFirstPage;

    // ========== 选择/打印 ==========
    private HashSet<int> selectedIds = new();
    private bool allSelected => _pageItems.Count > 0 && _pageItems.All(i => selectedIds.Contains(i.Id));

    private List<PrintColumnDef> GetPrintColumnDefs() =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();

    private async Task PrintSelected()
    {
        if (!selectedIds.Any()) { Snackbar.Add("请先选择要打印的记录", Severity.Warning); return; }
        // 富布局单据打印（后端 NcrPrintHelper 忽略 columns）：只发 ids，不携带 Columns → print.js 35 列校验自动跳过；
        // 第三参 true 显式声明富布局语义，防未来误加 columns 被列表打印列数上限误拦
        var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.Ncr}/print-selected-file";
        var json = JsonSerializer.Serialize(new { ids = selectedIds.ToArray() });
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json, true);
    }

    /// <summary>打印选中列表（按当前可见列渲染列表 PDF，Mode A 前端已准备数据）</summary>
    private async Task PrintSelectedList()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的记录", Severity.Warning);
            return;
        }
        try
        {
            // 列过多时各列被压缩到单字符放不下的宽度 → QuestPDF 布局冲突；A4 可显示列数上限 35 列（与后端 TablePrintHelper.MaxPrintColumns 同步），超限提前拦截并页面内警示
            const int MaxPrintColumns = 35;
            var visible = _visibleColumns;
            if (visible.Count > MaxPrintColumns)
            {
                Snackbar.Add($"当前可见列过多（{visible.Count} 列，打印上限 {MaxPrintColumns} 列），请通过列显隐精简后再打印", Severity.Warning);
                return;
            }

            var selectedItems = _pageItems
                .Where(o => selectedIds.Contains(o.Id))
                .Select(item =>
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var col in visible)
                        dict[col.Key] = GetCellDisplayText(item, col.Key) ?? "-";
                    return dict;
                }).ToList();

            var request = new NcrPrintListRequest
            {
                Title = "不合格报告列表",
                Items = selectedItems,
                Columns = GetPrintColumnDefs()
            };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.Ncr}/print-list-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    // 待处理卡片
    private List<NcrPendingCheckDto> _pendingItems = new();
    private bool _showPending = false;

    // ========== 不合格品实时待处理折叠表（表1） ==========
    private bool _showPendingOverview = false;

    // ========== 不合格品月度汇总折叠表（表2） ==========
    private bool _showMonthlySummary = false;
    private bool _isLoadingMonthly = false;
    private NcrMonthlySummaryDto? _monthlySummary;
    private List<NcrMonthlyRowDto> _monthlyRows = new();
    private List<int> _monthlyCategoryRowspans = new();
    private List<int> _monthlyDeptRowspans = new();
    private List<(int Qty, int? Weight)> _monthlyDeptTotals = new();
    private List<(int Qty, int? Weight)> _monthlyCategoryTotals = new();

    // 筛选
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // 责任类别下拉（配置表动态加载，失败兜底内置 5 值）
    private List<(string Value, string Text)> _responsibilityOptions = new()
    {
        (NcrResponsibilityKeys.ProductionInternal, DictValueDisplayHelper.GetText(DictValueDefaults.NcrResponsibilityKey, NcrResponsibilityKeys.ProductionInternal) ?? ""),
        (NcrResponsibilityKeys.ProductionOutsource, DictValueDisplayHelper.GetText(DictValueDefaults.NcrResponsibilityKey, NcrResponsibilityKeys.ProductionOutsource) ?? ""),
        (NcrResponsibilityKeys.MaterialTubeBlank, DictValueDisplayHelper.GetText(DictValueDefaults.NcrResponsibilityKey, NcrResponsibilityKeys.MaterialTubeBlank) ?? ""),
        (NcrResponsibilityKeys.MaterialPurchased, DictValueDisplayHelper.GetText(DictValueDefaults.NcrResponsibilityKey, NcrResponsibilityKeys.MaterialPurchased) ?? ""),
        (NcrResponsibilityKeys.MaterialSurplus, DictValueDisplayHelper.GetText(DictValueDefaults.NcrResponsibilityKey, NcrResponsibilityKeys.MaterialSurplus) ?? ""),
    };

    private async Task LoadResponsibilityOptionsAsync()
    {
        var result = await DictValueDefinitionService.GetEnabledValuesAsync(DictValueDefaults.NcrResponsibilityKey);
        if (result.Success && result.Data is { Count: > 0 })
        {
            _responsibilityOptions = result.Data
                .Select(t => (t.Value, t.DisplayName))
                .ToList();
        }
    }

    // 列定义
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    // B33: 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "DefectiveQuantity", "DefectiveWeight"
    };

    // 扩展常量
    private const string PageType = "ncrs";

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        // G1: 问题反馈
        new() { Key = "ReportDate",          Label = "反馈日期",    SortKey = "reportdate",       FilterType = "date",   Width = "100",
               GroupKey = 1, GroupName = "G1 问题反馈" },
        new() { Key = "ReportDepartment",     Label = "反馈部门",    SortKey = "reportdepartment",  FilterType = "string", Width = "100",
               GroupKey = 1, GroupName = "G1 问题反馈" },
        new() { Key = "Reporter",             Label = "反馈人",      SortKey = "reporter",          FilterType = "string", Width = "80",
               GroupKey = 1, GroupName = "G1 问题反馈" },
        new() { Key = "PipeCategory",         Label = "物料类型",    SortKey = "pipecategory",      FilterType = "enum",   Width = "100",
               GroupKey = 1, GroupName = "G1 问题反馈",
               EnumOptions = new List<EnumOption>
               {
                   new("TubeBlank", "荒管"), new("WorkInProgress", "在制品"), new("SurplusInventory", "余库料"),
                   new("CriticalFinished", "临界成品"), new("PreparedFinished", "备料成品"), new("OrderFinished", "订单成品"), new("SpecialDelivery", "订成-非交付态"),
               } },
        new() { Key = "BatchNo",              Label = "生产编号",    SortKey = "batchno",           FilterType = "string", Width = "120",
               GroupKey = 1, GroupName = "G1 问题反馈" },
        new() { Key = "WorkOrderNo",          Label = "工单号",      SortKey = "workorderno",       FilterType = "string", Width = "120",
               GroupKey = 1, GroupName = "G1 问题反馈" },
        new() { Key = "PlantGrade",           Label = "牌号",        SortKey = "plantgrade",        FilterType = "string", Width = "80",
               GroupKey = 1, GroupName = "G1 问题反馈" },
        new() { Key = "Specification",        Label = "规格",        SortKey = "specification",     FilterType = "string", Width = "100",
               GroupKey = 1, GroupName = "G1 问题反馈" },
        new() { Key = "DefectiveQuantity",    Label = "次品支数",  SortKey = "defectivequantity",                       Width = "80",
               GroupKey = 1, GroupName = "G1 问题反馈" },
        new() { Key = "DefectiveWeight",      Label = "次品重量",  SortKey = "defectiveweight",                        Width = "80",
               GroupKey = 1, GroupName = "G1 问题反馈" },
        new() { Key = "ProblemDescription",   Label = "问题描述",    SortKey = "problemdescription",FilterType = "string", Width = "150",
               GroupKey = 1, GroupName = "G1 问题反馈" },

        // G2: 不合格品处置
        new() { Key = "DisposalMethod",       Label = "处置方式",    SortKey = "disposalmethod",     FilterType = "enum",  Width = "100",
               GroupKey = 2, GroupName = "G2 不合格品处置",
               EnumOptions = DisplayHelper.GetEnumFilterOptions<DisposalMethod>() },
        new() { Key = "DisposalIsCompleted",  Label = "处置完结",    SortKey = "disposaliscompleted", FilterType = "boolean", Width = "70",
               GroupKey = 2, GroupName = "G2 不合格品处置",
               BoolTrueLabel = "是", BoolFalseLabel = "否" },
        new() { Key = "DisposalCompleteDate", Label = "处置完结日期",SortKey = "disposalcompletedate", FilterType = "date",  Width = "100",
               GroupKey = 2, GroupName = "G2 不合格品处置" },
        new() { Key = "DisposalRemark",       Label = "处置备注",    SortKey = "disposalremark",       FilterType = "string", Width = "120",
               GroupKey = 2, GroupName = "G2 不合格品处置" },

        // G3: 原因分析
        new() { Key = "Severity",             Label = "严重程度",    SortKey = "severity",           FilterType = "enum",   Width = "80",
               GroupKey = 3, GroupName = "G3 原因分析",
               EnumOptions = DisplayHelper.GetEnumFilterOptions<SeverityLevel>() },
        new() { Key = "RootCauseAnalysis",    Label = "原因分析",    SortKey = "rootcauseanalysis",   FilterType = "string", Width = "150",
               GroupKey = 3, GroupName = "G3 原因分析" },
        new() { Key = "AnalysisConfirmer",    Label = "分析确认人",  SortKey = "analysisconfirmer",   FilterType = "string", Width = "100",
               GroupKey = 3, GroupName = "G3 原因分析" },
        new() { Key = "AnalysisConfirmDate",  Label = "确认日期",    SortKey = "analysisconfirmdate", FilterType = "date",   Width = "100",
               GroupKey = 3, GroupName = "G3 原因分析" },

        // G4: 责任人及处理
        new() { Key = "ResponsibilityCategory", Label = "责任类别",  SortKey = "responsibilitycategory", FilterType = "string", Width = "110",
               GroupKey = 4, GroupName = "G4 责任人及处理" },
        new() { Key = "ResponsibleDept",      Label = "责任部门",    SortKey = "responsibledept",     FilterType = "string", Width = "120",
               GroupKey = 4, GroupName = "G4 责任人及处理" },
        new() { Key = "ResponsiblePerson",    Label = "责任人",      SortKey = "responsibleperson",   FilterType = "string", Width = "80",
               GroupKey = 4, GroupName = "G4 责任人及处理" },
        new() { Key = "OperationDate",        Label = "操作日期",    SortKey = "operationdate",       FilterType = "date",   Width = "100",
               GroupKey = 4, GroupName = "G4 责任人及处理" },
        new() { Key = "PersonDisposition",    Label = "责任人处理",  SortKey = "persondisposition",   FilterType = "string", Width = "120",
               GroupKey = 4, GroupName = "G4 责任人及处理" },
        new() { Key = "PersonCompleteDate",   Label = "追责完结日期",SortKey = "personcompletedate",  FilterType = "date",   Width = "100",
               GroupKey = 4, GroupName = "G4 责任人及处理" },
        new() { Key = "PersonIsCompleted",    Label = "追责完结",    SortKey = "personiscompleted",   FilterType = "boolean", Width = "70",
               GroupKey = 4, GroupName = "G4 责任人及处理",
               BoolTrueLabel = "是", BoolFalseLabel = "否" },

        // G5: 纠正预防措施及结果验证
        new() { Key = "CorrectiveAction",     Label = "纠正预防措施",SortKey = "correctiveaction",    FilterType = "string", Width = "150",
               GroupKey = 5, GroupName = "G5 纠正预防措施" },
        new() { Key = "ActionPlanner",        Label = "计划人",      SortKey = "actionplanner",       FilterType = "string", Width = "80",
               GroupKey = 5, GroupName = "G5 纠正预防措施" },
        new() { Key = "ActionPlanDate",       Label = "计划日期",    SortKey = "actionplandate",      FilterType = "date",   Width = "100",
               GroupKey = 5, GroupName = "G5 纠正预防措施" },
        new() { Key = "ActionVerifier",       Label = "验证人",      SortKey = "actionverifier",      FilterType = "string", Width = "80",
               GroupKey = 5, GroupName = "G5 纠正预防措施" },
        new() { Key = "ActionVerifyDate",     Label = "验证日期",    SortKey = "actionverifydate",    FilterType = "date",   Width = "100",
               GroupKey = 5, GroupName = "G5 纠正预防措施" },
        new() { Key = "VerifyResult",         Label = "验证结论",    SortKey = "verifyresult",        FilterType = "enum",   Width = "100",
               GroupKey = 5, GroupName = "G5 纠正预防措施",
               EnumOptions = DisplayHelper.GetEnumFilterOptions<VerifyResult>() },
        new() { Key = "ActionResult",         Label = "结果判定",    SortKey = "actionresult",        FilterType = "string", Width = "120",
               GroupKey = 5, GroupName = "G5 纠正预防措施" },

        // 状态
        new() { Key = "Status",               Label = "状态",        SortKey = "status",              FilterType = "enum",   Width = "80",
               GroupKey = 6, GroupName = "状态",
               EnumOptions = DisplayHelper.GetEnumFilterOptions<NcrStatus>() },

        // 审计
        new() { Key = "UpdatedTime",          Label = "更新日期",    SortKey = "updatedtime",         Width = "120",
               GroupKey = 6, GroupName = "状态" },
    };

    // ========== 生命周期 ==========

    protected override async Task OnInitializedAsync()
    {
        // 初始化列定义
        _allColumns = GetAllColumnDefs();

        // 恢复列偏好（合并保存的可见性/排序，不替换）
        var savedCols = await ColumnPrefs.LoadAsync(PageType, null);
        if (savedCols.Count > 0)
        {
            foreach (var s in savedCols)
            {
                var match = _allColumns.FirstOrDefault(c => c.Key == s.Key);
                if (match != null)
                    match.Visible = s.Visible;
            }
            var reordered = new List<ColumnDef>();
            foreach (var s in savedCols)
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

        // 恢复页面状态
        var savedState = await PageState.LoadAsync(PageType);
        if (savedState != null)
        {
            _searchKeyword = savedState.Keyword ?? "";
            sortColumn = savedState.SortBy ?? "reportdate";
            sortDescending = savedState.IsDescending;
            _restoredPageIndex = savedState.PageIndex;
            if (savedState.Extras?.ContainsKey("dateFrom") == true)
                _dateFrom = savedState.Extras["dateFrom"];
            if (savedState.Extras?.ContainsKey("dateTo") == true)
                _dateTo = savedState.Extras["dateTo"];
            if (savedState.Filters?.Count > 0)
            {
                _columnFilters = savedState.Filters
                    .Where(f => f.Values?.Count > 0)
                    .ToDictionary(f => f.Field, f => new HashSet<string>(f.Values!));
            }
        }

        // 加载筛选上下文
        await Task.WhenAll(
            LoadFilterContextsAsync(),
            LoadPendingChecksAsync(),
            LoadResponsibilityOptionsAsync()
        );
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            if (table != null)
                await table.ReloadServerData();
            await JS.InvokeVoidAsync("initGroupHeaders", "#ncrs-list-table");
        }
    }

    // ========== 数据加载 ==========

    private async Task<TableData<NcrDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        try
        {
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

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "reportdate";
            var filtersJson = SerializeFilters();

            DateTime? dateFrom = null;
            DateTime? dateTo = null;
            if (DateTime.TryParse(_dateFrom, out var df)) dateFrom = df;
            if (DateTime.TryParse(_dateTo, out var dt)) dateTo = dt;

            var result = await NcrService.GetAllAsync(
                pageIndex: state.Page + 1,
                pageSize: state.PageSize,
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy: sortBy,
                isDescending: sortDescending,
                filters: filtersJson,
                reportDateFrom: dateFrom,
                reportDateTo: dateTo);

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<NcrDto> { Items = _pageItems, TotalItems = _totalCount };

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                ComputePageSums();
            }
            else
            {
                _pageItems = new();
                _totalCount = 0;
                _pageSums.Clear();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _pageItems = new();
            _totalCount = 0;
            _pageSums.Clear();
        }

        return new TableData<NcrDto>
        {
            Items = _pageItems,
            TotalItems = _totalCount
        };
    }

    // ========== 搜索 ==========

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value;
        _resetToFirstPage = true;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnDateFromChanged(string value)
    {
        _dateFrom = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnDateToChanged(string value)
    {
        _dateTo = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 排序 ==========

    private async Task ToggleSort(string sortKey)
    {
        if (sortColumn == sortKey)
            sortDescending = !sortDescending;
        else
        {
            sortColumn = sortKey;
            sortDescending = true;
        }
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 筛选 ==========

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

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
            var result = await NcrService.GetFilterContextsAsync();
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
                Display = v,
                Count = 0
            }).ToList();
        }

        // 枚举列显示中文标签
        foreach (var col in _allColumns)
        {
            if (col.FilterType == "enum" && col.EnumOptions != null && _filterContextOptions.TryGetValue(col.Key, out var options))
            {
                var displayMap = col.EnumOptions.ToDictionary(e => e.Value, e => e.Display);
                foreach (var opt in options)
                {
                    if (displayMap.TryGetValue(opt.Value, out var display))
                        opt.Display = display;
                }
            }
        }

        // 责任类别（字典列）：后端返回英文 Key 值，映射中文显示；无后端选项时用字典下拉补齐
        var rcMap = _responsibilityOptions.ToDictionary(o => o.Value, o => o.Text);
        if (_filterContextOptions.TryGetValue("ResponsibilityCategory", out var rcOptions))
        {
            foreach (var opt in rcOptions)
            {
                if (rcMap.TryGetValue(opt.Value, out var rcText))
                    opt.Display = rcText;
            }
        }
        else
        {
            _filterContextOptions["ResponsibilityCategory"] = _responsibilityOptions
                .Select(o => new ExcelFilterOption { Value = o.Value, Display = o.Text, Count = 0 })
                .ToList();
        }

        // 补充枚举列筛选选项（后端不返回枚举列 DISTINCT 值）
        foreach (var col in _allColumns)
        {
            if (col.FilterType == "enum" && col.EnumOptions != null && !_filterContextOptions.ContainsKey(col.Key))
            {
                _filterContextOptions[col.Key] = col.EnumOptions.Select(e => new ExcelFilterOption
                {
                    Value = e.Value,
                    Display = e.Display,
                    Count = 0
                }).ToList();
            }
        }

        // 补充布尔列筛选选项
        foreach (var col in _allColumns)
        {
            if (col.FilterType == "boolean" && !_filterContextOptions.ContainsKey(col.Key))
            {
                _filterContextOptions[col.Key] = DisplayHelper.GetBoolFilterOptions(col);
            }
        }
    }

    // ========== 操作 ==========

    private void CreateNew()
    {
        Navigation.NavigateTo("/quality/ncr/create");
    }

    private void EditItem(int id)
    {
        Navigation.NavigateTo($"/quality/ncr/{id}");
    }

    private async Task DeleteItem(int id)
    {
        var dialog = await DialogService.ShowAsync<ConfirmDialog>("确认删除",
            new DialogParameters { ["ContentText"] = "确定要删除该不合格品报告吗？" });
        var result = await dialog.Result;
        if (result.Canceled) return;

        var response = await NcrService.DeleteAsync(id);
        if (response.Success)
        {
            Snackbar.Add("删除成功", Severity.Success);
            if (table != null) await table.ReloadServerData();
            await LoadFilterContextsAsync();
        }
        else
        {
            Snackbar.Add($"删除失败: {response.Message}", Severity.Error);
        }
    }

    private async Task UpdateStatus(int id, NcrStatus status)
    {
        var statusText = status switch
        {
            NcrStatus.Processing => "处理中",
            NcrStatus.Closed => "已关闭",
            _ => ""
        };
        if (string.IsNullOrEmpty(statusText)) return;

        var dialog = await DialogService.ShowAsync<ConfirmDialog>("确认状态变更",
            new DialogParameters { ["ContentText"] = $"确定要将状态变更为「{statusText}」吗？" });
        var result = await dialog.Result;
        if (result.Canceled) return;

        var response = await NcrService.UpdateStatusAsync(id, status.ToString());
        if (response.Success)
        {
            Snackbar.Add($"状态已变更为: {statusText}", Severity.Success);
            if (table != null) await table.ReloadServerData();
        }
        else
        {
            Snackbar.Add($"状态变更失败: {response.Message}", Severity.Error);
        }
    }

    // ========== 列选择器 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await ColumnPrefs.SaveAsync(PageType, null, _allColumns);
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        var idx = _allColumns.IndexOf(col);
        if (idx > 0)
        {
            _allColumns.RemoveAt(idx);
            _allColumns.Insert(idx - 1, col);
        }
        await ColumnPrefs.SaveAsync(PageType, null, _allColumns);
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        var idx = _allColumns.IndexOf(col);
        if (idx < _allColumns.Count - 1)
        {
            _allColumns.RemoveAt(idx);
            _allColumns.Insert(idx + 1, col);
        }
        await ColumnPrefs.SaveAsync(PageType, null, _allColumns);
    }

    // ========== 分页汇总（B33） ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(NcrDto)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);

        foreach (var key in _summableColumnKeys)
        {
            if (!props.TryGetValue(key, out var prop)) continue;
            var type = prop.PropertyType;
            try
            {
                if (type == typeof(int) || type == typeof(int?))
                {
                    var sum = _pageItems.Sum(item =>
                    {
                        var v = prop.GetValue(item);
                        return v != null ? Convert.ToInt32(v) : 0;
                    });
                    _pageSums[key] = sum.ToString();
                }
                else if (type == typeof(decimal?) || type == typeof(decimal))
                {
                    var sum = _pageItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
                    _pageSums[key] = ((int)sum).ToString();
                }
            }
            catch { }
        }
    }

    private string RenderFooterCell(ColumnDef col)
    {
        return _pageSums.GetValueOrDefault(col.Key, "");
    }

    // ========== 页面状态持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(_dateFrom))
            extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrEmpty(_dateTo))
            extras["dateTo"] = _dateTo;
        await PageState.SaveAsync(PageType, new PageState
        {
            Keyword = _searchKeyword,
            SortBy = sortColumn,
            IsDescending = sortDescending,
            PageIndex = table?.CurrentPage ?? 0,
            Filters = _columnFilters.Count > 0
                ? _columnFilters.Select(kvp => new FilterDescriptor
                {
                    Field = kvp.Key,
                    Operator = "in",
                    Values = kvp.Value.ToList()
                }).ToList()
                : null,
            Extras = extras.Count > 0 ? extras : null
        });
    }

    // ========== 待处理卡片 ==========

    private async Task LoadPendingChecksAsync()
    {
        try
        {
            var result = await NcrService.GetPendingChecksAsync();
            if (result.Success && result.Data != null)
                _pendingItems = result.Data;
        }
        catch { }
    }

    private void TogglePendingChecks() => _showPending = !_showPending;

    // ========== 不合格品实时待处理折叠表（表1） ==========

    private void TogglePendingOverview() => _showPendingOverview = !_showPendingOverview;

    /// <summary>反馈部门 = 来源 + 检验项目（中文化，与 NcrForm 自动填充口径一致）</summary>
    private static string GetPendingReportDepartment(NcrPendingCheckDto item)
    {
        var sourceText = GetSourceTypeText(item.SourceType);
        var itemText = GetInspectionItemDisplay(item.InspectionItem, item.SourceType);
        return string.IsNullOrEmpty(itemText) ? sourceText : $"{sourceText}-{itemText}";
    }

    /// <summary>物料类型（过程检验按工序名判荒管/在制；成品检验按物料名解析，与 NcrForm 口径一致）</summary>
    private static string GetPendingPipeCategoryText(NcrPendingCheckDto item)
    {
        if (item.SourceType == "ProcessInspection")
        {
            var category = string.Equals(item.ProcessName, ProcessKeys.RoughTubeProcessing, StringComparison.OrdinalIgnoreCase)
                ? MaterialType.RoughTube
                : MaterialType.WorkInProgress;
            return DisplayHelper.GetMaterialTypeText(category);
        }
        if (item.SourceType == "FinalInspection")
        {
            var category = string.IsNullOrEmpty(item.MaterialName)
                ? MaterialType.WorkInProgress
                : (Enum.TryParse<MaterialType>(item.MaterialName, true, out var mt) ? mt : MaterialType.WorkInProgress);
            return DisplayHelper.GetMaterialTypeText(category);
        }
        return "";
    }

    private async Task PrintPendingOverviewTable()
    {
        if (_pendingItems.Count == 0)
        {
            Snackbar.Add("暂无数据可打印", Severity.Warning);
            return;
        }
        try
        {
            var html = await JS.InvokeAsync<string>("getTableHtml", "#ncrs-pending-overview-table");
            if (!string.IsNullOrEmpty(html))
                await JS.InvokeVoidAsync("printRawHtml", html, "不合格品实时待处理");
            else
                Snackbar.Add("未找到可打印的汇总表格", Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 不合格品月度汇总折叠表（表2） ==========

    private void ToggleMonthlySummary()
    {
        _showMonthlySummary = !_showMonthlySummary;
        if (_showMonthlySummary && _monthlySummary == null)
            _ = LoadMonthlySummaryAsync();
    }

    private async Task LoadMonthlySummaryAsync()
    {
        _isLoadingMonthly = true;
        StateHasChanged();
        try
        {
            var result = await NcrService.GetMonthlySummaryAsync();
            if (result.Success && result.Data != null)
            {
                _monthlySummary = result.Data;
                _monthlyRows = result.Data.Rows;
                ComputeMonthlyRowspans();
            }
            else
            {
                _monthlySummary = null;
                _monthlyRows = new();
                Snackbar.Add(result.Message ?? "加载失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            _monthlySummary = null;
            _monthlyRows = new();
            Snackbar.Add($"加载异常: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoadingMonthly = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 计算月度汇总三级合并 rowspan（后端已按 责任类别→责任部门→处置方式 排序，同组相邻）。
    /// 责任类别 rowspan 合并 + 责任部门 rowspan 合并 + 部门/类别全年合计（首行非 0）。
    /// </summary>
    private void ComputeMonthlyRowspans()
    {
        _monthlyCategoryRowspans = new List<int>(new int[_monthlyRows.Count]);
        _monthlyDeptRowspans = new List<int>(new int[_monthlyRows.Count]);
        _monthlyDeptTotals = new List<(int, int?)>(new (int, int?)[_monthlyRows.Count]);
        _monthlyCategoryTotals = new List<(int, int?)>(new (int, int?)[_monthlyRows.Count]);

        var i = 0;
        while (i < _monthlyRows.Count)
        {
            var category = _monthlyRows[i].ResponsibilityCategory;
            var catCount = 1;
            while (i + catCount < _monthlyRows.Count
                   && string.Equals(_monthlyRows[i + catCount].ResponsibilityCategory, category, StringComparison.Ordinal))
                catCount++;
            _monthlyCategoryRowspans[i] = catCount;
            _monthlyCategoryTotals[i] = (
                _monthlyRows.Skip(i).Take(catCount).Sum(r => r.TotalQuantity),
                _monthlyRows.Skip(i).Take(catCount).Sum(r => r.TotalWeight ?? 0));

            var j = i;
            var catEnd = i + catCount;
            while (j < catEnd)
            {
                var dept = _monthlyRows[j].ResponsibleDept;
                var deptCount = 1;
                while (j + deptCount < catEnd
                       && string.Equals(_monthlyRows[j + deptCount].ResponsibleDept, dept, StringComparison.Ordinal))
                    deptCount++;
                _monthlyDeptRowspans[j] = deptCount;
                _monthlyDeptTotals[j] = (
                    _monthlyRows.Skip(j).Take(deptCount).Sum(r => r.TotalQuantity),
                    _monthlyRows.Skip(j).Take(deptCount).Sum(r => r.TotalWeight ?? 0));
                j += deptCount;
            }

            i += catCount;
        }
    }

    /// <summary>次品支数/重量单元格格式化：80支/565Kg，为 0 的部分省略，全 0 返回空串</summary>
    private static string FormatNcrCell(int quantity, int? weight)
    {
        var parts = new List<string>();
        if (quantity > 0) parts.Add($"{quantity}支");
        if (weight is > 0) parts.Add($"{weight}Kg");
        return string.Join("/", parts);
    }

    private async Task PrintMonthlySummaryTable()
    {
        if (_monthlyRows.Count == 0)
        {
            Snackbar.Add("暂无数据可打印", Severity.Warning);
            return;
        }
        try
        {
            var html = await JS.InvokeAsync<string>("getTableHtml", "#ncrs-monthly-summary-table-wrap");
            if (!string.IsNullOrEmpty(html))
            {
                // 横向 A4 + 表格撑满页宽（table-layout:fixed + white-space:normal），列总宽度不超过单页界限
                var printHtml = "<style>" +
                    "table{width:100%!important;table-layout:fixed!important;font-size:12px!important;border-collapse:collapse!important;}" +
                    "th,td{white-space:normal!important;padding:3px 4px!important;text-align:center!important;border:1px solid #333!important;}" +
                    "</style>" + html;
                await JS.InvokeVoidAsync("printRawHtml", printHtml, "不合格品月度汇总", "landscape");
            }
            else
            {
                Snackbar.Add("未找到可打印的不合格品月度汇总表格", Severity.Warning);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private void CreateFromPending(NcrPendingCheckDto item)
    {
        Navigation.NavigateTo($"/quality/ncr/create?batchNo={Uri.EscapeDataString(item.BatchNo)}" +
            $"&disposalMethod={item.DisposalMethod}" +
            $"&sourceType={item.SourceType}" +
            $"&defectQty={item.DefectQuantity}" +
            $"&defectWeight={item.DefectiveWeight}" +
            $"&inspector={Uri.EscapeDataString(item.Inspector ?? "")}" +
            $"&inspectionItem={Uri.EscapeDataString(item.InspectionItem ?? "")}" +
            $"&processName={Uri.EscapeDataString(item.ProcessName ?? "")}" +
            $"&materialName={Uri.EscapeDataString(item.MaterialName ?? "")}" +
            $"&reportDate={item.ReportDate:yyyy-MM-dd}" +
            $"&defectDescription={Uri.EscapeDataString(item.DefectDescription ?? "")}");
    }

    private static string GetSourceTypeText(string sourceType) => EnumHelper.GetDisplayName<ReportTemplateType>(sourceType);

    private static string GetInspectionItemDisplay(string? item, string? sourceType)
    {
        _ = sourceType;
        if (string.IsNullOrEmpty(item)) return "";
        if (Enum.TryParse<InspectionItem>(item, true, out var enumItem))
            return DisplayHelper.GetInspectionItemText(enumItem);
        return item;
    }

    private static string GetDisposalMethodText(DisposalMethod method) => DisplayHelper.GetDisposalMethodText(method);

    private static Color GetWarningColor() => Color.Warning;

    private static Color GetSourceTypeColor(string sourceType)
        => sourceType == "ProcessInspection" ? Color.Info : Color.Primary;

    private static Color GetDisposalChipColor(DisposalMethod method) => method switch
    {
        DisposalMethod.Rework => Color.Warning,
        DisposalMethod.WarehouseEntry => Color.Info,
        DisposalMethod.Scrap => Color.Error,
        _ => Color.Default
    };

    // ========== 列显示重置 ==========

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await ColumnPrefs.SaveAsync(PageType, null, _allColumns);
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
        StateHasChanged();
    }

    // ========== 分组 CSS ==========

    private List<GroupHeaderInfo> GetGroupHeaders()
    {
        var result = new List<GroupHeaderInfo>();
        int? lastKey = null;
        int totalWidth = 0;
        var groupKey = 0;
        var groupName = "";
        var count = 0;

        foreach (var col in _visibleColumns)
        {
            var gk = col.GroupKey ?? 0;
            if (gk != lastKey && lastKey.HasValue)
            {
                result.Add(new GroupHeaderInfo
                {
                    GroupKey = groupKey,
                    GroupName = groupName,
                    TotalWidth = totalWidth,
                    ColumnCount = count,
                    CssClass = GetHeaderGroupCss(groupKey, true)
                });
                totalWidth = 0;
                count = 0;
            }
            groupKey = gk;
            groupName = col.GroupName ?? "";
            totalWidth += int.TryParse(col.Width, out var w) ? w : 100;
            count++;
            lastKey = gk;
        }
        if (count > 0)
        {
            result.Add(new GroupHeaderInfo
            {
                GroupKey = groupKey,
                GroupName = groupName,
                TotalWidth = totalWidth,
                ColumnCount = count,
                CssClass = GetHeaderGroupCss(groupKey, true)
            });
        }
        // 复选框列占位符（40px）
        result.Insert(0, new GroupHeaderInfo
        {
            GroupKey = 0,
            GroupName = "",
            TotalWidth = 40,
            ColumnCount = 0,
            CssClass = "col-selection-th"
        });
        // 操作列尾随占位符（160px）
        result.Add(new GroupHeaderInfo
        {
            GroupKey = 0,
            GroupName = "",
            TotalWidth = 160,
            ColumnCount = 0,
            CssClass = ""
        });
        return result;
    }

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1",
            2 => "col-g2",
            3 => "col-g3",
            4 => "col-g4",
            5 => "col-g5",
            6 => "col-g6",
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start";
        return cls;
    }

    private static string GetCellGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1-cell",
            2 => "col-g2-cell",
            3 => "col-g3-cell",
            4 => "col-g4-cell",
            5 => "col-g5-cell",
            6 => "col-g6-cell",
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    // ========== 显示格式化 ==========

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(NcrDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "Status":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Color", GetStatusColor(item.Status));
                builder.AddAttribute(2, "Size", Size.Small);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b => b.AddContent(0, GetStatusText(item.Status))));
                builder.CloseComponent();
                break;
            case "PipeCategory":
                builder.AddContent(0, GetPipeCategoryText(item.PipeCategory));
                break;
            case "Reporter":
                builder.AddContent(0, DisplayHelper.FormatPersonName(item.Reporter));
                break;
            case "DisposalMethod":
                builder.AddContent(0, GetDisposalMethodText(item.DisposalMethod));
                break;
            case "Severity":
                if (item.Severity != null)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Color", GetSeverityColor(item.Severity));
                    builder.AddAttribute(2, "Size", Size.Small);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b => b.AddContent(0, GetSeverityText(item.Severity))));
                    builder.CloseComponent();
                }
                break;
            case "ResponsibilityCategory":
                builder.AddContent(0, DictValueDisplayHelper.GetText(DictValueDefaults.NcrResponsibilityKey, item.ResponsibilityCategory) ?? "");
                break;
            case "VerifyResult":
                builder.AddContent(0, GetVerifyResultText(item.VerifyResult));
                break;
            case "DisposalIsCompleted":
                builder.AddContent(0, item.DisposalIsCompleted ? "是" : "否");
                break;
            case "PersonIsCompleted":
                builder.AddContent(0, item.PersonIsCompleted ? "是" : "否");
                break;
            case "ReportDate":
                builder.AddContent(0, item.ReportDate.ToString("yyyy-MM-dd"));
                break;
            case "OperationDate":
            case "DisposalCompleteDate":
            case "AnalysisConfirmDate":
            case "ActionPlanDate":
            case "ActionVerifyDate":
            case "PersonCompleteDate":
                builder.AddContent(0, GetDateValue(item, col.Key));
                break;
            case "UpdatedTime":
                builder.AddContent(0, item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;
            default:
                var val = GetPropertyValue(item, col.Key);
                builder.AddContent(0, val ?? "");
                break;
        }
    };

    private static string? GetDateValue(NcrDto item, string key) => key switch
    {
        "OperationDate" => item.OperationDate?.ToString("yyyy-MM-dd"),
        "DisposalCompleteDate" => item.DisposalCompleteDate?.ToString("yyyy-MM-dd"),
        "AnalysisConfirmDate" => item.AnalysisConfirmDate?.ToString("yyyy-MM-dd"),
        "ActionPlanDate" => item.ActionPlanDate?.ToString("yyyy-MM-dd"),
        "ActionVerifyDate" => item.ActionVerifyDate?.ToString("yyyy-MM-dd"),
        "PersonCompleteDate" => item.PersonCompleteDate?.ToString("yyyy-MM-dd"),
        _ => null
    };

    private static string? GetPropertyValue(NcrDto item, string key)
    {
        var prop = typeof(NcrDto).GetProperty(key);
        if (prop == null) return null;
        var val = prop.GetValue(item);
        return val?.ToString();
    }

    /// <summary>按列取表格显示文本（复用 RenderCell 各分支口径，保证打印列表与页面单元格一致）</summary>
    private string? GetCellDisplayText(NcrDto item, string key) => key switch
    {
        "Status" => GetStatusText(item.Status),
        "PipeCategory" => GetPipeCategoryText(item.PipeCategory),
        "Reporter" => DisplayHelper.FormatPersonName(item.Reporter),
        "DisposalMethod" => GetDisposalMethodText(item.DisposalMethod),
        "Severity" => GetSeverityText(item.Severity),
        "ResponsibilityCategory" => DictValueDisplayHelper.GetText(DictValueDefaults.NcrResponsibilityKey, item.ResponsibilityCategory) ?? "",
        "VerifyResult" => GetVerifyResultText(item.VerifyResult),
        "DisposalIsCompleted" => item.DisposalIsCompleted ? "是" : "否",
        "PersonIsCompleted" => item.PersonIsCompleted ? "是" : "否",
        "ReportDate" => item.ReportDate.ToString("yyyy-MM-dd"),
        "OperationDate" => item.OperationDate?.ToString("yyyy-MM-dd"),
        "DisposalCompleteDate" => item.DisposalCompleteDate?.ToString("yyyy-MM-dd"),
        "AnalysisConfirmDate" => item.AnalysisConfirmDate?.ToString("yyyy-MM-dd"),
        "ActionPlanDate" => item.ActionPlanDate?.ToString("yyyy-MM-dd"),
        "ActionVerifyDate" => item.ActionVerifyDate?.ToString("yyyy-MM-dd"),
        "PersonCompleteDate" => item.PersonCompleteDate?.ToString("yyyy-MM-dd"),
        "UpdatedTime" => item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        _ => GetPropertyValue(item, key)
    };

    private static Color GetStatusColor(NcrStatus status) => status switch
    {
        NcrStatus.Pending => Color.Info,
        NcrStatus.Processing => Color.Warning,
        NcrStatus.Closed => Color.Success,
        _ => Color.Default
    };

    private string GetStatusText(NcrStatus status) => DisplayHelper.GetNcrStatusText(status);

    private string GetPipeCategoryText(MaterialType category) => DisplayHelper.GetMaterialTypeText(category);

    private string GetDisposalMethodText(DisposalMethod? method) => method.HasValue ? DisplayHelper.GetDisposalMethodText(method.Value) : "";

    private string GetSeverityText(SeverityLevel? severity) => severity.HasValue ? DisplayHelper.GetSeverityLevelText(severity.Value) : "";

    private string GetVerifyResultText(VerifyResult? result) => result.HasValue ? DisplayHelper.GetVerifyResultText(result.Value) : "";

    private static Color GetSeverityColor(SeverityLevel? severity) => severity switch
    {
        SeverityLevel.Critical => Color.Error,
        SeverityLevel.General => Color.Warning,
        _ => Color.Default
    };

    // ========== 分组标题信息 ==========

    private class GroupHeaderInfo
    {
        public int GroupKey { get; set; }
        public string GroupName { get; set; } = "";
        public int TotalWidth { get; set; }
        public int ColumnCount { get; set; }
        public string CssClass { get; set; } = "";
    }
}
