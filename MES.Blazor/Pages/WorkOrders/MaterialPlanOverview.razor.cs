using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Enums;
using MES.Core.Constants;
using MES.Core.Helpers;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Core.DTOs.WorkOrder;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using System.Text.Json;
using MES.Shared.Constants;

namespace MES.Blazor.Pages.WorkOrders;

public partial class MaterialPlanOverview
{
    private MudTable<WorkOrderListDto>? table;
    private List<WorkOrderListDto> _pageItems = new();
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new() { "TotalWeight", "TotalItemCount", "TotalQuantity" };
    private int _totalCount;
    private string errorMessage = string.Empty;

    // ========== 待投料量汇总卡片（复用原锁计划数据源） ==========
    private bool _showSummaryCard;              // 卡片显隐（默认折叠）
    private bool _summaryLoading;               // 防止并发重复加载
    private RawMaterialLockPendingSummaryDto? _pendingSummary;

    // ========== 工单原锁-错疑投料卡片（工单执行状况读模型：原料锁定 + 到料实投一致性 2错误+2疑问 明细） ==========
    private bool _showErrorDoubtCard;              // 卡片显隐（默认折叠）
    private bool _errorDoubtLoading;               // 防止并发重复加载
    private List<ErrorDoubtInputItemDto>? _errorDoubtItems;
    private string _errorDoubtSortKey = "WorkOrderNo";   // 卡片排序键（默认工单号升序）
    private bool _errorDoubtSortDesc;                    // 卡片排序方向
    private Dictionary<string, HashSet<string>> _errorDoubtColumnFilters = new();   // 卡片逐列筛选（ExcelFilter）
    private int _errorDoubtDisplayCount = 5;             // 卡片显示行数（默认 5，防视觉污染）
    private static readonly int[] _errorDoubtDisplayOptions = { 5, 10, 15, 20 };
    private const string _errorDoubtNullFilter = "__EXCEL_FILTER_NULL__";   // ExcelFilter 空值占位符（与组件一致）

    // ========== 卡片点击联动筛选（仿订单首页小表点击，覆盖式 + 提示条） ==========
    private MaterialPlanLinkFilterDto? _linkFilter;   // 联动筛选条件（null=未联动）
    private string? _linkLabel;                       // 联动提示条文案（卡片中文标签）
    // 矩阵行/列序 → 英文 Key（与后端 pending-summary 的行列序一致：备注 4 类，计划性排除 EPaused）
    private static readonly string[] _matrixRemarkKeys = RawMaterialLockRemarkKeys.All;
    private static readonly string[] _matrixUrgencyKeys = UrgencyLevelKeys.All.Where(k => k != UrgencyLevelKeys.EPaused).ToArray();
    // 选中状态
    private bool allSelected => _pageItems.Any() && _pageItems.All(i => selectedWorkOrderIds.Contains(i.Id));
    private HashSet<int> selectedWorkOrderIds = new();
    private int _currentPage = 1;
    private int _pageSize = 10;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _loadVersion;
    private bool _resetToFirstPage;
    private bool _isArrowNavSetup;
    private string _searchKeyword = string.Empty;
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;
    private string _deliveryDateFrom = string.Empty;
    private string _deliveryDateTo = string.Empty;

    private string sortColumn = "CreatedTime";
    private bool sortDescending = true;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // 计划类型筛选
    private bool includeSemi = true;
    private bool includeFinish = true;
    private bool includeInventory = true;
    private bool includeRework = true;
    private bool includePiercing = true;
    private bool includeInProcessRework = true;
    private bool includeInMainWorkOrder = true;
    private bool anyPlanTypeSelected => includeSemi || includeFinish || includeInventory || includeRework || includePiercing || includeInProcessRework || includeInMainWorkOrder;

    // ========== 列定义 ==========

    // 列偏好持久化 key（带版本号：列定义变更后自动丢弃旧偏好，强制采用新默认显隐/顺序）
    private const string ColumnPrefsKey = "materialPlanOverview_v4";

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        // ========== 1 基础数据 ==========
        new() { Key = "WorkOrderNo",        Label = "工单号",     SortKey = "WorkOrderNo", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "SalesOrderNo",       Label = "订单号",     SortKey = "SalesOrderNo", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "ProductionMainNo",   Label = "主号",       SortKey = "ProductionMainNo", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "ProductionSubNo",    Label = "次号",       SortKey = "ProductionSubNo", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据", Visible = false },
        new() { Key = "Salesman",           Label = "业务员",     SortKey = "Salesman", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "EndCustomer",        Label = "最终用户",   SortKey = "EndCustomer", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "MaterialName",       Label = "钢管制造",   SortKey = "MaterialName", FilterType = "enum", Width = "120", GroupKey = 1, GroupName = "基础数据", Visible = false,
            EnumOptions = DisplayHelper.GetEnumFilterOptions<PipeManufacturingType>() },
        new() { Key = "SettlementMethod",   Label = "结算方式",   SortKey = "SettlementMethod", FilterType = "enum", Width = "120", GroupKey = 1, GroupName = "基础数据", Visible = false,
            EnumOptions = DisplayHelper.GetEnumFilterOptions<SettlementMethod>() },
        new() { Key = "DelayPenalty",       Label = "延期罚款",   SortKey = "DelayPenalty", FilterType = "enum", Width = "120", GroupKey = 1, GroupName = "基础数据", Visible = false,
            EnumOptions = DisplayHelper.GetBoolOptions() },
        new() { Key = "SignDate",           Label = "签订日期",   SortKey = "SignDate", FilterType = "date", Width = "120", GroupKey = 1, GroupName = "基础数据", Visible = false },
        new() { Key = "DeliveryDate",       Label = "交货日期",   SortKey = "DeliveryDate", FilterType = "date", Width = "120", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "DeliveryState",      Label = "交货状态",   SortKey = "DeliveryState", FilterType = "enum", Width = "120", GroupKey = 1, GroupName = "基础数据",
            EnumOptions = DisplayHelper.GetEnumFilterOptions<DeliveryState>() },
        new() { Key = "PlantGrade",         Label = "工厂牌号",   SortKey = "PlantGrade", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "Specification",      Label = "规格",       SortKey = "Specification", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "LengthStatus",       Label = "长度状态",   SortKey = "LengthStatus", FilterType = "enum", Width = "120", GroupKey = 1, GroupName = "基础数据",
            EnumOptions = DisplayHelper.GetEnumFilterOptions<LengthStatus>() },
        new() { Key = "TotalItemCount",     Label = "含项次数",   SortKey = "TotalItemCount", Width = "80", GroupKey = 1, GroupName = "基础数据", Visible = false },
        new() { Key = "MinLength",          Label = "最小长度",   SortKey = "MinLength", Width = "80", GroupKey = 1, GroupName = "基础数据", Visible = false },
        new() { Key = "MaxLength",          Label = "最大长度",   SortKey = "MaxLength", Width = "80", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "TotalQuantity",      Label = "总支数",     SortKey = "TotalQuantity", Width = "80", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "TotalWeight",        Label = "总重量",     SortKey = "TotalWeight", Width = "80", GroupKey = 1, GroupName = "基础数据" },

        // ========== 2 实时关注（来源 WorkOrderExecutionSummary，主号级） ==========
        new() { Key = "ScheduleStage",          Label = "主号-关注",      SortKey = "ScheduleStage",          FilterType = "enum", Width = "110",
            EnumOptions = DisplayHelper.GetScheduleStageOptions(), GroupKey = 2, GroupName = "实时关注", Level = ColumnLevel.MainNo },
        new() { Key = "RawMaterialLockRemark",  Label = "主号-原锁备注",  SortKey = "RawMaterialLockRemark",  FilterType = "string", Width = "120",
            GroupKey = 2, GroupName = "实时关注", Level = ColumnLevel.MainNo },
        new() { Key = "UrgencyLevel",           Label = "主号-计划性",    SortKey = "UrgencyLevel",           FilterType = "string", Width = "110",
            GroupKey = 2, GroupName = "实时关注", Level = ColumnLevel.MainNo },
        new() { Key = "TotalMissingWeight", Label = "理论原料未至", SortKey = "TotalMissingWeight", FilterType = "number", Width = "90",
            GroupKey = 2, GroupName = "实时关注" },
        new() { Key = "PendingInputWeight", Label = "工单到料未投",   SortKey = "PendingInputWeight", FilterType = "number", Width = "80",
            GroupKey = 2, GroupName = "实时关注" },
        new() { Key = "InputWeight",        Label = "工单投料量",     SortKey = "InputWeight",        FilterType = "number", Width = "80",
            GroupKey = 2, GroupName = "实时关注" },
        new() { Key = "InputOutputRatio",   Label = "工单投料比",     SortKey = "InputOutputRatio",   FilterType = "number", Width = "80",
            GroupKey = 2, GroupName = "实时关注" },
        new() { Key = "InputStatus",        Label = "工单投料状态",   SortKey = "InputStatus",        FilterType = "enum", Width = "120",
            EnumOptions = DisplayHelper.GetFlowStatusOptions(), GroupKey = 2, GroupName = "实时关注" },

        // ========== 3 用料计划 ==========
        new() { Key = "OrderMaterialPlanStatus", Label = "订单-关联用料态", SortKey = "OrderMaterialPlanStatus", FilterType = "enum", Width = "120", GroupKey = 3, GroupName = "用料计划", Visible = false,
            EnumOptions = DisplayHelper.GetMaterialPlanStatusOptions(), Level = ColumnLevel.Order },
        new() { Key = "MainNoMaterialPlanStatus",Label = "主号-关联用料态", SortKey = "MainNoMaterialPlanStatus", FilterType = "enum", Width = "120", GroupKey = 3, GroupName = "用料计划",
            EnumOptions = DisplayHelper.GetMaterialPlanStatusOptions(), Level = ColumnLevel.MainNo },
        new() { Key = "MaterialPlanStatus",      Label = "工单用料计划",   SortKey = "MaterialPlanStatus", FilterType = "enum", Width = "120", GroupKey = 3, GroupName = "用料计划",
            EnumOptions = DisplayHelper.GetMaterialPlanStatusOptions() },
        new() { Key = "MaterialPlanRate",        Label = "工单满足率",     SortKey = "MaterialPlanRate", Width = "80", GroupKey = 3, GroupName = "用料计划" },
        new() { Key = "LatestPlanDate",          Label = "计划日期",       SortKey = "LatestPlanDate", FilterType = "date", Width = "120", GroupKey = 3, GroupName = "用料计划" },
        new() { Key = "PlanProportion",          Label = "分类用料占比",   SortKey = "MaterialPlanProportion", Width = "120", GroupKey = 3, GroupName = "用料计划" },
        new() { Key = "MaterialPlanCoveredCount",Label = "料态种数",      SortKey = "MaterialPlanCoveredCount", Width = "80", GroupKey = 3, GroupName = "用料计划", Visible = false },
        new() { Key = "LatestRequiredDate",      Label = "要求到货日",    SortKey = "LatestRequiredDate", FilterType = "date", Width = "120", GroupKey = 3, GroupName = "用料计划", Visible = false },
        new() { Key = "TheoreticalCutoffDate",  Label = "理论截止投料日", SortKey = "TheoreticalCutoffDate", FilterType = "date", Width = "120", GroupKey = 3, GroupName = "用料计划" },
        new() { Key = "MaxStandardCycle",       Label = "工单最大工艺周期", SortKey = "MaxStandardCycle", Width = "80", GroupKey = 3, GroupName = "用料计划", Visible = false },
        new() { Key = "MainNoMaxStandardCycle", Label = "主号最大工艺周期", SortKey = "MainNoMaxStandardCycle", Width = "80", GroupKey = 3, GroupName = "用料计划", Visible = false, Level = ColumnLevel.MainNo },
        new() { Key = "CapacityWorkDays",       Label = "主号产能工量",   SortKey = "CapacityWorkDays", Width = "80", GroupKey = 3, GroupName = "用料计划", Visible = false, Level = ColumnLevel.MainNo },
    };

    // ========== 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;
        var props = typeof(WorkOrderListDto)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);
        foreach (var col in _visibleColumns.Where(c => _summableColumnKeys.Contains(c.Key)))
        {
            if (!props.TryGetValue(col.Key, out var prop)) continue;
            var type = prop.PropertyType;
            try
            {
                if (type == typeof(int))
                {
                    var sum = _pageItems.Sum(item => (int)(prop.GetValue(item) ?? 0));
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal))
                {
                    var sum = _pageItems.Sum(item => (decimal)(prop.GetValue(item) ?? 0m));
                    _pageSums[col.Key] = ((int)sum).ToString();
                }
                else if (type == typeof(int?))
                {
                    var sum = _pageItems.Sum(item => (int?)(prop.GetValue(item)) ?? 0);
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal?))
                {
                    var sum = _pageItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
                    _pageSums[col.Key] = ((int)sum).ToString();
                }
            }
            catch { }
        }
    }

    private string RenderFooterCell(ColumnDef col)
    {
        if (_pageSums.TryGetValue(col.Key, out var sum)) return sum;
        return "-";
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<WorkOrderListDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        // 首次加载覆盖页码（MudTable 初始化时始终传 page=0）
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

        try
        {
            errorMessage = string.Empty;
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "CreatedTime";
            var filtersJson = SerializeFilters();
            var planTypeFilter = BuildPlanTypeFilter();

            var result = await WorkOrderService.GetPagedWithPlansAsync(
                pageIndex: state.Page + 1,
                pageSize: state.PageSize,
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy: sortBy,
                isDescending: sortDescending,
                filters: filtersJson,
                planTypeFilter: planTypeFilter,
                signDateFrom: DateTime.TryParse(_dateFrom, out var dFrom) ? dFrom : null,
                signDateTo: DateTime.TryParse(_dateTo, out var dTo) ? dTo : null,
                deliveryDateStart: DateTime.TryParse(_deliveryDateFrom, out var ddf) ? ddf : null,
                deliveryDateEnd: DateTime.TryParse(_deliveryDateTo, out var ddt) ? ddt : null,
                linkFilter: _linkFilter
            );

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<WorkOrderListDto> { Items = _pageItems, TotalItems = _totalCount };

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
                errorMessage = result?.Message ?? "查询失败";
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"加载异常: {ex.Message}";
            Snackbar.Add(errorMessage, Severity.Error);
            _pageItems = new();
            _totalCount = 0;
        }

        ComputePageSums();
        await SavePageStateAsync();

        return new TableData<WorkOrderListDto>
        {
            Items = _pageItems,
            TotalItems = _totalCount
        };
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

    private string? BuildPlanTypeFilter()
    {
        var planTypes = new List<string>();
        if (includePiercing) planTypes.Add("piercing");
        if (includeSemi) planTypes.Add("semi");
        if (includeFinish) planTypes.Add("finish");
        if (includeInventory) planTypes.Add("inventory");
        if (includeRework) planTypes.Add("rework");
        if (includeInProcessRework) planTypes.Add("inprocess");
        if (includeInMainWorkOrder) planTypes.Add("inmain");
        return planTypes.Count < 7 ? string.Join(",", planTypes) : null;
    }

    // ========== 筛选上下文加载（ExcelFilter 下拉选项） ==========

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await WorkOrderService.GetFilterContextsAsync();
            if (result.Success && result.Data != null)
            {
                _filterContextOptions.Clear();
                foreach (var kvp in result.Data)
                {
                    _filterContextOptions[kvp.Key] = kvp.Value.Select(v => new ExcelFilterOption
                    {
                        Value = v,
                        Display = kvp.Key switch
                        {
                            "UrgencyLevel" => DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, v) ?? v,
                            "RawMaterialLockRemark" => DictValueDisplayHelper.GetText(DictValueDefaults.RawMaterialLockRemarkKey, v) ?? v,
                            _ => v
                        },
                        Count = 0
                    }).ToList();
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
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载筛选上下文失败: {ex.Message}", Severity.Warning);
        }
    }

    // ========== ExcelFilter 事件 ==========

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }


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

    private async Task OnDeliveryDateFromChanged(string value)
    {
        _deliveryDateFrom = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnDeliveryDateToChanged(string value)
    {
        _deliveryDateTo = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 计划类型筛选 ==========

    private async Task OnPlanTypeFilterChangedAsync(bool value, int planType)
    {
        switch (planType)
        {
            case 1: includeSemi = value; break;
            case 2: includeFinish = value; break;
            case 3: includeInventory = value; break;
            case 4: includeRework = value; break;
            case 5: includePiercing = value; break;
            case 6: includeInProcessRework = value; break;
            case 7: includeInMainWorkOrder = value; break;
        }
        selectedWorkOrderIds.Clear();
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnUp(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnDown(ColumnDef col) => await SaveColumnPrefs();

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync(ColumnPrefsKey, null, _allColumns);
    }

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

        var saved = await ColumnPrefs.LoadAsync(ColumnPrefsKey, null);
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

        // 从 PageState 恢复排序/筛选状态
        var savedState = await PageState.LoadAsync("materialplan");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "CreatedTime";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _dateFrom = savedState.Extras?.ContainsKey("dateFrom") == true ? savedState.Extras["dateFrom"] ?? string.Empty : string.Empty;
            _dateTo = savedState.Extras?.ContainsKey("dateTo") == true ? savedState.Extras["dateTo"] ?? string.Empty : string.Empty;
            _deliveryDateFrom = savedState.Extras?.ContainsKey("deliveryDateFrom") == true ? savedState.Extras["deliveryDateFrom"] ?? string.Empty : string.Empty;
            _deliveryDateTo = savedState.Extras?.ContainsKey("deliveryDateTo") == true ? savedState.Extras["deliveryDateTo"] ?? string.Empty : string.Empty;
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
            // 恢复计划类型筛选状态
            if (savedState.Extras?.ContainsKey("includePiercing") == true)
                bool.TryParse(savedState.Extras["includePiercing"], out includePiercing);
            if (savedState.Extras?.ContainsKey("includeSemi") == true)
                bool.TryParse(savedState.Extras["includeSemi"], out includeSemi);
            if (savedState.Extras?.ContainsKey("includeFinish") == true)
                bool.TryParse(savedState.Extras["includeFinish"], out includeFinish);
            if (savedState.Extras?.ContainsKey("includeInventory") == true)
                bool.TryParse(savedState.Extras["includeInventory"], out includeInventory);
            if (savedState.Extras?.ContainsKey("includeRework") == true)
                bool.TryParse(savedState.Extras["includeRework"], out includeRework);
            if (savedState.Extras?.ContainsKey("includeInProcessRework") == true)
                bool.TryParse(savedState.Extras["includeInProcessRework"], out includeInProcessRework);
            if (savedState.Extras?.ContainsKey("includeInMainWorkOrder") == true)
                bool.TryParse(savedState.Extras["includeInMainWorkOrder"], out includeInMainWorkOrder);
            // 恢复卡片点击联动筛选（若存在，则联动时已覆盖并清空其他搜索/日期/列筛选，保持一致）
            if (savedState.Extras?.ContainsKey("linkFilter") == true)
            {
                try
                {
                    _linkFilter = JsonSerializer.Deserialize<MaterialPlanLinkFilterDto>(savedState.Extras["linkFilter"]);
                    if (_linkFilter != null)
                    {
                        var parts = new List<string>();
                        if (!string.IsNullOrEmpty(_linkFilter.Remark))
                            parts.Add(RawMaterialLockRemarkKeys.ToChinese(_linkFilter.Remark) ?? _linkFilter.Remark);
                        if (!string.IsNullOrEmpty(_linkFilter.Urgency))
                            parts.Add(UrgencyLevelKeys.ToChinese(_linkFilter.Urgency) ?? _linkFilter.Urgency);
                        _linkLabel = _linkFilter.PurchaseOnly
                            ? string.Join("·", parts) + "(成购)"
                            : string.Join("·", parts);
                    }
                }
                catch { }
            }
        }

        // 状态恢复后重新加载表格数据（首次渲染时 ServerData 可能已用默认值加载）
        if (savedState != null && table != null)
            await table.ReloadServerData();

        // 加载筛选上下文（ExcelFilter 下拉选项）
        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#material-plan-overview-list-table"))
                _isArrowNavSetup = false;
        }

        // 分组标题栏：测量实际列宽 + 同步滚动
        await JS.InvokeVoidAsync("initGroupHeaders", "#material-plan-overview-list-table");
    }

    // ========== 选中 ==========

    private void ToggleSelectWorkOrder(int id, bool selected)
    {
        if (selected)
            selectedWorkOrderIds.Add(id);
        else
            selectedWorkOrderIds.Remove(id);

        StateHasChanged();
    }

    // ========== 导航 ==========

    private void NavigateToMaterialPlan(int id)
    {
        Navigation.NavigateTo($"/workorders/{id}/material-plan");
    }

    // ========== 待投料量汇总卡片（复用原锁计划数据源） ==========

    private void ToggleSummaryCard()
    {
        _showSummaryCard = !_showSummaryCard;
        if (_showSummaryCard && _pendingSummary == null && !_summaryLoading)
            _ = LoadPendingSummaryAsync();
    }

    private async Task LoadPendingSummaryAsync()
    {
        _summaryLoading = true;
        try
        {
            var result = await RawMaterialLockPlanService.GetPendingSummaryAsync();
            if (result.Success && result.Data != null)
                _pendingSummary = result.Data;
            else
                Snackbar.Add(result?.Message ?? "获取待投料量汇总失败", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"获取待投料量汇总失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _summaryLoading = false;
            // fire-and-forget 加载（ToggleSummaryCard 里 _ = LoadPendingSummaryAsync()）完成后
            // 不会自动触发重新渲染，必须手动 StateHasChanged，否则卡片停留在「正在加载...」
            StateHasChanged();
        }
    }

    // ========== 错误疑问投料卡片（工单执行状况读模型） ==========

    private void ToggleErrorDoubtCard()
    {
        _showErrorDoubtCard = !_showErrorDoubtCard;
        if (_showErrorDoubtCard && _errorDoubtItems == null && !_errorDoubtLoading)
            _ = LoadErrorDoubtInputAsync();
    }

    private async Task LoadErrorDoubtInputAsync()
    {
        _errorDoubtLoading = true;
        try
        {
            var result = await WorkOrderService.GetErrorDoubtInputItemsAsync();
            if (result.Success && result.Data != null)
            {
                _errorDoubtItems = result.Data;
                // 重新加载后重置排序/筛选/行数，回到默认视图
                _errorDoubtSortKey = "WorkOrderNo";
                _errorDoubtSortDesc = false;
                _errorDoubtColumnFilters.Clear();
                _errorDoubtDisplayCount = 5;
            }
            else
                Snackbar.Add(result?.Message ?? "获取错误疑问投料失败", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"获取错误疑问投料失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _errorDoubtLoading = false;
            // fire-and-forget 加载完成后不会自动触发重新渲染，必须手动 StateHasChanged
            StateHasChanged();
        }
    }

    /// <summary>卡片重量列显示（kg，G29 去零；0 不显示防视觉污染）</summary>
    private static string FormatErrorDoubtWeight(decimal v) => v == 0 ? string.Empty : ((int)v).ToString("G29");

    /// <summary>到料实投一致性档位配色：2/3 疑问=橙黄，4/5 错误=红</summary>
    private static Color GetErrorDoubtConsistencyColor(int status) => status switch
    {
        2 or 3 => Color.Warning,
        4 or 5 => Color.Error,
        _ => Color.Default
    };

    /// <summary>打印「工单原锁-错疑投料」卡片明细：抓取隐藏完整表（绑定排序+筛选后全部行，不受显示行数截断影响）</summary>
    private async Task PrintErrorDoubtTable()
    {
        try
        {
            var table = await JS.InvokeAsync<string>("getTableHtml", "#mpol-error-doubt-print-table");
            if (string.IsNullOrEmpty(table))
            {
                Snackbar.Add("未找到可打印的错疑投料表格", Severity.Warning);
                return;
            }
            await JS.InvokeVoidAsync("printRawHtml", table, "用料计划总览-工单原锁-错疑投料");
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>卡片排序+筛选后的全部行（打印与计数用，不受显示行数截断影响）</summary>
    private List<ErrorDoubtInputItemDto> _errorDoubtSortedFiltered
    {
        get
        {
            if (_errorDoubtItems == null) return new();
            IEnumerable<ErrorDoubtInputItemDto> q = _errorDoubtItems;
            // 逐列筛选（ExcelFilter 多选，值与组件下拉选项一致）
            foreach (var kvp in _errorDoubtColumnFilters)
            {
                var vals = kvp.Value;
                q = q.Where(x => vals.Contains(ErrorDoubtFilterValue(x, kvp.Key) ?? _errorDoubtNullFilter));
            }
            return SortErrorDoubt(q);
        }
    }

    /// <summary>卡片显示行（排序+筛选后截断，默认 10 行防视觉污染）</summary>
    private List<ErrorDoubtInputItemDto> _errorDoubtVisibleItems =>
        _errorDoubtSortedFiltered.Take(_errorDoubtDisplayCount).ToList();

    /// <summary>卡片列头点击切换排序（同列再点切换升/降，异列重置为升序）</summary>
    private void ToggleErrorDoubtSort(string key)
    {
        if (_errorDoubtSortKey == key)
            _errorDoubtSortDesc = !_errorDoubtSortDesc;
        else
        {
            _errorDoubtSortKey = key;
            _errorDoubtSortDesc = false;
        }
    }

    /// <summary>ExcelFilter 逐列筛选变更：更新筛选字典，派生集合自动重算</summary>
    private void OnErrorDoubtFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _errorDoubtColumnFilters[fieldKey] = selectedValues;
        else
            _errorDoubtColumnFilters.Remove(fieldKey);
    }

    /// <summary>卡片 12 列筛选值提取（与 ExcelFilter 下拉选项 Value 一致；null 用占位符匹配「(空值)」选项）</summary>
    private static string? ErrorDoubtFilterValue(ErrorDoubtInputItemDto x, string key) => key switch
    {
        "WorkOrderNo" => x.WorkOrderNo,
        "SalesOrderNo" => x.SalesOrderNo,
        "ProductionMainNo" => x.ProductionMainNo,
        "PlantGrade" => x.PlantGrade,
        "Specification" => x.Specification,
        "TotalWeight" => ((int)x.TotalWeight).ToString("G29"),
        "TotalPlanWeight" => ((int)x.TotalPlanWeight).ToString("G29"),
        "CutoffArrivalDate" => x.CutoffArrivalDate?.ToString("yyyy-MM-dd"),
        "TotalAvailableWeight" => ((int)x.TotalAvailableWeight).ToString("G29"),
        "ActualInputWeight" => ((int)x.ActualInputWeight).ToString("G29"),
        "PlanInputConsistency" => x.PlanInputConsistency.ToString(),
        "TotalMissingWeight" => ((int)x.TotalMissingWeight).ToString("G29"),
        _ => x.WorkOrderNo
    };

    /// <summary>卡片内存排序：数值列按 decimal、日期列按 DateTime、档位列按 int、其余文本按字符串（升序空值排前可接受）</summary>
    private List<ErrorDoubtInputItemDto> SortErrorDoubt(IEnumerable<ErrorDoubtInputItemDto> q)
    {
        var key = _errorDoubtSortKey;
        return key switch
        {
            "TotalWeight" or "TotalPlanWeight" or "TotalAvailableWeight" or "ActualInputWeight" or "TotalMissingWeight" =>
                _errorDoubtSortDesc
                    ? q.OrderByDescending(x => ErrorDoubtNumericValue(x, key)).ToList()
                    : q.OrderBy(x => ErrorDoubtNumericValue(x, key)).ToList(),
            "CutoffArrivalDate" =>
                _errorDoubtSortDesc
                    ? q.OrderByDescending(x => x.CutoffArrivalDate).ThenByDescending(x => x.WorkOrderNo).ToList()
                    : q.OrderBy(x => x.CutoffArrivalDate).ThenBy(x => x.WorkOrderNo).ToList(),
            "PlanInputConsistency" =>
                _errorDoubtSortDesc
                    ? q.OrderByDescending(x => x.PlanInputConsistency).ToList()
                    : q.OrderBy(x => x.PlanInputConsistency).ToList(),
            _ =>
                _errorDoubtSortDesc
                    ? q.OrderByDescending(x => ErrorDoubtTextValue(x, key), StringComparer.OrdinalIgnoreCase).ToList()
                    : q.OrderBy(x => ErrorDoubtTextValue(x, key), StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static decimal ErrorDoubtNumericValue(ErrorDoubtInputItemDto x, string key) => key switch
    {
        "TotalWeight" => x.TotalWeight,
        "TotalPlanWeight" => x.TotalPlanWeight,
        "TotalAvailableWeight" => x.TotalAvailableWeight,
        "ActualInputWeight" => x.ActualInputWeight,
        "TotalMissingWeight" => x.TotalMissingWeight,
        _ => 0m
    };

    private static string? ErrorDoubtTextValue(ErrorDoubtInputItemDto x, string key) => key switch
    {
        "WorkOrderNo" => x.WorkOrderNo,
        "SalesOrderNo" => x.SalesOrderNo,
        "ProductionMainNo" => x.ProductionMainNo,
        "PlantGrade" => x.PlantGrade,
        "Specification" => x.Specification,
        _ => x.WorkOrderNo
    };

    /// <summary>打印「待投料量汇总」卡片（前端 printRawHtml 打印待投料矩阵 + 成购矩阵两个 DOM 表格，不含截日）</summary>
    private async Task PrintSummaryTable()
    {
        try
        {
            var pending = await JS.InvokeAsync<string>("getTableHtml", "#mpol-summary-pending");
            var purchase = await JS.InvokeAsync<string>("getTableHtml", "#mpol-summary-purchase");
            if (string.IsNullOrEmpty(pending))
            {
                Snackbar.Add("未找到可打印的汇总表格", Severity.Warning);
                return;
            }
            var html = "<div style=\"font-weight:600; margin-bottom:4px;\">待投料</div>" + pending;
            if (!string.IsNullOrEmpty(purchase))
                html += "<div style=\"font-weight:600; margin:10px 0 4px;\">成购（外购成品）</div>" + purchase;
            await JS.InvokeVoidAsync("printRawHtml", html, "用料计划总览-待投料量汇总");
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    // 待投料矩阵（单数 + 待投料吨）
    private static string FormatMatrixPending(int count, decimal weight)
        => count > 0 ? $"{count} 单 / {weight / 1000m:F1}吨" : "-";
    private static string FormatMatrixPurchase(int count, decimal weight)
        => count > 0 ? $"{count} 单 / {weight / 1000m:F1}吨" : "-";

    // ========== 卡片点击联动筛选（仿订单首页小表点击，覆盖式 + 提示条） ==========

    /// <summary>矩阵单元格/行/列点击：按备注×计划性联动筛选下方工单列表（严格限定原料锁定，后端 linkFilter 生效）。
    /// purchaseOnly=true 为成购矩阵联动（「包含」口径）；excludeSingleFinishPurchase=true 为待投料矩阵联动（排除「单一成品采购」工单）</summary>
    private async Task ApplyMatrixLink(string? remarkKey, string? urgencyKey, bool purchaseOnly = false, bool excludeSingleFinishPurchase = false)
    {
        if (remarkKey == null && urgencyKey == null) return;

        _linkFilter = new MaterialPlanLinkFilterDto
        {
            Remark = remarkKey,
            Urgency = urgencyKey,
            PurchaseOnly = purchaseOnly,
            ExcludeSingleFinishPurchase = excludeSingleFinishPurchase,
        };

        // 提示条文案：用卡片显示中文（矩阵标签），避免重复查字典；成购联动附加「成购缺口」标识
        var parts = new List<string>();
        if (remarkKey != null)
        {
            var ri = Array.IndexOf(_matrixRemarkKeys, remarkKey);
            parts.Add(_pendingSummary != null && ri >= 0 && ri < _pendingSummary.MatrixRowLabels.Count
                ? _pendingSummary.MatrixRowLabels[ri]
                : (RawMaterialLockRemarkKeys.ToChinese(remarkKey) ?? remarkKey));
        }
        if (urgencyKey != null)
        {
            var ci = Array.IndexOf(_matrixUrgencyKeys, urgencyKey);
            parts.Add(_pendingSummary != null && ci >= 0 && ci < _pendingSummary.MatrixColumnLabels.Count
                ? _pendingSummary.MatrixColumnLabels[ci]
                : (UrgencyLevelKeys.ToChinese(urgencyKey) ?? urgencyKey));
        }
        _linkLabel = purchaseOnly
            ? string.Join("·", parts) + "(成购)"
            : string.Join("·", parts);

        // 覆盖现有搜索/签订日期/交货日期/列筛选/计划类型筛选
        _searchKeyword = string.Empty;
        _dateFrom = string.Empty;
        _dateTo = string.Empty;
        _deliveryDateFrom = string.Empty;
        _deliveryDateTo = string.Empty;
        _columnFilters.Clear();
        includeSemi = includeFinish = includeInventory = includeRework = includePiercing = includeInProcessRework = includeInMainWorkOrder = true;
        _resetToFirstPage = true;

        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
        Snackbar.Add($"已按「{_linkLabel}」联动筛选工单列表", Severity.Info);
    }

    /// <summary>点击矩阵行（备注）：该行有数据才联动（待投料口径：排除「单一成品采购」工单）</summary>
    private async Task OnMatrixRowClick(int remarkIndex)
    {
        if (_pendingSummary == null || remarkIndex < 0 || remarkIndex >= _pendingSummary.MatrixRows.Count) return;
        if (_pendingSummary.MatrixRows[remarkIndex].RowCount <= 0) return;
        await ApplyMatrixLink(_matrixRemarkKeys[remarkIndex], null, excludeSingleFinishPurchase: true);
    }

    /// <summary>点击矩阵列（计划性）：该列有数据才联动（待投料口径：排除「单一成品采购」工单）</summary>
    private async Task OnMatrixColumnClick(int urgencyIndex)
    {
        if (_pendingSummary == null || urgencyIndex < 0 || urgencyIndex >= _pendingSummary.MatrixColumnTotals.Count) return;
        if (_pendingSummary.MatrixColumnTotals[urgencyIndex].Count <= 0) return;
        await ApplyMatrixLink(null, _matrixUrgencyKeys[urgencyIndex], excludeSingleFinishPurchase: true);
    }

    /// <summary>点击矩阵单元格（备注×计划性）：该单元格有数据才联动（待投料口径：排除「单一成品采购」工单）</summary>
    private async Task OnMatrixCellClick(int remarkIndex, int urgencyIndex)
    {
        if (_pendingSummary == null || remarkIndex < 0 || remarkIndex >= _pendingSummary.MatrixRows.Count) return;
        var cell = _pendingSummary.MatrixRows[remarkIndex].Cells.ElementAtOrDefault(urgencyIndex);
        if (cell == null || (cell.Count <= 0 && cell.PendingWeight <= 0 && cell.PurchaseCount <= 0 && cell.PurchaseWeight <= 0)) return;
        await ApplyMatrixLink(_matrixRemarkKeys[remarkIndex], _matrixUrgencyKeys[urgencyIndex], excludeSingleFinishPurchase: true);
    }

    // ========== 成购（外购成品）矩阵联动：语义=按「包含」口径筛成品采购计划量 > 0 的工单（FinishPlanWeight > 0，含单一成品采购），与待投料联动共用备注/计划性，但附加成购包含条件 ==========

    /// <summary>点击成购矩阵行（备注）：该行有成购数据才联动</summary>
    private async Task OnPurchaseMatrixRowClick(int remarkIndex)
    {
        if (_pendingSummary == null || remarkIndex < 0 || remarkIndex >= _pendingSummary.MatrixRows.Count) return;
        if (_pendingSummary.MatrixRows[remarkIndex].RowPurchaseCount <= 0) return;
        await ApplyMatrixLink(_matrixRemarkKeys[remarkIndex], null, purchaseOnly: true);
    }

    /// <summary>点击成购矩阵列（计划性）：该列有成购数据才联动</summary>
    private async Task OnPurchaseMatrixColumnClick(int urgencyIndex)
    {
        if (_pendingSummary == null || urgencyIndex < 0 || urgencyIndex >= _pendingSummary.MatrixColumnTotals.Count) return;
        if (_pendingSummary.MatrixColumnTotals[urgencyIndex].PurchaseCount <= 0) return;
        await ApplyMatrixLink(null, _matrixUrgencyKeys[urgencyIndex], purchaseOnly: true);
    }

    /// <summary>点击成购矩阵单元格（备注×计划性）：该单元格有成购数据才联动</summary>
    private async Task OnPurchaseMatrixCellClick(int remarkIndex, int urgencyIndex)
    {
        if (_pendingSummary == null || remarkIndex < 0 || remarkIndex >= _pendingSummary.MatrixRows.Count) return;
        var cell = _pendingSummary.MatrixRows[remarkIndex].Cells.ElementAtOrDefault(urgencyIndex);
        if (cell == null || cell.PurchaseCount <= 0) return;
        await ApplyMatrixLink(_matrixRemarkKeys[remarkIndex], _matrixUrgencyKeys[urgencyIndex], purchaseOnly: true);
    }

    /// <summary>清除联动筛选，恢复全量列表</summary>
    private async Task ClearLinkFilter()
    {
        _linkFilter = null;
        _linkLabel = null;
        _resetToFirstPage = true;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 辅助方法 ==========

    private Color GetStatusColor(MaterialPlanStatus status)
    {
        return status switch
        {
            MaterialPlanStatus.NotPlanned => Color.Default,
            MaterialPlanStatus.Partial => Color.Warning,
            MaterialPlanStatus.Satisfied => Color.Success,
            MaterialPlanStatus.Excess => Color.Default,
            _ => Color.Default
        };
    }

    private string GetStatusText(MaterialPlanStatus status) => DisplayHelper.GetMaterialPlanStatusText(status);

    // "超量"采用深色底白字 Chip（其余档位默认色）
    private static string GetStatusChipClass(MaterialPlanStatus status)
        => status == MaterialPlanStatus.Excess ? "chip-dark" : "";

    // ========== 单元格原始值/显示值 ==========

    private string? GetCellRawValue(WorkOrderListDto item, string key) => key switch
    {
        // 各列显示口径与 RenderCell 一致（0 值防视觉污染显示 "-"、枚举/字典走中文、周期带「天」）
        "TotalMissingWeight" => item.TotalMissingWeight.HasValue && item.TotalMissingWeight.Value > 0 ? ((int)item.TotalMissingWeight.Value).ToString() : "-",
        "WorkOrderNo" => item.WorkOrderNo,
        "SalesOrderNo" => item.SalesOrderNo,
        "ProductionMainNo" => item.ProductionMainNo,
        "ProductionSubNo" => item.ProductionSubNo,
        "SignDate" => item.SignDate.ToString("yyyy-MM-dd"),
        "Salesman" => item.Salesman,
        "EndCustomer" => item.EndCustomer,
        "DeliveryDate" => item.DeliveryDate.ToString("yyyy-MM-dd"),
        "DelayPenalty" => DisplayHelper.GetYesNoText(item.DelayPenalty),
        "SettlementMethod" => DisplayHelper.GetSettlementMethodText(item.SettlementMethod),
        "PlantGrade" => item.PlantGrade,
        "Specification" => item.Specification,
        "MaterialName" => DisplayHelper.GetPipeManufacturingTypeText(item.PipeManufacturingType),
        "LengthStatus" => DisplayHelper.GetWorkOrderLengthStatusText(item.LengthStatus, item.MinLength, item.MaxLength),
        "MaxLength" => item.MaxLength?.ToString("G29"),
        "MinLength" => item.MinLength?.ToString("G29"),
        "TotalQuantity" => item.TotalQuantity.ToString("G29"),
        "TotalWeight" => ((int)item.TotalWeight).ToString(),
        "DeliveryState" => DisplayHelper.GetDeliveryStateText(item.DeliveryState),
        "TotalItemCount" => item.TotalItemCount.ToString("G29"),
        "ScheduleStage" => item.ScheduleStage.HasValue ? IntStatusDisplayHelper.GetScheduleStageText(item.ScheduleStage) : "-",
        "UrgencyLevel" => string.IsNullOrEmpty(item.UrgencyLevel) ? "-" : (DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, item.UrgencyLevel) ?? "-"),
        "RawMaterialLockRemark" => DictValueDisplayHelper.GetText(DictValueDefaults.RawMaterialLockRemarkKey, item.RawMaterialLockRemark) ?? "-",
        "PendingInputWeight" => item.PendingInputWeight.HasValue && item.PendingInputWeight.Value > 0 ? ((int)item.PendingInputWeight.Value).ToString() : "-",
        "InputWeight" => item.InputWeight.HasValue && item.InputWeight.Value > 0 ? ((int)item.InputWeight.Value).ToString() : "-",
        "InputOutputRatio" => item.InputOutputRatio.HasValue && item.InputOutputRatio.Value > 0 ? $"{item.InputOutputRatio.Value:F1}%" : "-",
        "InputStatus" => item.InputStatus.HasValue ? IntStatusDisplayHelper.GetInputStatusText(item.InputStatus.Value) : "-",
        "LatestPlanDate" => item.LatestPlanDate?.ToString("yyyy-MM-dd"),
        "MaterialPlanStatus" => DisplayHelper.GetMaterialPlanStatusText(item.MaterialPlanStatus),
        "MaterialPlanRate" => $"{item.MaterialPlanRate:F1}%",
        "PlanProportion" => string.IsNullOrEmpty(item.PlanProportionText) ? "-" : item.PlanProportionText,
        "MainNoMaterialPlanStatus" => GetStatusText(item.MainNoMaterialPlanStatus),
        "OrderMaterialPlanStatus" => GetStatusText(item.OrderMaterialPlanStatus),
        "MaterialPlanCoveredCount" => item.MaterialPlanCoveredCount > 0 ? $"{item.MaterialPlanCoveredCount}/7" : "-",
        "MaxStandardCycle" => item.MaxStandardCycle > 0 ? $"{item.MaxStandardCycle}天" : "-",
        "LatestRequiredDate" => item.LatestRequiredDate?.ToString("yyyy-MM-dd"),
        "MainNoMaxStandardCycle" => item.MainNoMaxStandardCycle > 0 ? $"{item.MainNoMaxStandardCycle}天" : "-",
        "CapacityWorkDays" => item.CapacityWorkDays.HasValue ? $"{item.CapacityWorkDays}天" : "-",
        "TheoreticalCutoffDate" => item.TheoreticalCutoffDate?.ToString("yyyy-MM-dd"),
        _ => null
    };

    private string? GetCellDisplayText(WorkOrderListDto item, string key) => key switch
    {
        "DelayPenalty" => DisplayHelper.GetYesNoText(item.DelayPenalty),
        "SettlementMethod" => DisplayHelper.GetSettlementMethodText(item.SettlementMethod),
        "MaterialName" => DisplayHelper.GetPipeManufacturingTypeText(item.PipeManufacturingType),
        "LengthStatus" => DisplayHelper.GetWorkOrderLengthStatusText(item.LengthStatus, item.MinLength, item.MaxLength),
        "DeliveryState" => DisplayHelper.GetDeliveryStateText(item.DeliveryState),
        "MaterialPlanStatus" => DisplayHelper.GetMaterialPlanStatusText(item.MaterialPlanStatus),
        "MainNoMaterialPlanStatus" => GetStatusText(item.MainNoMaterialPlanStatus),
        "OrderMaterialPlanStatus" => GetStatusText(item.OrderMaterialPlanStatus),
        _ => GetCellRawValue(item, key) ?? ""
    };

    // ========== 分组标题栏 ==========

    private class GroupHeaderInfo
    {
        public int GroupKey { get; init; }
        public string GroupName { get; init; } = "";
        public int TotalWidth { get; init; }
        public int ColumnCount { get; init; }
        public string CssClass { get; init; } = "";
    }

    private List<GroupHeaderInfo> GetGroupHeaders()
    {
        var result = new List<GroupHeaderInfo>();

        // 选择列占位（40px），对齐表格最左侧的 checkbox 列
        result.Add(new GroupHeaderInfo
        {
            GroupKey = 0,
            GroupName = "",
            TotalWidth = 40,
            ColumnCount = 0,
            CssClass = ""
        });

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
        return result;
    }

    // ========== 分组 CSS class ==========

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1",
            2 => "col-g2",
            3 => "col-g3",
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
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(WorkOrderListDto wo, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "WorkOrderNo":
                builder.OpenComponent<MudLink>(0);
                builder.AddAttribute(1, "Typo", Typo.body2);
                builder.AddAttribute(2, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => NavigateToMaterialPlan(wo.Id)));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, wo.WorkOrderNo)));
                builder.CloseComponent();
                break;
            case "SalesOrderNo":
                builder.AddContent(0, wo.SalesOrderNo);
                break;
            case "ProductionMainNo":
                builder.AddContent(0, wo.ProductionMainNo);
                break;
            case "ProductionSubNo":
                builder.AddContent(0, wo.ProductionSubNo);
                break;
            case "SignDate":
                builder.AddContent(0, wo.SignDate.ToString("yyyy-MM-dd"));
                break;
            case "Salesman":
                builder.AddContent(0, wo.Salesman);
                break;
            case "EndCustomer":
                builder.AddContent(0, wo.EndCustomer ?? "-");
                break;
            case "DeliveryDate":
                builder.AddContent(0, wo.DeliveryDate.ToString("yyyy-MM-dd"));
                break;
            case "DelayPenalty":
                builder.AddContent(0, DisplayHelper.GetYesNoText(wo.DelayPenalty));
                break;
            case "SettlementMethod":
                builder.AddContent(0, DisplayHelper.GetSettlementMethodText(wo.SettlementMethod));
                break;
            case "PlantGrade":
                builder.AddContent(0, wo.PlantGrade);
                break;
            case "Specification":
                builder.AddContent(0, wo.Specification);
                break;
            case "MaterialName":
                builder.AddContent(0, DisplayHelper.GetPipeManufacturingTypeText(wo.PipeManufacturingType));
                break;
            case "LengthStatus":
                builder.AddContent(0, DisplayHelper.GetWorkOrderLengthStatusText(wo.LengthStatus, wo.MinLength, wo.MaxLength));
                break;
            case "MaxLength":
                builder.AddContent(0, wo.MaxLength?.ToString("G29") ?? "-");
                break;
            case "MinLength":
                builder.AddContent(0, wo.MinLength?.ToString("G29") ?? "-");
                break;
            case "TotalQuantity":
                builder.AddContent(0, wo.TotalQuantity);
                break;
            case "TotalWeight":
                builder.AddContent(0, ((int)wo.TotalWeight).ToString());
                break;
            case "DeliveryState":
                builder.AddContent(0, DisplayHelper.GetDeliveryStateText(wo.DeliveryState));
                break;
            case "TotalItemCount":
                builder.AddContent(0, wo.TotalItemCount);
                break;
            case "ScheduleStage":
                if (wo.ScheduleStage.HasValue)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", DisplayHelper.GetScheduleStageColor(wo.ScheduleStage.Value));
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, IntStatusDisplayHelper.GetScheduleStageText(wo.ScheduleStage))));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;
            case "UrgencyLevel":
                if (!string.IsNullOrEmpty(wo.UrgencyLevel))
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", DisplayHelper.GetUrgencyColor(wo.UrgencyLevel));
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, wo.UrgencyLevel))));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;
            case "RawMaterialLockRemark":
                builder.AddContent(0, DictValueDisplayHelper.GetText(DictValueDefaults.RawMaterialLockRemarkKey, wo.RawMaterialLockRemark) ?? "-");
                break;
            case "TotalMissingWeight":
                // 理论原料未至（理论缺失总料重）：0 默认不显示（防视觉污染），>0 黑色 Chip（计划缺口缺料）
                if (wo.TotalMissingWeight.HasValue && wo.TotalMissingWeight.Value > 0)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Default);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, ((int)wo.TotalMissingWeight.Value).ToString())));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;
            case "PendingInputWeight":
                // 0 默认不显示（防视觉污染），>0 用「工单用料计划-超量」样式（chip-dark 深色底白字）
                if (wo.PendingInputWeight.HasValue && wo.PendingInputWeight.Value > 0)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Class", "chip-dark");
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, ((int)wo.PendingInputWeight.Value).ToString())));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;
            case "InputWeight":
                // 0 默认不显示（防视觉污染）
                builder.AddContent(0, wo.InputWeight.HasValue && wo.InputWeight.Value > 0 ? ((int)wo.InputWeight.Value).ToString() : "-");
                break;
            case "InputOutputRatio":
                builder.AddContent(0, wo.InputOutputRatio.HasValue && wo.InputOutputRatio.Value > 0 ? $"{wo.InputOutputRatio.Value:F1}%" : "-");
                break;
            case "InputStatus":
                if (wo.InputStatus.HasValue)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", DisplayHelper.GetInputStatusColor(wo.InputStatus.Value));
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, IntStatusDisplayHelper.GetInputStatusText(wo.InputStatus.Value))));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;
            case "LatestPlanDate":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", Color.Info);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, wo.LatestPlanDate?.ToString("yyyy-MM-dd") ?? "-")));
                builder.CloseComponent();
                break;
            case "MaterialPlanStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetStatusColor(wo.MaterialPlanStatus));
                builder.AddAttribute(3, "Class", GetStatusChipClass(wo.MaterialPlanStatus));
                builder.AddAttribute(4, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetStatusText(wo.MaterialPlanStatus))));
                builder.CloseComponent();
                break;
            case "MaterialPlanRate":
                builder.AddContent(0, $"{wo.MaterialPlanRate:F1}%");
                break;
            case "PlanProportion":
                if (!string.IsNullOrEmpty(wo.PlanProportionText))
                {
                    builder.OpenComponent<MudText>(0);
                    builder.AddAttribute(1, "Typo", Typo.caption);
                    builder.AddAttribute(2, "Class", "text-wrap");
                    builder.AddAttribute(3, "Style", "max-width:180px;");
                    builder.AddAttribute(4, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, wo.PlanProportionText)));
                    builder.CloseComponent();
                }
                else
                {
                    builder.OpenComponent<MudText>(0);
                    builder.AddAttribute(1, "Typo", Typo.caption);
                    builder.AddAttribute(2, "Color", Color.Secondary);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "-")));
                    builder.CloseComponent();
                }
                break;
            case "MainNoMaterialPlanStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetStatusColor(wo.MainNoMaterialPlanStatus));
                builder.AddAttribute(3, "Class", GetStatusChipClass(wo.MainNoMaterialPlanStatus));
                builder.AddAttribute(4, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetStatusText(wo.MainNoMaterialPlanStatus))));
                builder.CloseComponent();
                break;
            case "OrderMaterialPlanStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetStatusColor(wo.OrderMaterialPlanStatus));
                builder.AddAttribute(3, "Class", GetStatusChipClass(wo.OrderMaterialPlanStatus));
                builder.AddAttribute(4, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetStatusText(wo.OrderMaterialPlanStatus))));
                builder.CloseComponent();
                break;
            case "MaxStandardCycle":
                builder.AddContent(0, wo.MaxStandardCycle > 0 ? $"{wo.MaxStandardCycle}天" : "-");
                break;
            case "MainNoMaxStandardCycle":
                builder.AddContent(0, wo.MainNoMaxStandardCycle > 0 ? $"{wo.MainNoMaxStandardCycle}天" : "-");
                break;
            case "CapacityWorkDays":
                builder.AddContent(0, wo.CapacityWorkDays.HasValue ? $"{wo.CapacityWorkDays}天" : "-");
                break;
            case "TheoreticalCutoffDate":
                if (wo.TheoreticalCutoffDate.HasValue)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Warning);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, wo.TheoreticalCutoffDate.Value.ToString("yyyy-MM-dd"))));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;
            case "MaterialPlanCoveredCount":
                builder.AddContent(0, wo.MaterialPlanCoveredCount > 0 ? $"{wo.MaterialPlanCoveredCount}/7" : "-");
                break;
            case "LatestRequiredDate":
                if (wo.LatestRequiredDate.HasValue)
                    builder.AddContent(0, wo.LatestRequiredDate.Value.ToString("yyyy-MM-dd"));
                else
                    builder.AddContent(0, "-");
                break;
        }
    };

    // ========== 批量打印 ==========

    /// <summary>打印选中列表（按当前可见列渲染列表 PDF，Mode A 前端已准备数据，复用工单列表打印端点）</summary>
    private async Task PrintSelectedList()
    {
        if (!selectedWorkOrderIds.Any())
        {
            Snackbar.Add("请先选择要打印的工单", Severity.Warning);
            return;
        }

        // 列过多时各列被压缩到单字符放不下的宽度 → QuestPDF 布局冲突；A4 可显示列数上限 35 列（与后端 TablePrintHelper.MaxPrintColumns 同步），超限提前拦截并页面内警示
        const int MaxPrintColumns = 35;
        if (_visibleColumns.Count > MaxPrintColumns)
        {
            Snackbar.Add($"当前可见列过多（{_visibleColumns.Count} 列，打印上限 {MaxPrintColumns} 列），请通过列显隐精简后再打印", Severity.Warning);
            return;
        }

        try
        {
            var selectedItems = _pageItems
                .Where(i => selectedWorkOrderIds.Contains(i.Id))
                .Select(item =>
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var col in _visibleColumns)
                        dict[col.Key] = GetCellDisplayText(item, col.Key) ?? "";
                    return dict;
                }).ToList();

            var request = new WorkOrderPrintListRequest
            {
                Title = "用料计划总览-工单列表",
                Items = selectedItems,
                Columns = _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList()
            };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.WorkOrder}/order-print-list-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>打印选中计划（按用料计划类型勾选生成计划 PDF）</summary>
    private async Task PrintSelectedPlans()
    {
        if (!selectedWorkOrderIds.Any() || !anyPlanTypeSelected) return;

        var request = new MaterialPlanBatchPrintRequest
        {
            WorkOrderIds = selectedWorkOrderIds.ToArray(),
            IncludeSemi = includeSemi,
            IncludeFinish = includeFinish,
            IncludeInventory = includeInventory,
            IncludeRework = includeRework,
            IncludeRoundBarPiercing = includePiercing,
            IncludeInProcessRework = includeInProcessRework,
            IncludeInMainWorkOrder = includeInMainWorkOrder
        };

        try
        {
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.MaterialPlan}/print/batch";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
        if (!string.IsNullOrWhiteSpace(_dateFrom)) extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrWhiteSpace(_dateTo)) extras["dateTo"] = _dateTo;
        if (!string.IsNullOrWhiteSpace(_deliveryDateFrom)) extras["deliveryDateFrom"] = _deliveryDateFrom;
        if (!string.IsNullOrWhiteSpace(_deliveryDateTo)) extras["deliveryDateTo"] = _deliveryDateTo;
        extras["includePiercing"] = includePiercing.ToString();
        extras["includeSemi"] = includeSemi.ToString();
        extras["includeFinish"] = includeFinish.ToString();
        extras["includeInventory"] = includeInventory.ToString();
        extras["includeRework"] = includeRework.ToString();
        extras["includeInProcessRework"] = includeInProcessRework.ToString();
        extras["includeInMainWorkOrder"] = includeInMainWorkOrder.ToString();
        if (_linkFilter != null)
            extras["linkFilter"] = JsonSerializer.Serialize(_linkFilter);
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage,
            Extras = extras
        };
        await PageState.SaveAsync("materialplan", state);
    }
}
