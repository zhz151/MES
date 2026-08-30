using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Shared.Constants;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.Constants;
using MES.Core.Helpers;
using System.Text.Json;

namespace MES.Blazor.Pages.Materials;

public partial class PurchaseOrders : IAsyncDisposable
{
    private MudTable<PurchaseOrderDto>? table;
    private List<PurchaseOrderDto> _pageItems = new();
    private int _totalCount;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _loadVersion;
    private bool _resetToFirstPage;

    private string _searchKeyword = string.Empty;
    private bool _isArrowNavSetup;
    private bool _isAdmin;
    private int _currentPage = 1;
    private int _pageSize = 10;

    // 日期范围搜索
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;

    // B33: 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "Quantity", "Weight", "WoTotalQuantity", "WoTotalWeight", "WoTotalItemCount",
    };

    // 排序状态
    private string sortColumn = "orderdate";
    private bool sortDescending = true;

    // 选中状态
    private bool allSelected
    {
        get => _pageItems.Any() && _pageItems.All(i => selectedIds.Contains(i.Id));
        set
        {
            if (value)
            {
                foreach (var item in _pageItems)
                    selectedIds.Add(item.Id);
            }
            else
            {
                selectedIds.Clear();
            }
            StateHasChanged();
        }
    }
    private HashSet<int> selectedIds = new();

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // 空值筛选哨兵（与 ExcelFilter 组件/后端 Service 的 "__EXCEL_FILTER_NULL__" 一致）
    private const string FilterNull = "__EXCEL_FILTER_NULL__";

    // ========== 采购状态 & 关联异常 ==========
    private List<ProcurementStatusDto> procurementItems = new();
    private bool showProcurementStatus = false;
    private List<OrderMismatchInfo> mismatchItems = new();

    // ========== 采购首页汇总折叠卡片（荒管/成品 各 3 张：待购/在购/月度，懒加载） ==========
    private bool _showSemiPending;
    private bool _isLoadingSemiPending;
    private List<PurchasePendingDto> _semiPendingItems = new();
    private bool _showFinishedPending;
    private bool _isLoadingFinishedPending;
    private List<PurchasePendingDto> _finishedPendingItems = new();

    private bool _showSemiInProgress;
    private bool _isLoadingSemiInProgress;
    private PurchaseInProgressResultDto? _semiInProgressData;
    private bool _showFinishedInProgress;
    private bool _isLoadingFinishedInProgress;
    private PurchaseInProgressResultDto? _finishedInProgressData;

    private bool _showSemiMonthly;
    private bool _isLoadingSemiMonthly;
    private PurchaseMonthlyResultDto? _semiMonthlyData;
    private bool _showFinishedMonthly;
    private bool _isLoadingFinishedMonthly;
    private PurchaseMonthlyResultDto? _finishedMonthlyData;

    // ========== 状态面板定时轮询 ==========
    private CancellationTokenSource? _pollingCts;

    // ========== 列选择管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<EnumOption> GetMaterialCategoryOptions() => DisplayHelper.GetEnumFilterOptions<MaterialType>();

    private static List<ColumnDef> GetAllColumnDefs()
    {
        // G1: 采购信息
        var g1 = new List<ColumnDef>
        {
            new() { Key = "OrderNo",             Label = "采购单号",     SortKey = "orderno", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "采购信息" },
            new() { Key = "SupplierName",        Label = "供应商",       SortKey = "suppliername", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "采购信息" },
            new() { Key = "OrderDate",           Label = "下单日期",     SortKey = "orderdate", FilterType = "date", Width = "120", GroupKey = 1, GroupName = "采购信息" },
            new() { Key = "SourceWorkOrderNo",   Label = "来源工单号",   SortKey = "sourceworkorderno", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "采购信息" },
            new() { Key = "MaterialCategory",    Label = "物料分类",     SortKey = "materialcategory", FilterType = "enum", Width = "120", GroupKey = 1, GroupName = "采购信息", EnumOptions = GetMaterialCategoryOptions() },
            new() { Key = "PlantGrade",          Label = "厂内钢种",     SortKey = "plantgrade", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "采购信息" },
            new() { Key = "Specification",       Label = "规格",         SortKey = "specification", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "采购信息" },
            new() { Key = "UnitWeight",          Label = "单支重量",     SortKey = "unitweight", Width = "80", GroupKey = 1, GroupName = "采购信息" },
            new() { Key = "Quantity",            Label = "支数",         SortKey = "quantity", Width = "80", GroupKey = 1, GroupName = "采购信息" },
            new() { Key = "Weight",              Label = "采购重量",     SortKey = "weight", Width = "80", GroupKey = 1, GroupName = "采购信息" },
            new() { Key = "RequiredDate",        Label = "要求到货日",   SortKey = "requireddate", FilterType = "date", Width = "120", GroupKey = 1, GroupName = "采购信息" },
            new() { Key = "InputMultiple",       Label = "投料制成倍",   SortKey = "inputmultiple", Width = "80", GroupKey = 1, GroupName = "采购信息", Visible = false },
            new() { Key = "Remark",              Label = "采购备注",     SortKey = "remark", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "采购信息", Visible = false },
        };

        // G2: 工单实时关注（从工单执行状况读模型按来源工单号关联，无记录默认 "-"）
        var g2Exec = new List<ColumnDef>
        {
            new() { Key = "ExecutionScheduleStage",         Label = "工单关注",     SortKey = "executionschedulestage",         FilterType = "enum",   Width = "100", GroupKey = 2, GroupName = "工单实时关注",
                EnumOptions = new List<EnumOption> { new(FilterNull, "空值") }.Concat(DisplayHelper.GetScheduleStageOptions()).ToList() },
            new() { Key = "ExecutionRawMaterialLockRemark", Label = "原锁执行备注", SortKey = "executionrawmateriallockremark", FilterType = "string", Width = "130", GroupKey = 2, GroupName = "工单实时关注" },
            new() { Key = "ExecutionUrgencyLevel",          Label = "计划性",       SortKey = "executionurgencylevel",          FilterType = "string", Width = "100", GroupKey = 2, GroupName = "工单实时关注" },
            new() { Key = "ExecutionTheoreticalCutoffDate", Label = "理论截止投料日", SortKey = "executiontheoreticalcutoffdate", FilterType = "date",   Width = "120", GroupKey = 2, GroupName = "工单实时关注" },
        };

        // G3: 执行状态
        var g2 = new List<ColumnDef>
        {
            new() { Key = "Status",              Label = "状态",         SortKey = "status", FilterType = "enum", Width = "120", GroupKey = 3, GroupName = "执行状态",
                EnumOptions = DisplayHelper.GetEnumFilterOptions<PurchaseOrderStatus>() },
            new() { Key = "ArrivalDate",         Label = "到货截止日",   SortKey = "lastarrivaldate", FilterField = "LastArrivalDate", FilterType = "date", Width = "120", GroupKey = 3, GroupName = "执行状态" },
            new() { Key = "Received",            Label = "已到货量",     Width = "100", GroupKey = 3, GroupName = "执行状态" },
            new() { Key = "Returned",            Label = "退货量",       Width = "100", GroupKey = 3, GroupName = "执行状态" },
            new() { Key = "IsForceCompleted",    Label = "属强制完成",   SortKey = "isforcecompleted", FilterType = "enum", Width = "100", GroupKey = 3, GroupName = "执行状态",
                EnumOptions = DisplayHelper.GetBoolOptions() },
        };

        // G3: 来源销售订单（默认隐藏）
        var g3 = new List<ColumnDef>
        {
            new() { Key = "WoSalesOrderNo",      Label = "订单号",       SortKey = "wosalesorderno", FilterType = "string", Width = "120", GroupKey = 4, GroupName = "来源销售订单", Visible = false },
            new() { Key = "WoProductionMainNo",  Label = "主号",         SortKey = "woproductionmainno", FilterType = "string", Width = "120", GroupKey = 4, GroupName = "来源销售订单", Visible = false },
            new() { Key = "WoProductionSubNo",   Label = "次号",         SortKey = "woproductionsubno", FilterType = "string", Width = "120", GroupKey = 4, GroupName = "来源销售订单", Visible = false },
            new() { Key = "WoSignDate",          Label = "签订日期",     SortKey = "wosigndate", FilterType = "date", Width = "120", GroupKey = 4, GroupName = "来源销售订单", Visible = false },
            new() { Key = "WoSalesman",          Label = "业务员",       SortKey = "wosalesman", FilterType = "string", Width = "120", GroupKey = 4, GroupName = "来源销售订单", Visible = false },
            new() { Key = "WoEndCustomer",       Label = "最终用户",     SortKey = "woendcustomer", FilterType = "string", Width = "120", GroupKey = 4, GroupName = "来源销售订单", Visible = false },
            new() { Key = "WoDeliveryDate",      Label = "交货日期",     SortKey = "wodeliverydate", FilterType = "date", Width = "120", GroupKey = 4, GroupName = "来源销售订单", Visible = false },
            new() { Key = "WoDelayPenalty",      Label = "延期罚款",     SortKey = "wodelaypenalty", FilterType = "enum", Width = "120", GroupKey = 4, GroupName = "来源销售订单", Visible = false,
                EnumOptions = DisplayHelper.GetBoolOptions() },
            new() { Key = "WoSettlementMethod",  Label = "结算方式",     SortKey = "wosettlementmethod", FilterType = "enum", Width = "120", GroupKey = 4, GroupName = "来源销售订单", Visible = false,
                EnumOptions = DisplayHelper.GetEnumFilterOptions<SettlementMethod>() },
            new() { Key = "WoPlantGrade",        Label = "工厂牌号",     SortKey = "woplantgrade", FilterType = "string", Width = "120", GroupKey = 4, GroupName = "来源销售订单", Visible = false },
            new() { Key = "WoSpecification",     Label = "成品规格",     SortKey = "wospecification", FilterType = "string", Width = "120", GroupKey = 4, GroupName = "来源销售订单", Visible = false },
            new() { Key = "WoLengthStatus",      Label = "长度状态",     SortKey = "wolengthstatus", FilterType = "enum", Width = "120", GroupKey = 4, GroupName = "来源销售订单", Visible = false,
                EnumOptions = DisplayHelper.GetEnumFilterOptions<LengthStatus>() },
            new() { Key = "WoMaxLength",         Label = "最大长度",     SortKey = "womaxlength", Width = "80", GroupKey = 4, GroupName = "来源销售订单", Visible = false },
            new() { Key = "WoTotalQuantity",     Label = "总支数",       SortKey = "wototalquantity", Width = "80", GroupKey = 4, GroupName = "来源销售订单", Visible = false },
            new() { Key = "WoTotalWeight",       Label = "总重量",       SortKey = "wototalweight", Width = "80", GroupKey = 4, GroupName = "来源销售订单", Visible = false },
            new() { Key = "WoDeliveryState",     Label = "交货状态",     SortKey = "wodeliverystate", FilterType = "enum", Width = "120", GroupKey = 4, GroupName = "来源销售订单", Visible = false,
                EnumOptions = DisplayHelper.GetEnumFilterOptions<DeliveryState>() },
            new() { Key = "WoTotalItemCount",    Label = "含项次数",     SortKey = "wototalitemcount", Width = "80", GroupKey = 4, GroupName = "来源销售订单", Visible = false },
            new() { Key = "CreatedBy",           Label = "创建人",       SortKey = "createdby", FilterType = "string", Width = "100", GroupKey = 5, GroupName = "审计信息", Visible = false },
            new() { Key = "CreatedTime",         Label = "创建时间",     SortKey = "createdtime", FilterType = "date", Width = "130", GroupKey = 5, GroupName = "审计信息", Visible = false },
            new() { Key = "UpdatedBy",           Label = "更新人",       SortKey = "updatedby", FilterType = "string", Width = "100", GroupKey = 5, GroupName = "审计信息", Visible = false },
            new() { Key = "UpdatedTime",         Label = "更新时间",     SortKey = "updatedtime", FilterType = "date", Width = "130", GroupKey = 5, GroupName = "审计信息", Visible = false },
        };

        var all = new List<ColumnDef>();
        all.AddRange(g1);
        all.AddRange(g2Exec);
        all.AddRange(g2);
        all.AddRange(g3);
        return all;
    }

    // ========== 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(PurchaseOrderDto)
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
            catch
            {
                // ignore individual column sum errors
            }
        }
    }

    private string RenderFooterCell(ColumnDef col)
    {
        if (_pageSums.TryGetValue(col.Key, out var sum))
            return sum;
        return "-";
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<PurchaseOrderDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "orderdate";
            var filters = SerializeFilters();

            // 恢复持久化的页码（MudTable 初始化时始终传 page=0）
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

            DateTime? dateFrom = DateTime.TryParse(_dateFrom, out var dFrom) ? dFrom : null;
            DateTime? dateTo = DateTime.TryParse(_dateTo, out var dTo) ? dTo : null;

            var result = await PurchaseService.GetPagedAsync(
                new QueryParams
                {
                    PageIndex = state.Page + 1,
                    PageSize = state.PageSize,
                    SortBy = sortBy,
                    IsDescending = sortDescending,
                    Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                    Filters = filters
                },
                status: null,
                dateFrom: dateFrom,
                dateTo: dateTo);

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<PurchaseOrderDto> { Items = _pageItems, TotalItems = _totalCount };

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPage = state.Page + 1;
                ComputePageSums();
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

        return new TableData<PurchaseOrderDto>
        {
            Items = _pageItems,
            TotalItems = _totalCount
        };
    }

    private List<FilterDescriptor>? SerializeFilters()
    {
        if (_columnFilters.Count == 0) return null;
        var descriptors = new List<FilterDescriptor>();
        var colByKey = _allColumns.ToDictionary(c => c.Key, c => c);
        foreach (var kvp in _columnFilters)
        {
            if (kvp.Value.Count == 0) continue;
            // 别名列（显示 Key ≠ 后端字段名）经 FilterField 映射发后端
            var field = colByKey.TryGetValue(kvp.Key, out var col) && !string.IsNullOrEmpty(col.FilterField) ? col.FilterField! : kvp.Key;
            // 空值哨兵 → IncludeNull=true 生成 OR IS NULL；仅勾选空值时用 isnull 操作符
            var values = kvp.Value;
            var hasNull = values.Contains(FilterNull);
            var actualValues = values.Where(v => v != FilterNull).ToList();
            if (hasNull)
            {
                if (actualValues.Count > 0)
                    descriptors.Add(new FilterDescriptor { Field = field, Operator = "in", Values = actualValues, IncludeNull = true });
                else
                    descriptors.Add(new FilterDescriptor { Field = field, Operator = "isnull", IncludeNull = true });
            }
            else
            {
                descriptors.Add(new FilterDescriptor { Field = field, Operator = "in", Values = actualValues });
            }
        }
        return descriptors.Count > 0 ? descriptors : null;
    }

    // ========== 筛选上下文加载（ExcelFilter 下拉选项） ==========

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await PurchaseService.GetFilterContextsAsync();
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

        // Status 列显示中文
        if (_filterContextOptions.TryGetValue("Status", out var statusOptions))
        {
            foreach (var opt in statusOptions)
            {
                opt.Display = opt.Value switch
                {
                    "Open" => "已下单",
                    "Partial" => "部分到货",
                    "Completed" => "已完成",
                    "OverReceived" => "超量到货",
                    _ => opt.Value
                };
            }
        }

        // WoDelayPenalty 列显示中文
        if (_filterContextOptions.TryGetValue("WoDelayPenalty", out var delayOptions))
        {
            foreach (var opt in delayOptions)
                opt.Display = opt.Value == "True" ? "是" : "否";
        }

        // WoSettlementMethod 列显示中文
        if (_filterContextOptions.TryGetValue("WoSettlementMethod", out var settlementOptions))
        {
            foreach (var opt in settlementOptions)
            {
                opt.Display = opt.Value switch
                {
                    "MonthlyStatement" => "月结",
                    "PerOrder" => "单结",
                    "Deposit" => "定金",
                    "FullPayment" => "全款",
                    _ => opt.Value
                };
            }
        }

        // WoLengthStatus 列显示中文
        if (_filterContextOptions.TryGetValue("WoLengthStatus", out var lengthOptions))
        {
            foreach (var opt in lengthOptions)
            {
                opt.Display = opt.Value switch
                {
                    "Fixed" => "定尺",
                    "Range" => "范围尺",
                    _ => opt.Value
                };
            }
        }

        // WoDeliveryState 列显示中文
        if (_filterContextOptions.TryGetValue("WoDeliveryState", out var deliveryOptions))
        {
            foreach (var opt in deliveryOptions)
            {
                opt.Display = DisplayHelper.GetDeliveryStateText(opt.Value);
            }
        }

        // MaterialCategory 列显示中文
        if (_filterContextOptions.TryGetValue("MaterialCategory", out var materialCatOptions))
        {
            foreach (var opt in materialCatOptions)
            {
                opt.Display = DisplayHelper.GetMaterialTypeText(opt.Value);
            }
        }

        // 工单实时关注组：计划性 / 原锁执行备注 筛选选项显示中文（后端 DISTINCT 返回英文 Key）
        if (_filterContextOptions.TryGetValue("ExecutionUrgencyLevel", out var execUrgencyOptions))
        {
            foreach (var opt in execUrgencyOptions)
                opt.Display = DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, opt.Value) ?? opt.Value;
        }
        if (_filterContextOptions.TryGetValue("ExecutionRawMaterialLockRemark", out var execLockOptions))
        {
            foreach (var opt in execLockOptions)
                opt.Display = DictValueDisplayHelper.GetText(DictValueDefaults.RawMaterialLockRemarkKey, opt.Value) ?? opt.Value;
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

        // 空值选项统一显示「空值」（哨兵 "__EXCEL_FILTER_NULL__"，须在各项中文映射之后执行）
        foreach (var options in _filterContextOptions.Values)
        {
            foreach (var opt in options.Where(o => o.Value == FilterNull))
                opt.Display = "空值";
        }

        // 别名列（FilterField）选项回填到列 Key：如 ArrivalDate ← LastArrivalDate
        foreach (var col in _allColumns)
        {
            if (!string.IsNullOrEmpty(col.FilterField)
                && _filterContextOptions.TryGetValue(col.FilterField, out var aliasOpts)
                && !_filterContextOptions.ContainsKey(col.Key))
            {
                _filterContextOptions[col.Key] = aliasOpts;
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

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("purchase_orders", null, _allColumns);
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

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(PurchaseOrderDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "OrderNo":
                builder.OpenComponent<MudLink>(0);
                builder.AddAttribute(1, "Typo", Typo.body2);
                builder.AddAttribute(2, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => ViewDetail(item.Id)));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 =>
                {
                    b2.OpenElement(4, "strong");
                    b2.AddContent(5, item.OrderNo);
                    b2.CloseElement();
                }));
                builder.CloseComponent();
                break;
            case "SourceWorkOrderNo":
                builder.AddContent(0, item.SourceWorkOrderNo);
                break;
            case "ExecutionScheduleStage":
                if (item.ExecutionScheduleStage.HasValue)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", DisplayHelper.GetScheduleStageColor(item.ExecutionScheduleStage.Value));
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, IntStatusDisplayHelper.GetScheduleStageText(item.ExecutionScheduleStage.Value))));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;
            case "ExecutionRawMaterialLockRemark":
                builder.AddContent(0, string.IsNullOrEmpty(item.ExecutionRawMaterialLockRemark) ? "-" : (DictValueDisplayHelper.GetText(DictValueDefaults.RawMaterialLockRemarkKey, item.ExecutionRawMaterialLockRemark) ?? "-"));
                break;
            case "ExecutionUrgencyLevel":
                builder.AddContent(0, string.IsNullOrEmpty(item.ExecutionUrgencyLevel) ? "-" : (DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, item.ExecutionUrgencyLevel) ?? "-"));
                break;
            case "ExecutionTheoreticalCutoffDate":
                builder.AddContent(0, item.ExecutionTheoreticalCutoffDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "WoSalesOrderNo":
                builder.AddContent(0, item.WoSalesOrderNo);
                break;
            case "WoProductionMainNo":
                builder.AddContent(0, item.WoProductionMainNo);
                break;
            case "WoProductionSubNo":
                builder.AddContent(0, item.WoProductionSubNo);
                break;
            case "WoSignDate":
                builder.AddContent(0, item.WoSignDate?.ToString("yyyy-MM-dd"));
                break;
            case "WoSalesman":
                builder.AddContent(0, item.WoSalesman);
                break;
            case "WoEndCustomer":
                builder.AddContent(0, item.WoEndCustomer ?? "-");
                break;
            case "WoDeliveryDate":
                builder.AddContent(0, item.WoDeliveryDate?.ToString("yyyy-MM-dd"));
                break;
            case "WoDelayPenalty":
                builder.AddContent(0, DisplayHelper.GetYesNoText(item.WoDelayPenalty));
                break;
            case "WoSettlementMethod":
                builder.AddContent(0, DisplayHelper.GetSettlementMethodText(item.WoSettlementMethod));
                break;
            case "WoPlantGrade":
                builder.AddContent(0, item.WoPlantGrade);
                break;
            case "WoSpecification":
                builder.AddContent(0, item.WoSpecification);
                break;
            case "WoLengthStatus":
                builder.AddContent(0, DisplayHelper.GetLengthStatusText(item.WoLengthStatus));
                break;
            case "WoMaxLength":
                builder.AddContent(0, item.WoMaxLength?.ToString("G29") ?? "-");
                break;
            case "WoTotalQuantity":
                builder.AddContent(0, item.WoTotalQuantity);
                break;
            case "WoTotalWeight":
                builder.AddContent(0, item.WoTotalWeight.HasValue ? ((int)item.WoTotalWeight.Value).ToString() : "-");
                break;
            case "WoDeliveryState":
                builder.AddContent(0, DisplayHelper.GetDeliveryStateText(item.WoDeliveryState));
                break;
            case "WoTotalItemCount":
                builder.AddContent(0, item.WoTotalItemCount);
                break;
            case "OrderDate":
                builder.AddContent(0, item.OrderDate.ToString("yyyy-MM-dd"));
                break;
            case "MaterialCategory":
                builder.AddContent(0, DisplayHelper.GetMaterialTypeText(item.MaterialCategory));
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade);
                break;
            case "Specification":
                builder.AddContent(0, item.Specification);
                break;
            case "UnitWeight":
                builder.AddContent(0, item.UnitWeight?.ToString("G29") ?? "-");
                break;
            case "Quantity":
                builder.AddContent(0, item.Quantity?.ToString() ?? "-");
                break;
            case "InputMultiple":
                builder.AddContent(0, item.InputMultiple?.ToString() ?? "-");
                break;
            case "Remark":
                builder.AddContent(0, item.Remark ?? "-");
                break;
            case "Weight":
                builder.AddContent(0, ((int)item.Weight).ToString());
                break;
            case "RequiredDate":
                builder.AddContent(0, item.RequiredDate.ToString("yyyy-MM-dd"));
                break;
            case "SupplierName":
                builder.AddContent(0, item.SupplierName);
                break;
            case "Status":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetStatusColor(item.Status));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetStatusText(item.Status))));
                builder.CloseComponent();
                break;
            case "ArrivalDate":
                builder.AddContent(0, item.LastArrivalDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "Received":
                builder.AddContent(0, item.ReceivedQuantity == 0 && item.ReceivedWeight == 0m
                    ? "-"
                    : $"{item.ReceivedQuantity}支/{item.ReceivedWeight.ToString("G29")}kg");
                break;
            case "Returned":
                builder.AddContent(0, item.ReturnQuantity == 0 && item.ReturnWeight == 0m
                    ? "-"
                    : $"{item.ReturnQuantity}支/{item.ReturnWeight.ToString("G29")}kg");
                break;
            case "IsForceCompleted":
                builder.AddContent(0, item.IsForceCompleted ? "是" : "-");
                break;
            case "CreatedBy":
                builder.AddContent(0, string.IsNullOrEmpty(item.CreatedBy) ? "-" : item.CreatedBy);
                break;
            case "CreatedTime":
                builder.AddContent(0, item.CreatedTime == default ? "-" : item.CreatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;
            case "UpdatedBy":
                builder.AddContent(0, string.IsNullOrEmpty(item.UpdatedBy) ? "-" : item.UpdatedBy);
                break;
            case "UpdatedTime":
                builder.AddContent(0, item.UpdatedTime == default ? "-" : item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;
        }
    };

    // ========== 分组渲染 ==========

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
            if (lastKey.HasValue && gk != lastKey.Value)
            {
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

        // 操作列占位，对齐表格最右侧的操作按钮列
        result.Add(new GroupHeaderInfo
        {
            GroupKey = 0,
            GroupName = "",
            TotalWidth = 90,
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

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        _isAdmin = user.Claims.Any(c => c.Type == "role" && c.Value == "Admin")
                || user.IsInRole(Roles.Admin);

        await LoadProcurementStatus();

        // 列定义与偏好加载
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("purchase_orders", null);
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

        // 恢复排序/筛选状态
        var savedState = await PageState.LoadAsync("purchase_orders");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "orderdate";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);
            if (savedState.Extras?.ContainsKey("dateFrom") == true)
                _dateFrom = savedState.Extras["dateFrom"];
            if (savedState.Extras?.ContainsKey("dateTo") == true)
                _dateTo = savedState.Extras["dateTo"];
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

        // 状态恢复后重新加载表格数据（首次渲染时 ServerData 可能已用默认值加载）
        if (savedState != null && table != null)
            await table.ReloadServerData();

        // 加载筛选上下文
        await LoadFilterContextsAsync();

        // 加载关联异常
        await LoadOrderMismatches();

        // 启动状态面板定时轮询（后台运行，不阻塞初始化）
        _ = StartPollingAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 分组标题栏：测量实际列宽 + 同步滚动
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#purchase-orders-list-table");
        }
        catch { }

        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#purchase-orders-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 采购状态 ==========

    private async Task LoadProcurementStatus()
    {
        try
        {
            var result = await PurchaseService.GetProcurementStatusAsync();
            if (result.Success && result.Data != null)
                procurementItems = result.Data;
            else if (!result.Success)
                Snackbar.Add($"加载用料计划执行状态失败: {result.Message}", Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载用料计划执行状态异常: {ex.Message}", Severity.Error);
        }
    }

    private void ToggleProcurementStatus() => showProcurementStatus = !showProcurementStatus;

    // ========== 采购首页汇总折叠卡片（懒加载） ==========

    private async Task ToggleSemiPending()
    {
        _showSemiPending = !_showSemiPending;
        if (_showSemiPending && _semiPendingItems.Count == 0) await LoadPendingAsync(false);
    }

    private async Task ToggleFinishedPending()
    {
        _showFinishedPending = !_showFinishedPending;
        if (_showFinishedPending && _finishedPendingItems.Count == 0) await LoadPendingAsync(true);
    }

    private async Task ToggleSemiInProgress()
    {
        _showSemiInProgress = !_showSemiInProgress;
        if (_showSemiInProgress && _semiInProgressData == null) await LoadInProgressAsync(false);
    }

    private async Task ToggleFinishedInProgress()
    {
        _showFinishedInProgress = !_showFinishedInProgress;
        if (_showFinishedInProgress && _finishedInProgressData == null) await LoadInProgressAsync(true);
    }

    private async Task ToggleSemiMonthly()
    {
        _showSemiMonthly = !_showSemiMonthly;
        if (_showSemiMonthly && _semiMonthlyData == null) await LoadMonthlyAsync(false);
    }

    private async Task ToggleFinishedMonthly()
    {
        _showFinishedMonthly = !_showFinishedMonthly;
        if (_showFinishedMonthly && _finishedMonthlyData == null) await LoadMonthlyAsync(true);
    }

    private async Task LoadPendingAsync(bool isFinished)
    {
        if (isFinished) _isLoadingFinishedPending = true; else _isLoadingSemiPending = true;
        StateHasChanged();
        try
        {
            var result = await PurchaseService.GetPurchasePendingAsync(isFinished);
            var items = result.Success && result.Data != null ? result.Data : new List<PurchasePendingDto>();
            if (isFinished) _finishedPendingItems = items; else _semiPendingItems = items;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"{(isFinished ? "成品" : "荒管")}待购数据加载失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            if (isFinished) _isLoadingFinishedPending = false; else _isLoadingSemiPending = false;
            StateHasChanged();
        }
    }

    private async Task LoadInProgressAsync(bool isFinished)
    {
        if (isFinished) _isLoadingFinishedInProgress = true; else _isLoadingSemiInProgress = true;
        StateHasChanged();
        try
        {
            var result = await PurchaseService.GetPurchaseInProgressAsync(isFinished);
            var data = result.Success && result.Data != null ? result.Data : new PurchaseInProgressResultDto();
            if (isFinished) _finishedInProgressData = data; else _semiInProgressData = data;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"{(isFinished ? "成品" : "荒管")}在购数据加载失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            if (isFinished) _isLoadingFinishedInProgress = false; else _isLoadingSemiInProgress = false;
            StateHasChanged();
        }
    }

    private async Task LoadMonthlyAsync(bool isFinished)
    {
        if (isFinished) _isLoadingFinishedMonthly = true; else _isLoadingSemiMonthly = true;
        StateHasChanged();
        try
        {
            var result = await PurchaseService.GetPurchaseMonthlyAsync(isFinished);
            var data = result.Success && result.Data != null ? result.Data : new PurchaseMonthlyResultDto();
            if (isFinished) _finishedMonthlyData = data; else _semiMonthlyData = data;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"{(isFinished ? "成品" : "荒管")}月度采购数据加载失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            if (isFinished) _isLoadingFinishedMonthly = false; else _isLoadingSemiMonthly = false;
            StateHasChanged();
        }
    }

    // ========== 汇总卡片格式化 ==========

    /// <summary>待购量(kg) 格式化：0 值留空（后端已过滤 PendingWeight&gt;0，仅防边界）</summary>
    private static string FormatPendingWeight(decimal kg) => kg > 0 ? ((int)kg).ToString() : string.Empty;

    /// <summary>在购单元格格式化（t，保留 1 位）：总量&gt;0 显示「总量」，急量&gt;0 追加「[*急量]」（`*` 标红=表内急量数据值）；全 0 留空</summary>
    private static string FormatInProgressCell(PurchaseInProgressCellDto cell)
    {
        var total = cell.TotalWeight / 1000m;
        if (total <= 0) return string.Empty;
        var s = total.ToString("F1");
        var urgent = cell.UrgentWeight / 1000m;
        return urgent > 0 ? $"{s}[<span style=\"color:#d32f2f;font-weight:700;\">*</span>{urgent.ToString("F1")}]" : s;
    }

    /// <summary>在购单元格 MarkupString 渲染（急量 `*` 标红需按 HTML 解析）</summary>
    private static MarkupString RenderInProgressCell(PurchaseInProgressCellDto cell) => new(FormatInProgressCell(cell));

    /// <summary>月度单元格格式化（t，保留 1 位）：「购X/回Y」，0 值留空</summary>
    private static string FormatPurchaseMonthlyCell(decimal buy, decimal ret)
    {
        if (buy <= 0 && ret <= 0) return string.Empty;
        var parts = new List<string>();
        if (buy > 0) parts.Add("购" + (buy / 1000m).ToString("F1"));
        if (ret > 0) parts.Add("回" + (ret / 1000m).ToString("F1"));
        return string.Join("/", parts);
    }

    /// <summary>现在购(t) 格式化：kg /1000 保留 1 位，0 值留空</summary>
    private static string FormatNowInProgress(decimal kg) => kg > 0 ? (kg / 1000m).ToString("F1") : string.Empty;

    // ========== 汇总卡片打印（前端 printRawHtml 直接打印 DOM 表格） ==========

    private async Task PrintTableAsync(string tableId, string title)
    {
        try
        {
            var html = await JS.InvokeAsync<string>("getTableHtml", tableId);
            if (!string.IsNullOrEmpty(html))
                await JS.InvokeVoidAsync("printRawHtml", html, title);
            else
                Snackbar.Add("未找到可打印的汇总表格", Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private Task PrintSemiPendingTable() => PrintTableAsync("#po-semi-pending-table", "荒管待购实时数据");
    private Task PrintFinishedPendingTable() => PrintTableAsync("#po-finished-pending-table", "成品待购实时数据");
    private Task PrintSemiInProgressTable() => PrintTableAsync("#po-semi-in-progress-table", "荒管在购实时数据");
    private Task PrintFinishedInProgressTable() => PrintTableAsync("#po-finished-in-progress-table", "成品在购实时数据");
    private Task PrintSemiMonthlyTable() => PrintTableAsync("#po-semi-monthly-table", "荒管月度采购数据");
    private Task PrintFinishedMonthlyTable() => PrintTableAsync("#po-finished-monthly-table", "成品月度采购数据");

    private async Task LoadOrderMismatches()
    {
        try
        {
            var result = await PurchaseService.GetMismatchedOrdersAsync();
            if (result.Success && result.Data != null)
            {
                mismatchItems = result.Data;
            }
            else
                mismatchItems.Clear();
        }
        catch
        {
            mismatchItems.Clear();
        }
    }

    // ========== 打印 ==========

    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的采购单", Severity.Warning);
            return;
        }
        try
        {
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var ids = selectedIds.ToArray();
            var request = new OrderPrintBatchRequest { Ids = ids, Columns = _visibleColumns.Select(c => c.ToPrintColumnDef()).ToList() };
            var apiUrl = $"{Navigation.BaseUri}{ApiEndpoints.PurchaseOrder}/print-batch-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex) { Snackbar.Add($"打印失败: {ex.Message}", Severity.Error); }
    }

    // ========== 导航 ==========

    private void NavigateToCreate() => Navigation.NavigateTo("/purchase-orders/create");
    private void ViewDetail(int id) => Navigation.NavigateTo($"/purchase-orders/{id}");
    private void NavigateToEdit(int id) => Navigation.NavigateTo($"/purchase-orders/{id}");

    // ========== 取消订单 ==========

    private async Task CancelOrder(PurchaseOrderDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters { ["ContentText"] = $"确定要取消采购单 \"{item.OrderNo}\" 吗？", ["ConfirmText"] = "确认取消", ["Color"] = Color.Error });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await PurchaseService.DeleteAsync(item.Id);
                if (result.Success) { Snackbar.Add("取消成功", Severity.Success); await LoadProcurementStatus(); if (table != null) await table.ReloadServerData(); await LoadFilterContextsAsync(); }
                else { Snackbar.Add(result.Message ?? "取消失败", Severity.Error); }
            }
            catch (Exception ex) { Snackbar.Add($"取消失败: {ex.Message}", Severity.Error); }
        }
    }

    // ========== 颜色 ==========

    private static Color GetStatusColor(PurchaseOrderStatus status) => DisplayHelper.GetPurchaseOrderStatusColor(status);
    private static string GetStatusText(PurchaseOrderStatus status) => DisplayHelper.GetPurchaseOrderStatusText(status);

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
        if (!string.IsNullOrEmpty(_dateFrom)) extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrEmpty(_dateTo)) extras["dateTo"] = _dateTo;
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage,
            Extras = extras
        };
        await PageState.SaveAsync("purchase_orders", state);
    }

    // ========== 状态面板定时轮询（每 2 分钟） ==========

    private async Task StartPollingAsync()
    {
        _pollingCts = new CancellationTokenSource();
        try
        {
            while (!_pollingCts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(2), _pollingCts.Token);
                try
                {
                    await InvokeAsync(async () =>
                    {
                        await LoadProcurementStatus();
                        StateHasChanged();
                    });
                }
                catch
                {
                    // 单次轮询异常不影响下一轮
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 组件销毁时正常取消
        }
    }

    public ValueTask DisposeAsync()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        return ValueTask.CompletedTask;
    }
}
