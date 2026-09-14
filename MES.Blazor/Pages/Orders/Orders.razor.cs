using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
using MES.Core.DTOs.Order;
using MES.Core.DTOs.Shared;
using System.Text.Json;
using System.Globalization;
using MES.Shared.Constants;

namespace MES.Blazor.Pages.Orders;

public partial class Orders
{
    private MudTable<SalesOrderListDto>? table;
    private List<SalesOrderListDto> _pageItems = new();
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new() { "TotalContractWeight", "ItemCount", "FinishedInboundWeight", "FinishedOutboundWeight", "FinishedStockWeight" };
    private int _totalCount;
    private HashSet<int> selectedOrderIds = new();
    private bool _isArrowNavSetup;
    private int _currentPage = 1;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;
    private string _deliveryDateFrom = string.Empty;
    private string _deliveryDateTo = string.Empty;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _loadVersion;
    private bool _resetToFirstPage;

    // ========== 完成预估及延期风险（卡片仅展示两张交期预估小表） ==========
    private bool _showEstimateCard;
    /// <summary>订单交期预估（两小表：订单(整单)完成预估 / 风险-已延期订单(整单)，x单/y吨，订单级口径）</summary>
    private OrderDeliveryEstimateDto? _deliveryEstimate;

    // ========== 投料产出总况（卡片：按订单完成月聚合的投料 / 产出 / 退货） ==========
    private bool _showThroughputCard;
    /// <summary>投料产出总况（默认近 12 个月按完成月分行；口径随 <see cref="_throughputScope"/>、日期区间随起/止切换）</summary>
    private OrderThroughputSummaryDto? _throughput;
    /// <summary>生产类型范围口径（默认「全部四种」：荒管 + 在制 + 库存 + 外购）</summary>
    private string _throughputScope = ProductionScopeKeys.All;
    /// <summary>完成日期范围-起（yyyy-MM-dd；与止同时为空 = 默认最近 12 个月）</summary>
    private string _throughputDateFrom = string.Empty;
    /// <summary>完成日期范围-止（yyyy-MM-dd；含当天）</summary>
    private string _throughputDateTo = string.Empty;

    /// <summary>口径下拉选项（合计 2 档 + 单一生产类型 4 档）</summary>
    private static readonly (string Key, string Label)[] _throughputScopeOptions =
    [
        (ProductionScopeKeys.All, "全部（荒管+在制+库存+外购）"),
        (ProductionScopeKeys.Pure, "纯生产（荒管+在制）"),
        (ProductionTypeKeys.RoughTube, "荒管生产"),
        (ProductionTypeKeys.InProcess, "在制生产"),
        (ProductionTypeKeys.Inventory, "库存料生产"),
        (ProductionTypeKeys.OutsourcedPurchased, "外购生产"),
    ];

    // ========== 小表点击联动筛选订单列表 ==========
    /// <summary>小表点击联动筛选条件（null=未联动），点击后覆盖现有搜索/列筛选</summary>
    private OrderDeliveryEstimateFilterDto? _estimateLinkFilter;
    /// <summary>联动提示条文案（如「风险-已延期订单(整单)·≤26/8/26」）</summary>
    private string? _estimateLinkLabel;

    private string sortColumn = "signdate";
    private bool sortDescending = true;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    // 列偏好版本键：变更默认显隐/分组后递增，强制老用户按新默认重新加载（col_prefs_orders_v2）
    private const string ColumnPrefsVersion = "v3";
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    // ========== 数值列（数据格居中） ==========
    private static readonly HashSet<string> _centerColumnKeys = new(StringComparer.Ordinal)
    {
        "TotalContractWeight", "ItemCount",
        "FinishedInboundWeight", "FinishedOutboundWeight", "FinishedStockWeight"
    };
    private static bool IsNumericColumn(ColumnDef col) => _centerColumnKeys.Contains(col.Key);

    // ========== B23 分组列标题栏 ==========
    private int _totalTableWidth =>
        40 + _visibleColumns.Sum(c => int.TryParse(c.Width, out var w) ? w : 100) + 150;

    private List<GroupHeaderInfo> _groupHeaders => GetGroupHeaders();

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

        // 选择列占位（40px）
        result.Add(new GroupHeaderInfo
        {
            GroupKey = 0,
            GroupName = "",
            TotalWidth = 40,
            ColumnCount = 0,
            CssClass = ""
        });

        int? lastKey = null; int totalWidth = 0;
        var groupKey = 0; var groupName = ""; var count = 0;
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
                totalWidth = 0; count = 0;
            }
            groupKey = gk; groupName = col.GroupName ?? "";
            totalWidth += int.TryParse(col.Width, out var w) ? w : 100;
            count++; lastKey = gk;
        }
        if (count > 0)
            result.Add(new GroupHeaderInfo
            {
                GroupKey = groupKey,
                GroupName = groupName,
                TotalWidth = totalWidth,
                ColumnCount = count,
                CssClass = GetHeaderGroupCss(groupKey, true)
            });

        // 操作列占位（150px）
        result.Add(new GroupHeaderInfo
        {
            GroupKey = 0,
            GroupName = "",
            TotalWidth = 150,
            ColumnCount = 0,
            CssClass = ""
        });

        return result;
    }

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch { 1 => "col-g1", 2 => "col-g2", 3 => "col-g3", 4 => "col-g4", _ => "" };
        if (isGroupStart && groupKey > 1) cls += " col-group-start";
        return cls;
    }

    private static string GetCellGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch { 1 => "col-g1-cell", 2 => "col-g2-cell", 3 => "col-g3-cell", 4 => "col-g4-cell", _ => "" };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        // ========== ① 基本信息（基本信息 + 合同交付 合并；默认仅显示：订单号/签订日期/业务员/客户名称/交期截止/订单总重量/含项次数） ==========
        new() { Key = "ordernumber",   Label = "订单号",   SortKey = "ordernumber",   FilterType = "string", Width = "120", GroupKey = 1, GroupName = "① 基本信息" },
        new() { Key = "signdate",      Label = "签订日期", SortKey = "signdate",     FilterType = "date", Width = "120", GroupKey = 1, GroupName = "① 基本信息" },
        new() { Key = "salesman",      Label = "业务员",   SortKey = "salesman",     FilterType = "string", Width = "120", GroupKey = 1, GroupName = "① 基本信息" },
        new() { Key = "customername",  Label = "客户名称", SortKey = "customername", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "① 基本信息" },
        new() { Key = "endcustomer",   Label = "最终客户", SortKey = "endcustomer",  FilterType = "string", Width = "120", GroupKey = 1, GroupName = "① 基本信息", Visible = false },
        new() { Key = "deliverystart", Label = "交期起始", SortKey = "deliverystart", FilterType = "date", Width = "120", GroupKey = 1, GroupName = "① 基本信息", Visible = false },
        new() { Key = "deliveryend",   Label = "交期截止", SortKey = "deliveryend",  FilterType = "date", Width = "120", GroupKey = 1, GroupName = "① 基本信息" },
        new() { Key = "hasdelaypenalty", Label = "延期罚款", SortKey = "hasdelaypenalty", FilterType = "boolean", Width = "60", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 1, GroupName = "① 基本信息", Visible = false },
        new() { Key = "TotalContractWeight", Label = "订单总重量", SortKey = "totalcontractweight", Width = "80", GroupKey = 1, GroupName = "① 基本信息" },
        new() { Key = "ItemCount", Label = "含项次数", SortKey = "itemcount", Width = "80", GroupKey = 1, GroupName = "① 基本信息" },
        // ========== ② 订单确认 ==========
        new() { Key = "notech",        Label = "技术要求", SortKey = "hastechnicalrequirement", FilterType = "boolean", Width = "120", BoolTrueLabel = "已编辑", BoolFalseLabel = "未编辑", GroupKey = 2, GroupName = "② 订单确认" },
        new() { Key = "status",        Label = "状态",     SortKey = "status", FilterType = "enum", Width = "120", GroupKey = 2, GroupName = "② 订单确认",
               EnumOptions = DisplayHelper.GetEnumFilterOptions<SalesOrderStatus>(),
               DisplayConverter = v => v is SalesOrderStatus s ? DisplayHelper.GetSalesOrderStatusText(s) : "-" },
        new() { Key = "createdby",   Label = "创建人",   Width = "100", GroupKey = 2, GroupName = "② 订单确认", Visible = false },
        new() { Key = "createdtime", Label = "创建时间", Width = "120", GroupKey = 2, GroupName = "② 订单确认", Visible = false },
        new() { Key = "updatedby",   Label = "更新人",   Width = "100", GroupKey = 2, GroupName = "② 订单确认", Visible = false },
        new() { Key = "lastchangedate",Label = "变更日期", SortKey = "lastchangedate", FilterType = "date", Width = "120", GroupKey = 2, GroupName = "② 订单确认", Visible = false },
        // ========== ③ 订单执行 ==========
        new() { Key = "schedulestage",     Label = "执行关注", SortKey = "schedulestage",     FilterType = "enum", Width = "100", GroupKey = 3, GroupName = "③ 订单执行",
               EnumOptions = new List<EnumOption> { new("", "未排产") }.Concat(DisplayHelper.GetScheduleStageOptions()).ToList(),
               DisplayConverter = v => v is SalesOrderListDto d ? d.ScheduleStageText : "-" },
        new() { Key = "urgencylevel",      Label = "紧急性",   SortKey = "urgencylevel",      FilterType = "string", Width = "80", GroupKey = 3, GroupName = "③ 订单执行" },
        new() { Key = "estimatedcompletiondate", Label = "预计完成", SortKey = "estimatedcompletiondate", FilterType = "date", Width = "100", GroupKey = 3, GroupName = "③ 订单执行" },
        new() { Key = "FinishedInboundWeight",  Label = "成品入库量", SortKey = "finishedinboundweight",  Width = "100", GroupKey = 3, GroupName = "③ 订单执行",
               DisplayConverter = v => v is SalesOrderListDto d ? d.FinishedInboundWeight.ToString("G29") : "-" },
        new() { Key = "FinishedOutboundWeight", Label = "成品出库量", SortKey = "finishedoutboundweight", Width = "100", GroupKey = 3, GroupName = "③ 订单执行",
               DisplayConverter = v => v is SalesOrderListDto d ? d.FinishedOutboundWeight.ToString("G29") : "-" },
        new() { Key = "FinishedStockWeight",   Label = "成品库存量", SortKey = "finishedstockweight",   Width = "100", GroupKey = 3, GroupName = "③ 订单执行",
               DisplayConverter = v => v is SalesOrderListDto d ? d.FinishedStockWeight.ToString("G29") : "-" },
        new() { Key = "businesscompleted",     Label = "业务完结",   SortKey = "businesscompleted",     FilterType = "boolean", BoolTrueLabel = "完结", BoolFalseLabel = "否", Width = "90", GroupKey = 3, GroupName = "③ 订单执行" },
    };

    // ========== 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;
        var props = typeof(SalesOrderListDto)
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

    private async Task<TableData<SalesOrderListDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        try
        {
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

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "signdate";
            var filtersJson = SerializeFilters();

            var query = new QueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending
            };

            if (!string.IsNullOrEmpty(filtersJson))
            {
                try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filtersJson); }
                catch { }
            }

            var result = await OrderService.GetPagedAsync(
                query,
                dateFrom: DateTime.TryParse(_dateFrom, out var dFrom) ? dFrom : null,
                dateTo: DateTime.TryParse(_dateTo, out var dTo) ? dTo : null,
                deliveryDateFrom: DateTime.TryParse(_deliveryDateFrom, out var ddFrom) ? ddFrom : null,
                deliveryDateTo: DateTime.TryParse(_deliveryDateTo, out var ddTo) ? ddTo : null,
                estimateFilter: _estimateLinkFilter);

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<SalesOrderListDto> { Items = _pageItems, TotalItems = _totalCount };

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

        ComputePageSums();
        await SavePageStateAsync();

        return new TableData<SalesOrderListDto>
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

    // ========== 筛选上下文加载（ExcelFilter 下拉选项） ==========

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await OrderService.GetFilterContextsAsync();
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
            var key = kvp.Key.ToLower(); // backend returns PascalCase, columns use lowercase
            _filterContextOptions[key] = kvp.Value.Select(v => new ExcelFilterOption
            {
                Value = v,
                Display = key switch
                {
                    "urgencylevel" => DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, v) ?? v,
                    _ => v
                },
                Count = 0
            }).ToList();
        }

        // Status 列显示中文
        if (_filterContextOptions.TryGetValue("status", out var statusOptions))
        {
            foreach (var opt in statusOptions)
            {
                opt.Display = opt.Value switch
                {
                    "Pending" => "待处理",
                    "Confirmed" => "已确认",
                    _ => opt.Value
                };
            }
        }

        // HasDelayPenalty 显示 是/否
        if (_filterContextOptions.TryGetValue("hasdelaypenalty", out var dpOptions))
        {
            foreach (var opt in dpOptions)
            {
                opt.Display = opt.Value == "True" ? "是" : "否";
            }
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

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("orders", ColumnPrefsVersion, _allColumns);
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
        var saved = await ColumnPrefs.LoadAsync("orders", ColumnPrefsVersion);
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
        var savedState = await PageState.LoadAsync("orders");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "signdate";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _dateFrom = savedState.Extras?.ContainsKey("dateFrom") == true ? savedState.Extras["dateFrom"] ?? string.Empty : string.Empty;
            _dateTo = savedState.Extras?.ContainsKey("dateTo") == true ? savedState.Extras["dateTo"] ?? string.Empty : string.Empty;
            _deliveryDateFrom = savedState.Extras?.ContainsKey("deliveryDateFrom") == true ? savedState.Extras["deliveryDateFrom"] ?? string.Empty : string.Empty;
            _deliveryDateTo = savedState.Extras?.ContainsKey("deliveryDateTo") == true ? savedState.Extras["deliveryDateTo"] ?? string.Empty : string.Empty;
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

            // 恢复小表点击联动筛选（若存在，则联动时已覆盖并清空其他搜索/日期/列筛选，保持一致）
            if (savedState.Extras?.ContainsKey("estimateLinkFilter") == true)
            {
                try
                {
                    _estimateLinkFilter = JsonSerializer.Deserialize<OrderDeliveryEstimateFilterDto>(savedState.Extras["estimateLinkFilter"]);
                    _estimateLinkLabel = savedState.Extras.TryGetValue("estimateLinkLabel", out var label) ? label : null;
                    if (_estimateLinkFilter != null)
                    {
                        _searchKeyword = string.Empty;
                        _dateFrom = string.Empty;
                        _dateTo = string.Empty;
                        _deliveryDateFrom = string.Empty;
                        _deliveryDateTo = string.Empty;
                        _columnFilters.Clear();
                    }
                }
                catch { }
            }
        }

        // 恢复页码
        if (savedState != null)
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);

        // 状态恢复后重新加载表格数据（首次渲染时 ServerData 可能已用默认值加载）
        if (savedState != null && table != null)
            await table.ReloadServerData();
        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#orders-list-table");
        }
        catch { }
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#orders-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(SalesOrderListDto order, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "ordernumber":
                builder.OpenComponent<MudLink>(0);
                builder.AddAttribute(1, "Typo", Typo.body2);
                builder.AddAttribute(2, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => ViewOrder(order.Id)));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, order.OrderNumber)));
                builder.CloseComponent();
                break;
            case "signdate":
                builder.AddContent(0, order.SignDate.ToString("yyyy-MM-dd"));
                break;
            case "salesman":
                builder.AddContent(0, order.Salesman);
                break;
            case "customername":
                builder.AddContent(0, order.CustomerName);
                break;
            case "endcustomer":
                builder.AddContent(0, order.EndCustomer);
                break;
            case "deliverystart":
                builder.AddContent(0, order.DeliveryStart?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "deliveryend":
                builder.AddContent(0, order.DeliveryEnd?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "hasdelaypenalty":
                builder.AddContent(0, DisplayHelper.GetYesNoText(order.HasDelayPenalty));
                break;
            case "TotalContractWeight":
                builder.AddContent(0, order.TotalContractWeight.ToString("G29"));
                break;
            case "ItemCount":
                builder.AddContent(0, order.ItemCount);
                break;
            case "notech":
                if (order.HasTechnicalRequirement)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Success);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "已编辑")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Error);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "未编辑")));
                    builder.CloseComponent();
                    if (order.FirstOrderItemId.HasValue)
                    {
                        builder.OpenComponent<MudIconButton>(0);
                        builder.AddAttribute(1, "Icon", Icons.Material.Filled.Edit);
                        builder.AddAttribute(2, "Size", Size.Small);
                        builder.AddAttribute(3, "Color", Color.Warning);
                        builder.AddAttribute(4, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => ViewTechnicalRequirement(order.Id)));
                        builder.CloseComponent();
                    }
                }
                break;
            case "status":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetStatusColor(order.Status));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetStatusText(order.Status))));
                builder.CloseComponent();
                break;
            case "lastchangedate":
                builder.AddContent(0, order.LastChangeDate?.ToString("yyyy-MM-dd HH:mm") ?? "-");
                break;
            case "createdby":
                builder.AddContent(0, order.CreatedBy);
                break;
            case "createdtime":
                builder.AddContent(0, order.CreatedTime?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "-");
                break;
            case "updatedby":
                builder.AddContent(0, order.UpdatedBy);
                break;
            case "schedulestage":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", order.ScheduleStage.HasValue ? DisplayHelper.GetScheduleStageColor(order.ScheduleStage.Value) : Color.Default);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, order.ScheduleStageText)));
                builder.CloseComponent();
                break;
            case "urgencylevel":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetUrgencyColor(order.UrgencyLevel));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, order.UrgencyLevel) ?? "-")));
                builder.CloseComponent();
                break;
            case "estimatedcompletiondate":
                // 主号完成（档1）时该值为实际入库截止日（事实值），用绿色 Chip 与预测值区分
                if (order.ScheduleStage == 1 && order.EstimatedCompletionDate.HasValue)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Success);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, order.EstimatedCompletionDate!.Value.ToString("yyyy-MM-dd"))));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, order.EstimatedCompletionDate?.ToString("yyyy-MM-dd") ?? "-");
                }
                break;
            case "FinishedInboundWeight":
                builder.AddContent(0, order.FinishedInboundWeight.ToString("G29"));
                break;
            case "FinishedOutboundWeight":
                builder.AddContent(0, order.FinishedOutboundWeight.ToString("G29"));
                break;
            case "FinishedStockWeight":
                builder.AddContent(0, order.FinishedStockWeight.ToString("G29"));
                break;
            case "businesscompleted":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", order.BusinessCompleted ? Color.Success : Color.Default);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, order.BusinessCompletedText)));
                builder.CloseComponent();
                break;
        }
    };

    // ========== GetCellRawValue / GetCellDisplayText（用于 ExcelFilter 旧模式，保留引用） ==========

    private string? GetCellRawValue(SalesOrderListDto item, string key) => key switch
    {
        "ordernumber" => item.OrderNumber,
        "signdate" => item.SignDate.ToString("yyyy-MM-dd"),
        "salesman" => item.Salesman,
        "customername" => item.CustomerName,
        "endcustomer" => item.EndCustomer,
        "deliverystart" => item.DeliveryStart?.ToString("yyyy-MM-dd"),
        "deliveryend" => item.DeliveryEnd?.ToString("yyyy-MM-dd"),
        "hasdelaypenalty" => item.HasDelayPenalty.ToString(),
        "TotalContractWeight" => item.TotalContractWeight.ToString(),
        "ItemCount" => item.ItemCount.ToString(),
        "notech" => item.HasTechnicalRequirement.ToString(),
        "status" => GetStatusText(item.Status),
        "lastchangedate" => item.LastChangeDate?.ToString("yyyy-MM-dd HH:mm"),
        "createdby" => item.CreatedBy,
        "createdtime" => item.CreatedTime?.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        "updatedby" => item.UpdatedBy,
        "schedulestage" => item.ScheduleStage?.ToString(),
        "urgencylevel" => item.UrgencyLevel,
        "estimatedcompletiondate" => item.EstimatedCompletionDate?.ToString("yyyy-MM-dd"),
        "FinishedInboundWeight" => item.FinishedInboundWeight.ToString("G29"),
        "FinishedOutboundWeight" => item.FinishedOutboundWeight.ToString("G29"),
        "FinishedStockWeight" => item.FinishedStockWeight.ToString("G29"),
        "businesscompleted" => item.BusinessCompleted.ToString(),
        _ => null
    };

    private string? GetCellDisplayText(SalesOrderListDto item, string key) => key switch
    {
        "hasdelaypenalty" => DisplayHelper.GetYesNoText(item.HasDelayPenalty),
        "notech" => item.HasTechnicalRequirement ? "已编辑" : "未编辑",
        "status" => GetStatusText(item.Status),
        "schedulestage" => item.ScheduleStageText,
        "urgencylevel" => DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, item.UrgencyLevel),
        "businesscompleted" => item.BusinessCompletedText,
        _ => GetCellRawValue(item, key)
    };

    // ========== 业务操作 ==========

    private void NavigateToCreate() => Navigation.NavigateTo("/orders/create");
    private void ViewOrder(int id) => Navigation.NavigateTo($"/orders/{id}");
    private void EditOrder(int id) => Navigation.NavigateTo($"/orders/{id}");
    private void ViewTechnicalRequirement(int orderId) => Navigation.NavigateTo($"/orders/{orderId}/requirements");
    private void NavigateToProgress(string orderNo) => Navigation.NavigateTo($"/orders/progress?salesOrderNo={System.Uri.EscapeDataString(orderNo)}");

    private async Task ConfirmOrder(SalesOrderListDto order)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要将订单 \"{order.OrderNumber}\" 确认为正式合同吗？\n\n确认后状态将变为\"已确认\"。",
            ["ConfirmText"] = "确认",
            ["Color"] = Color.Primary
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        try
        {
            var updateRequest = new UpdateSalesOrderRequest
            {
                Status = SalesOrderStatus.Confirmed,
                RowVersion = order.RowVersion ?? Array.Empty<byte>()
            };

            var result = await OrderService.UpdateAsync(order.Id, updateRequest);

            if (result.Success)
            {
                Snackbar.Add($"订单 \"{order.OrderNumber}\" 已确认为正式合同", Severity.Success);
                if (table != null) await table.ReloadServerData();
            }
            else
            {
                Snackbar.Add(result.Message ?? "确认失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"确认失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task CancelOrder(SalesOrderListDto order)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要取消订单 \"{order.OrderNumber}\" 吗？\n\n取消后订单将被永久删除，不可恢复！",
            ["ConfirmText"] = "确认取消",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        try
        {
            var result = await OrderService.DeleteAsync(order.Id);
            if (result.Success)
            {
                Snackbar.Add($"订单 \"{order.OrderNumber}\" 已取消", Severity.Success);
                if (table != null) await table.ReloadServerData();
            }
            else
            {
                Snackbar.Add(result.Message ?? "取消失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"取消失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 辅助方法 ==========

    private Color GetStatusColor(SalesOrderStatus status) => DisplayHelper.GetSalesOrderStatusColor(status);
    private string GetStatusText(SalesOrderStatus status) => DisplayHelper.GetSalesOrderStatusText(status);

    // ========== 完成预估及延期风险（卡片，仅加载两张交期预估小表） ==========

    private async Task ToggleEstimateCard()
    {
        _showEstimateCard = !_showEstimateCard;
        if (_showEstimateCard && _deliveryEstimate == null)
        {
            try
            {
                var estimate = await OrderService.GetDeliveryEstimateAsync();
                // 交期预估加载失败不阻断主表：保留 null，页面显示「正在加载...」
                if (estimate.Success && estimate.Data != null)
                    _deliveryEstimate = estimate.Data;
            }
            catch (Exception ex)
            {
                Snackbar.Add($"加载交期预估失败: {ex.Message}", Severity.Error);
            }
        }
    }

    /// <summary>小表单元格点击：按桶口径联动筛选订单列表（覆盖现有搜索/列筛选，显示可清除提示条）</summary>
    private async Task OnEstimateBucketClick(int tableIndex, int bucketIndex)
    {
        var estimateTable = _deliveryEstimate?.Tables.ElementAtOrDefault(tableIndex);
        var bucket = estimateTable?.Buckets.ElementAtOrDefault(bucketIndex);
        if (estimateTable == null || bucket == null || (bucket.Count <= 0 && bucket.Weight <= 0)) return;

        _estimateLinkFilter = new OrderDeliveryEstimateFilterDto
        {
            Table = estimateTable.Id,
            DateFrom = bucket.DateFrom,
            DateTo = bucket.DateTo
        };
        _estimateLinkLabel = $"{estimateTable.Name}·{estimateTable.BucketLabels[bucketIndex]}";

        // 覆盖现有搜索/签订日期/交货日期/列筛选
        _searchKeyword = string.Empty;
        _dateFrom = string.Empty;
        _dateTo = string.Empty;
        _deliveryDateFrom = string.Empty;
        _deliveryDateTo = string.Empty;
        _columnFilters.Clear();
        _resetToFirstPage = true;

        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
        Snackbar.Add($"已按「{_estimateLinkLabel}」联动筛选订单列表（{bucket.Count}单/{bucket.Weight.ToString("F1")}吨）", Severity.Info);
    }

    /// <summary>清除小表联动筛选，恢复全量列表</summary>
    private async Task ClearEstimateLinkFilter()
    {
        _estimateLinkFilter = null;
        _estimateLinkLabel = null;
        _resetToFirstPage = true;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    /// <summary>订单交期预估小表单元格（z单/x吨/y万，彩色取整；延期罚款不再单列显示）</summary>
    private static MarkupString FormatDeliveryBucket(OrderDeliveryBucketDto b)
        => OrderOverviewFormatter.RenderEstimateCell(b.Count, b.Weight, b.Amount);

    /// <summary>打印订单交期预估小表（两小表：订单(整单)完成预估 / 风险-已延期订单(整单)）</summary>
    private async Task PrintDeliveryEstimate(string tableId, string title)
    {
        try
        {
            var html = await JS.InvokeAsync<string>("getTableHtml", tableId);
            if (string.IsNullOrEmpty(html))
            {
                Snackbar.Add("未找到可打印的交期预估表", Severity.Warning);
                return;
            }
            await JS.InvokeVoidAsync("printRawHtml", html, title);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 投料产出总况（卡片，懒加载 + 口径切换 + 完成日期范围） ==========

    /// <summary>是否处于完成日期区间模式（任一端有值）</summary>
    private bool _throughputRangeMode =>
        !string.IsNullOrWhiteSpace(_throughputDateFrom) || !string.IsNullOrWhiteSpace(_throughputDateTo);

    private async Task ToggleThroughputCard()
    {
        _showThroughputCard = !_showThroughputCard;
        if (_showThroughputCard && _throughput == null)
            await LoadThroughputAsync();
    }

    /// <summary>加载投料产出总况（首次展开 / 口径切换 / 应用日期区间）；失败不阻断主表，保留既有数据</summary>
    private async Task LoadThroughputAsync()
    {
        try
        {
            var result = await OrderService.GetThroughputSummaryAsync(
                _throughputScope, ParseThroughputDate(_throughputDateFrom), ParseThroughputDate(_throughputDateTo));
            if (result.Success && result.Data != null)
                _throughput = result.Data;
            else
                Snackbar.Add(result.Message ?? "加载投料产出总况失败", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载投料产出总况失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task OnThroughputScopeChanged(string scope)
    {
        _throughputScope = ProductionScopeKeys.Resolve(scope);
        await LoadThroughputAsync();
    }

    /// <summary>应用完成日期区间（重新取数，服务端按「订单真实完成日落入区间」过滤）</summary>
    private async Task ApplyThroughputRangeAsync() => await LoadThroughputAsync();

    /// <summary>清除完成日期区间并回落到默认最近 12 个月</summary>
    private async Task ClearThroughputRangeAsync()
    {
        _throughputDateFrom = string.Empty;
        _throughputDateTo = string.Empty;
        await LoadThroughputAsync();
    }

    /// <summary>解析 yyyy-MM-dd 日期文本（空/非法返回 null，该端不参与过滤）</summary>
    private static DateTime? ParseThroughputDate(string text)
        => DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d) ? d : null;

    /// <summary>
    /// 卡内脚注（随日期区间模式切换）：区间模式须显式提示「按真实完成日过滤」与
    /// 「各列为命中订单的全生命周期合计（含区间外投料/入库量）」，避免被误读为区间内发生量。
    /// </summary>
    private string ThroughputFootnote()
        => _throughputRangeMode
            ? "注：行 = 所选完成日期范围（按订单真实完成日落入该区间过滤，起止当日均含；整区间聚合为 1 行）。"
              + "各列为命中订单的全生命周期合计、与完成日不相关——生产投料 = 各批次工艺卡领料重之和，入库列为各批次入库量之和，"
              + "故区间之外发生的投料/入库量也会计入本行。重量单位 kg 四舍五入取整，比率 = 0~1（分母 ≤0 显示 —）。"
              + "生产投料、次品入库已扣退货，退货单列供核对。订单数 = 本口径（生产类型范围）内有生产批次的订单数（非区间内完成订单总数）。"
              + "口径「全部」下订单成品入库只计交付态成品，「纯生产 / 单一生产类型」口径含非交付态（U 型管自产只到非交付态，交付态由外购委外产出）。"
            : "注：行 = 订单完成月（订单级「完成」，即该订单全部主号最终入库完成日所在月；固定近 12 个月，无完成订单的月不显示）；"
              + "重量单位 kg 四舍五入取整，比率 = 0~1（分母 ≤0 显示 —）。生产投料、次品入库已扣退货，退货单列供核对。"
              + "订单数 = 该完成月中在本口径（生产类型范围）内有生产批次的订单数，与本行各列同口径（非该月完成订单总数）；某月在该口径下无相关订单则该月整行不显示。"
              + "口径「全部」下订单成品入库只计交付态成品，「纯生产 / 单一生产类型」口径含非交付态（U 型管自产只到非交付态，交付态由外购委外产出）。";

    /// <summary>当前口径下由服务端返回的行（日期过滤已在服务端完成；区间模式下为整区间聚合的单行）</summary>
    private List<OrderThroughputMonthDto> _throughputRows => _throughput?.Months ?? new List<OrderThroughputMonthDto>();

    /// <summary>重量显示：四舍五入取整（本报表口径，不用 DisplayHelper.FormatDecimalAsInt 的截断）</summary>
    private static string FormatThroughputWeight(decimal value)
        => Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("0");

    /// <summary>比率显示：0~1 转百分数一位小数；分母 ≤0（null）显示占位符</summary>
    private static string FormatThroughputRate(decimal? ratio)
        => ratio.HasValue ? (ratio.Value * 100m).ToString("0.0") + "%" : "—";

    /// <summary>口径中文标签（打印标题用）</summary>
    private static string ThroughputScopeLabel(string scope)
    {
        var key = ProductionScopeKeys.Resolve(scope);
        foreach (var opt in _throughputScopeOptions)
        {
            if (opt.Key == key) return opt.Label;
        }
        return key;
    }

    /// <summary>打印投料产出总况表（所见即所得：按当前口径 + 完成日期范围结果输出；页脚由 print.js 带打印日期）</summary>
    private async Task PrintThroughputCard()
    {
        try
        {
            var html = await JS.InvokeAsync<string>("getTableHtml", "#order-throughput-table");
            if (string.IsNullOrEmpty(html))
            {
                Snackbar.Add("未找到可打印的投料产出总况表", Severity.Warning);
                return;
            }
            await JS.InvokeVoidAsync("printRawHtml", html, $"投料产出总况（{ThroughputScopeLabel(_throughputScope)}）");
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 打印方法 ==========
    /// <summary>打印选中列表（按当前可见列渲染列表 PDF，Mode A 前端已准备数据）</summary>
    private async Task PrintSelectedList()
    {
        if (!selectedOrderIds.Any())
        {
            Snackbar.Add("请先选择要打印的订单", Severity.Warning);
            return;
        }
        try
        {
            var selectedItems = _pageItems
                .Where(o => selectedOrderIds.Contains(o.Id))
                .Select(item =>
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var col in _visibleColumns)
                        dict[col.Key] = GetPrintValue(item, col);
                    return dict;
                }).ToList();

            var request = new OrderPrintListRequest
            {
                Title = "订单列表",
                Items = selectedItems,
                Columns = GetPrintColumnDefs()
            };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.Order}/print-list-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>当前可见列 → 打印列定义（Key/Label 对应当前列显隐与顺序）</summary>
    private List<PrintColumnDef> GetPrintColumnDefs() =>
        _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();

    /// <summary>按列取表格显示文本（复用 GetCellDisplayText，保证与页面单元格口径一致）</summary>
    private object GetPrintValue(SalesOrderListDto item, ColumnDef col) =>
        GetCellDisplayText(item, col.Key) ?? "-";

    private async Task PrintSelected(bool includeAmounts = true)
    {
        if (!selectedOrderIds.Any())
        {
            Snackbar.Add("请先选择要打印的订单", Severity.Warning);
            return;
        }
        try
        {
            var request = new OrderPrintBatchRequest { Ids = selectedOrderIds.ToArray(), IncludeAmounts = includeAmounts };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.Order}/print-file";
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
        if (!string.IsNullOrWhiteSpace(_dateFrom)) extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrWhiteSpace(_dateTo)) extras["dateTo"] = _dateTo;
        if (!string.IsNullOrWhiteSpace(_deliveryDateFrom)) extras["deliveryDateFrom"] = _deliveryDateFrom;
        if (!string.IsNullOrWhiteSpace(_deliveryDateTo)) extras["deliveryDateTo"] = _deliveryDateTo;
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
        if (_estimateLinkFilter != null)
        {
            extras["estimateLinkFilter"] = JsonSerializer.Serialize(_estimateLinkFilter);
            if (!string.IsNullOrWhiteSpace(_estimateLinkLabel)) extras["estimateLinkLabel"] = _estimateLinkLabel;
        }
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage,
            Extras = extras
        };
        await PageState.SaveAsync("orders", state);
    }
}
