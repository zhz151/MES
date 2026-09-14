using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Blazored.LocalStorage;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.Helpers;
using MES.Blazor.Helpers;
using MES.Blazor.Services;
using MudBlazor;

namespace MES.Blazor.Pages.Reports;

/// <summary>
/// 报表系统总览页（6 Tab 聚合各上下文已有汇总数据，懒加载，Tab2/Tab6 嵌入现有组件）
/// </summary>
public partial class ReportOverview
{
    [Inject] private OrderService OrderService { get; set; } = null!;
    [Inject] private PurchaseOrderService PurchaseService { get; set; } = null!;
    [Inject] private SubcontractOrderService SubcontractService { get; set; } = null!;
    [Inject] private RawMaterialLockPlanAndExecutionService RawMaterialLockPlanService { get; set; } = null!;
    [Inject] private BatchPlanService BatchPlanSvc { get; set; } = null!;
    [Inject] private SectionParagraphFlowAnalysisService SectionParagraphFlowAnalysisSvc { get; set; } = null!;
    [Inject] private SectionOutsourceService SectionOutsourceSvc { get; set; } = null!;
    [Inject] private ColdRollPlanService ColdRollSvc { get; set; } = null!;
    [Inject] private FinalInspectionPlanService FinalInspectionPlanSvc { get; set; } = null!;
    [Inject] private FinalInspectionService FinalInspectionSvc { get; set; } = null!;
    [Inject] private NcrService NcrSvc { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private ILocalStorageService LocalStorage { get; set; } = null!;

    // 三张「往来数据」表（客户/供应商/委外单位）：直接调源上下文列表同款分页端点取整表
    [Inject] private CustomerService CustomerSvc { get; set; } = null!;
    [Inject] private SupplierService SupplierSvc { get; set; } = null!;
    [Inject] private OutsourceVendorService OutsourceVendorSvc { get; set; } = null!;

    // ========== 懒加载状态 ==========

    private int _activeIndex;
    private readonly bool[] _loaded = new bool[7];
    private readonly bool[] _loading = new bool[7];
    private readonly bool[] _failed = new bool[7];
    private readonly string[] _errorMessages = new string[7];

    /// <summary>嵌入组件（Tab2/Tab6）重建键，页面级刷新时 +1 强制重新实例化自动加载</summary>
    private int _refreshKey;

    protected override async Task OnInitializedAsync()
    {
        await LoadCollapsedCardsAsync();
        _activeIndex = 0;
        await ActivateTabAsync(0);
    }

    // ========== 汇总卡折叠状态（localStorage 持久化） ==========
    // 方案B（业务总况/物料执行/生产执行/质量管理）：默认折叠「月度/历史」类低频卡，核心实时卡展开
    // 业务总况（2026-09-14）：全卡可折叠（客户往来/投料产出总况属历史类默认折叠，订单接单出库·现负荷汇总默认展开）
    // 原料需求 为单区域大表 Tab（非多卡），不参与折叠
    // 现订单负荷总量/仓库报表 为嵌入组件 Tab，无可打印汇总卡，不参与折叠
    // 三张「往来数据」卡（2026-09-10 批二十二）：低频查看、默认折叠，展开默认显 10 行 + 行数切换

    /// <summary>卡折叠状态字典：key=卡片标识 → true=折叠；未记录时取默认折叠集</summary>
    private readonly Dictionary<string, bool> _cardCollapsed = new(StringComparer.OrdinalIgnoreCase);
    private const string CollapsedStorageKey = "reportoverview_card_collapsed";

    /// <summary>默认折叠的卡（首次进入/未持久化时的展示策略）</summary>
    private static readonly HashSet<string> DefaultCollapsedCards = new(StringComparer.OrdinalIgnoreCase)
    {
        // 三张「往来数据」卡（批二十二）：默认折叠，展开显 10 行
        "report:throughput",        // 投料产出总况（2026-09-14）：12 个月趋势/历史类，默认折叠
        "report:customer-trade",
        "report:supplier-trade",
        "report:outsource-trade",
        // 物料执行（方案B：3 张月度历史卡默认折叠）
        "material:semi-monthly",
        "material:finished-monthly",
        "material:piercing-monthly",
        // 生产执行（方案B：2 张月度历史卡默认折叠）
        "production:monthly-production",
        "production:monthly-outsource",
        // 质量管理（方案B：2 张月度历史卡默认折叠）
        "quality:monthly-inspection",
        "quality:ncr-monthly",
    };

    private async Task LoadCollapsedCardsAsync()
    {
        try
        {
            var saved = await LocalStorage.GetItemAsync<Dictionary<string, bool>>(CollapsedStorageKey);
            if (saved != null)
                foreach (var kv in saved)
                    _cardCollapsed[kv.Key] = kv.Value;
        }
        catch { /* 无持久化记录时使用默认折叠集 */ }
    }

    private bool IsCardCollapsed(string key)
        => _cardCollapsed.TryGetValue(key, out var c) ? c : DefaultCollapsedCards.Contains(key);

    private async Task ToggleCardAsync(string key)
    {
        _cardCollapsed[key] = !IsCardCollapsed(key);
        try { await LocalStorage.SetItemAsync(CollapsedStorageKey, _cardCollapsed); }
        catch { /* 持久化失败不影响本次交互 */ }
        StateHasChanged();
    }

    private void OnActivePanelIndexChanged(int index)
    {
        _activeIndex = index;
        _ = ActivateTabAsync(index);
    }

    private async Task ActivateTabAsync(int index)
    {
        if (index < 0 || index >= 7 || _loaded[index] || _loading[index]) return;
        _loading[index] = true;
        StateHasChanged();
        try
        {
            switch (index)
            {
                case 0: await LoadTab1Async(); break;
                case 2: await LoadTab3Async(); break;
                case 3: await LoadTab4Async(); break;
                case 4: await LoadTab5Async(); break;
                case 5: await LoadTab6Async(); break;
                // 1 现负荷 / 6 仓库：嵌入组件激活时自加载，无需拉取
            }
            _loaded[index] = true;
        }
        catch (Exception ex)
        {
            _failed[index] = true;
            _errorMessages[index] = ex.Message;
        }
        finally
        {
            _loading[index] = false;
            StateHasChanged();
        }
    }

    /// <summary>页面级「全部刷新」：已加载的数据 Tab 重拉 + 嵌入组件重建</summary>
    private async Task RefreshAllAsync()
    {
        _refreshKey++;
        for (var i = 0; i < 7; i++)
        {
            if (_loaded[i] && i is not (1 or 6))
            {
                _loaded[i] = false;
                _failed[i] = false;
                await ActivateTabAsync(i);
            }
        }
    }

    // ========== Tab1 业务总况：订单接单·出库及现负荷汇总 ==========

    private OrderInOutSummaryDto? _inOutSummary;

    /// <summary>订单交期预估（两小表：订单完成预估 / 延期交货订单预估，x单/y吨，订单级口径）</summary>
    private OrderDeliveryEstimateDto? _deliveryEstimate;

    private async Task LoadTab1Async()
    {
        var t1 = OrderService.GetInOutSummaryAsync(DateTime.Today.Year);
        var t2 = OrderService.GetDeliveryEstimateAsync();
        var t3 = LoadCustomerTradeAsync();
        var t4 = LoadThroughputAsync();
        await Task.WhenAll(t1, t2, t3, t4);

        var r1 = await t1;
        if (r1.Success && r1.Data != null)
            _inOutSummary = r1.Data;
        else
            throw new InvalidOperationException(r1.Message ?? "订单接单·出库及现负荷汇总获取失败");

        var r2 = await t2;
        if (r2.Success && r2.Data != null)
            _deliveryEstimate = r2.Data;
        // 交期预估加载失败不阻断主表：保留 null，页面显示「暂无数据」
    }

    // ========== Tab1 业务总况·客户往来数据（同源客户管理列表：整表一次性加载 + 身份列排序 + 搜索） ==========

    private List<CustomerProfileDto> _customerTradeRows = new();
    private string _customerTradeKeyword = "";
    private string? _customerTradeSortBy;   // null=原序；salesman / endcustomer
    private bool _customerTradeDesc;
    private int _customerTradeTake = 10;    // 显示行数：0=全部；默认 10
    private string _customerTradeSignFrom = "";   // 接单区间-开始（yyyy-MM-dd，空=不限）
    private string _customerTradeSignTo = "";     // 接单区间-结束（yyyy-MM-dd，闭区间含当天）
    private string _customerTradeShipFrom = "";   // 发货区间-开始（yyyy-MM-dd，空=不限）
    private string _customerTradeShipTo = "";     // 发货区间-结束（yyyy-MM-dd，闭区间含当天）

    /// <summary>
    /// 是否处于「接单区间」模式（任一端填写即生效）。生效时服务端把「本年接单」改按 <c>SalesOrder.SignDate</c> 落区间重算。
    /// </summary>
    private bool CustomerTradeSignRangeMode => !string.IsNullOrWhiteSpace(_customerTradeSignFrom) || !string.IsNullOrWhiteSpace(_customerTradeSignTo);

    /// <summary>
    /// 是否处于「发货区间」模式（任一端填写即生效）。生效时服务端把「本年已发货」改按 <c>OutboundRecord.OutboundDate</c> 落区间重算。
    /// </summary>
    private bool CustomerTradeShipRangeMode => !string.IsNullOrWhiteSpace(_customerTradeShipFrom) || !string.IsNullOrWhiteSpace(_customerTradeShipTo);

    /// <summary>任一时间区间生效（两个区间互相独立、可叠加）。生效时只显示「有数据」的客户行。</summary>
    private bool CustomerTradeAnyRangeMode => CustomerTradeSignRangeMode || CustomerTradeShipRangeMode;

    /// <summary>接单区间生效时唯一有意义的列（服务端口径已切换）。</summary>
    private static readonly string[] CustomerTradeSignColumns = ["YearOrder"];
    /// <summary>发货区间生效时唯一有意义的列（服务端口径已切换）。</summary>
    private static readonly string[] CustomerTradeShipColumns = ["ShippedDone", "ShippedOther"];

    /// <summary>
    /// 当前区间模式下「有数据」的列集合；返回 null 表示未启用任何区间（8 列全部正常展示、不筛行）。
    /// 未激活的列渲染为「—」防视觉污染——因为该列仍为自然年/存量的旧口径，与所选区间不同源。
    /// </summary>
    private HashSet<string>? CustomerTradeActiveColumns()
    {
        if (!CustomerTradeAnyRangeMode) return null;
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (CustomerTradeSignRangeMode) set.UnionWith(CustomerTradeSignColumns);
        if (CustomerTradeShipRangeMode) set.UnionWith(CustomerTradeShipColumns);
        return set;
    }

    /// <summary>该统计列在当前区间模式下是否有数据（无区间模式时恒 true）</summary>
    private bool CustomerTradeColumnActive(string col)
        => CustomerTradeActiveColumns()?.Contains(col) ?? true;

    /// <summary>拉取客户档案全量（PageSize=5000 覆盖全量小档案，统计随 GetPagedAsync 回填；失败不阻断总览）</summary>
    private async Task LoadCustomerTradeAsync()
    {
        try
        {
            var r = await CustomerSvc.GetPagedAsync(new QueryParams
            {
                PageIndex = 1,
                PageSize = 5000,
                SignDateFrom = ParseTradeDate(_customerTradeSignFrom),
                SignDateTo = ParseTradeDate(_customerTradeSignTo),
                ShipDateFrom = ParseTradeDate(_customerTradeShipFrom),
                ShipDateTo = ParseTradeDate(_customerTradeShipTo)
            });
            _customerTradeRows = OkData(r)?.Items ?? new List<CustomerProfileDto>();
        }
        catch { _customerTradeRows = new List<CustomerProfileDto>(); }
    }

    /// <summary>解析区间日期文本（yyyy-MM-dd；空或非法一律返回 null，该端不参与过滤）</summary>
    private static DateTime? ParseTradeDate(string text)
        => DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;

    /// <summary>接单区间变更：按新口径重新拉取统计（该区间两端皆空则回到「本年接单」口径）</summary>
    private async Task OnCustomerTradeSignChangedAsync()
    {
        await LoadCustomerTradeAsync();
        StateHasChanged();
    }

    /// <summary>发货区间变更：按新口径重新拉取统计（该区间两端皆空则回到「本年已发货」口径）</summary>
    private async Task OnCustomerTradeShipChangedAsync()
    {
        await LoadCustomerTradeAsync();
        StateHasChanged();
    }

    /// <summary>清空全部区间条件（接单 + 发货），回到完全现状口径</summary>
    private async Task ClearCustomerTradeRangeAsync()
    {
        _customerTradeSignFrom = _customerTradeSignTo = "";
        _customerTradeShipFrom = _customerTradeShipTo = "";
        await LoadCustomerTradeAsync();
        StateHasChanged();
    }

    /// <summary>表头动态文本：各自区间生效时对应列改称「区间接单 / 区间已发货」（口径已切换，防误导）</summary>
    private string CustomerTradeHeader(string col) => col switch
    {
        "YearOrder" => CustomerTradeSignRangeMode ? "区间接单" : "本年接单",
        "ShippedDone" => CustomerTradeShipRangeMode ? "区间已发货(整单)" : "本年已发货(整单)",
        "ShippedOther" => CustomerTradeShipRangeMode ? "区间已发货(非整单)" : "本年已发货(非整单)",
        _ => col
    };

    /// <summary>区间生效时的口径提示条（说明哪些列有值、哪些列置「—」、存量列不受影响）</summary>
    private string CustomerTradeRangeHint()
    {
        if (CustomerTradeSignRangeMode && CustomerTradeShipRangeMode)
            return "接单 + 发货区间模式：「区间接单」「区间已发货(整单/非整单)」按各自所选日期区间统计；其余列置「—」（口径与所选区间的维度不同源）；待发货/待在产为当前存量，不受区间影响；列表只显示所选区间内有数据的客户。";
        if (CustomerTradeSignRangeMode)
            return "接单区间模式：仅「区间接单」按所选接单日期区间统计；其余列置「—」（口径为自然年/全时段/当前存量，与区间不同源）；列表只显示该区间内有接单数据的客户。";
        return "发货区间模式：仅「区间已发货(整单/非整单)」按所选发货日期区间统计；其余列置「—」（口径为自然年/全时段/当前存量，与区间不同源）；列表只显示该区间内有发货数据的客户。";
    }

    /// <summary>区间/累计模式下的空值占位（灰色破折号，避免与真实 0 混淆）</summary>
    private static readonly MarkupString CustomerTradeDash = new("<span style=\"color:#9e9e9e\">—</span>");

    /// <summary>
    /// 客户往来可见行：任一区间生效时先剔除「激活列全为 0」的客户行（防区间口径下满屏空行），
    /// 再做身份列关键字过滤 + 业务员/最终用户点击排序（中文按 Unicode，客户端小集够用）。
    /// </summary>
    private List<CustomerProfileDto> CustomerTradeVisible()
    {
        IEnumerable<CustomerProfileDto> q = _customerTradeRows;

        var active = CustomerTradeActiveColumns();
        if (active != null)
        {
            q = q.Where(r => active.Any(col =>
            {
                var v = CustomerTradeValues(r, col);
                return v.Count > 0 || v.Weight > 0m;
            }));
        }

        if (!string.IsNullOrWhiteSpace(_customerTradeKeyword))
        {
            var kw = _customerTradeKeyword.Trim();
            q = q.Where(r => r.Salesman.Contains(kw, StringComparison.OrdinalIgnoreCase)
                          || r.EndCustomer.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }
        if (_customerTradeSortBy != null)
        {
            var sel = _customerTradeSortBy == "endcustomer"
                ? (Func<CustomerProfileDto, string>)(r => r.EndCustomer)
                : r => r.Salesman;
            q = _customerTradeDesc
                ? q.OrderByDescending(sel, StringComparer.OrdinalIgnoreCase)
                : q.OrderBy(sel, StringComparer.OrdinalIgnoreCase);
        }
        return q.ToList();
    }

    /// <summary>客户往来显示行：过滤排序后按行数设置截取（0=全部；无翻页，显示前 N 条）</summary>
    private List<CustomerProfileDto> CustomerTradeShown()
    {
        var all = CustomerTradeVisible();
        return _customerTradeTake > 0 && all.Count > _customerTradeTake
            ? all.Take(_customerTradeTake).ToList()
            : all;
    }

    private void SortCustomerTrade(string col)
    {
        if (_customerTradeSortBy == col) _customerTradeDesc = !_customerTradeDesc;
        else { _customerTradeSortBy = col; _customerTradeDesc = false; }
    }

    /// <summary>排序指示类名：被排序列浅蓝背景+加粗（表头不再渲染 ▲/▼ 箭头文字）</summary>
    private string CustomerTradeSortedClass(string col)
        => _customerTradeSortBy == col ? " report-th-sorted" : "";

    /// <summary>客户往来统计格（z单/x吨/y万 三色取整；与客户管理列表口径一致）。区间模式下未激活列置「—」。</summary>
    private MarkupString CustomerTradeCell(CustomerProfileDto r, string col)
    {
        if (!CustomerTradeColumnActive(col))
            return CustomerTradeDash;
        var v = CustomerTradeValues(r, col);
        return OrderOverviewFormatter.RenderTradeMarkup(v.Count, v.Weight, v.Amount, v.WithCount);
    }

    /// <summary>单行 8 统计列取值（客户 8 列均含单数成分；与源列表页「客户管理」往来信息列表同源同口径）</summary>
    private static (bool WithCount, int Count, decimal Weight, decimal Amount) CustomerTradeValues(CustomerProfileDto r, string col) => col switch
    {
        "TotalOrder" => (true, r.TotalOrderCount, r.TotalOrderWeight, r.TotalOrderAmount),
        "YearOrder" => (true, r.YearOrderCount, r.YearOrderWeight, r.YearOrderAmount),
        "ShippedDone" => (true, r.ShippedCompletedCount, r.ShippedCompletedWeight, r.ShippedCompletedAmount),
        "ShippedOther" => (true, r.ShippedOtherCount, r.ShippedOtherWeight, r.ShippedOtherAmount),
        "StockDone" => (true, r.StockCompletedCount, r.StockCompletedWeight, r.StockCompletedAmount),
        "StockOther" => (true, r.StockOtherCount, r.StockOtherWeight, r.StockOtherAmount),
        "WipNone" => (true, r.WipNoneCount, r.WipNoneWeight, r.WipNoneAmount),
        "WipPartial" => (true, r.WipPartialCount, r.WipPartialWeight, r.WipPartialAmount),
        _ => (true, 0, 0m, 0m)
    };

    /// <summary>客户往来列合计（按当前显示行汇总，供卡内「合计」行）。区间模式下未激活列置「—」。</summary>
    private MarkupString CustomerTradeSummary(IEnumerable<CustomerProfileDto> rows, string col)
    {
        if (!CustomerTradeColumnActive(col))
            return CustomerTradeDash;
        var c = 0; decimal w = 0m, a = 0m;
        foreach (var r in rows)
        {
            var v = CustomerTradeValues(r, col);
            c += v.Count; w += v.Weight; a += v.Amount;
        }
        return OrderOverviewFormatter.RenderTradeMarkup(c, w, a, true);
    }

    // ========== Tab1 业务总况·投料产出总况（行=订单完成月；同源订单列表页卡片） ==========

    /// <summary>投料产出总况（口径随 <see cref="_throughputScope"/>、日期范围随 <see cref="_throughputDateFrom"/>/<see cref="_throughputDateTo"/> 切换）</summary>
    private OrderThroughputSummaryDto? _throughput;
    /// <summary>生产类型范围口径（默认「全部四种」：荒管 + 在制 + 库存 + 外购）</summary>
    private string _throughputScope = ProductionScopeKeys.All;
    /// <summary>完成日期范围-起（yyyy-MM-dd；与止同时为空 = 默认最近 12 个月）</summary>
    private string _throughputDateFrom = string.Empty;
    /// <summary>完成日期范围-止（yyyy-MM-dd；含当天）</summary>
    private string _throughputDateTo = string.Empty;

    /// <summary>口径下拉选项（合计 2 档 + 单一生产类型 4 档；同源订单列表页）</summary>
    private static readonly (string Key, string Label)[] _throughputScopeOptions =
    [
        (ProductionScopeKeys.All, "全部（荒管+在制+库存+外购）"),
        (ProductionScopeKeys.Pure, "纯生产（荒管+在制）"),
        (ProductionTypeKeys.RoughTube, "荒管生产"),
        (ProductionTypeKeys.InProcess, "在制生产"),
        (ProductionTypeKeys.Inventory, "库存料生产"),
        (ProductionTypeKeys.OutsourcedPurchased, "外购生产"),
    ];

    /// <summary>是否处于完成日期区间模式（任一端有值）</summary>
    private bool _throughputRangeMode =>
        !string.IsNullOrWhiteSpace(_throughputDateFrom) || !string.IsNullOrWhiteSpace(_throughputDateTo);

    /// <summary>加载投料产出总况（随 Tab1 一并加载 / 口径或日期区间变更）；失败不阻断主表，保留 null 显示「暂无数据」</summary>
    private async Task LoadThroughputAsync()
    {
        try
        {
            var result = await OrderService.GetThroughputSummaryAsync(
                _throughputScope, ParseThroughputDate(_throughputDateFrom), ParseThroughputDate(_throughputDateTo));
            if (result.Success && result.Data != null)
                _throughput = result.Data;
        }
        catch { /* 不阻断业务总况主表 */ }
    }

    private async Task OnThroughputScopeChangedAsync(string scope)
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

    /// <summary>卡头口径说明（随日期区间模式切换文案）</summary>
    private string ThroughputRangeCaption()
        => _throughputRangeMode ? "同源「订单列表页」卡片｜按完成日落入所选区间，整区间聚合为 1 行"
                                : "同源「订单列表页」卡片｜行 = 订单完成月（近 12 个月）";

    /// <summary>
    /// 卡内脚注（随日期区间模式切换）：区间模式须显式提示「按真实完成日过滤」与
    /// 「各列为命中订单的全生命周期合计（含区间外投料/入库量）」，避免被误读为区间内发生量。
    /// </summary>
    private string ThroughputFootnote()
        => _throughputRangeMode
            ? "注：行 = 所选完成日期范围（按订单真实完成日落入该区间过滤，起止当日均含；整区间聚合为 1 行）。"
              + "各列为命中订单的全生命周期合计、与完成日不相关——生产投料 = 各批次工艺卡领料重之和，入库列为各批次入库量之和，"
              + "故区间之外发生的投料/入库量也会计入本行。重量 kg 四舍五入取整，比率 0~1（分母 ≤0 显示 —）。"
              + "生产投料、次品入库已扣退货，退货单列供核对。订单数 = 本口径内有生产批次的订单数（非区间内完成订单总数）。"
              + "口径「全部」下订单成品入库只计交付态成品，「纯生产 / 单一生产类型」口径含非交付态。"
            : "注：行 = 订单完成月（该订单全部主号最终入库完成日所在月；固定近 12 个月，无完成订单的月不显示）；"
              + "重量 kg 四舍五入取整，比率 0~1（分母 ≤0 显示 —）。生产投料、次品入库已扣退货，退货单列供核对。"
              + "订单数 = 该完成月中在本口径内有生产批次的订单数（非该月完成订单总数）。"
              + "口径「全部」下订单成品入库只计交付态成品，「纯生产 / 单一生产类型」口径含非交付态。";

    /// <summary>当前口径下由服务端返回的月度行（日期过滤已在服务端完成）</summary>
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

    // ========== Tab3 原料需求：原锁待投料量汇总 ==========

    private RawMaterialLockPendingSummaryDto? _pendingSummary;

    private async Task LoadTab3Async()
    {
        var result = await RawMaterialLockPlanService.GetPendingSummaryAsync();
        if (result.Success && result.Data != null)
            _pendingSummary = result.Data;
        else
            throw new InvalidOperationException(result.Message ?? "待投料量汇总获取失败");
    }

    // ========== Tab4 物料执行：采购 6 卡 + 穿孔 3 卡 ==========

    private List<PurchasePendingDto> _semiPendingItems = new();
    private PurchaseInProgressResultDto? _semiInProgressData;
    private PurchaseMonthlyResultDto? _semiMonthlyData;
    private List<PurchasePendingDto> _finishedPendingItems = new();
    private PurchaseInProgressResultDto? _finishedInProgressData;
    private PurchaseMonthlyResultDto? _finishedMonthlyData;
    private List<SubcontractPiercingPendingDto> _piercingPendingItems = new();
    private SubcontractPiercingInProgressResultDto? _piercingInProgressData;
    private SubcontractPiercingMonthlyResultDto? _piercingMonthlyData;

    private async Task LoadTab4Async()
    {
        var t1 = PurchaseService.GetPurchasePendingAsync(false);
        var t2 = PurchaseService.GetPurchaseInProgressAsync(false);
        var t3 = PurchaseService.GetPurchaseMonthlyAsync(false);
        var t4 = PurchaseService.GetPurchasePendingAsync(true);
        var t5 = PurchaseService.GetPurchaseInProgressAsync(true);
        var t6 = PurchaseService.GetPurchaseMonthlyAsync(true);
        var t7 = SubcontractService.GetPiercingPendingAsync();
        var t8 = SubcontractService.GetPiercingInProgressAsync();
        var t9 = SubcontractService.GetPiercingMonthlyAsync();
        await Task.WhenAll(t1, t2, t3, t4, t5, t6, t7, t8, t9);

        _semiPendingItems = OkData(await t1) ?? new();
        _semiInProgressData = OkData(await t2);
        _semiMonthlyData = OkData(await t3);
        _finishedPendingItems = OkData(await t4) ?? new();
        _finishedInProgressData = OkData(await t5);
        _finishedMonthlyData = OkData(await t6);
        _piercingPendingItems = OkData(await t7) ?? new();
        _piercingInProgressData = OkData(await t8);
        _piercingMonthlyData = OkData(await t9);
        await LoadSupplierTradeAsync();
    }

    // ========== Tab4 物料执行·供应商往来数据（同源供应商管理列表：整表加载 + 身份列排序 + 搜索） ==========

    private List<SupplierProfileDto> _supplierTradeRows = new();
    private string _supplierTradeKeyword = "";
    private string? _supplierTradeSortBy;   // null=原序；name / category
    private bool _supplierTradeDesc;
    private int _supplierTradeTake = 10;    // 显示行数：0=全部；默认 10
    private string _supplierTradeOrderFrom = "";     // 出单区间-开始（yyyy-MM-dd，空=不限）
    private string _supplierTradeOrderTo = "";       // 出单区间-结束（yyyy-MM-dd，闭区间含当天）
    private string _supplierTradeArrivalFrom = "";   // 到货区间-开始（yyyy-MM-dd，空=不限）
    private string _supplierTradeArrivalTo = "";     // 到货区间-结束（yyyy-MM-dd，闭区间含当天）

    /// <summary>是否处于「出单区间」模式（任一端填写即生效）。生效时把「本年出单」改按采购/委外单 <c>OrderDate</c> 落区间重算。</summary>
    private bool SupplierTradeOrderRangeMode => !string.IsNullOrWhiteSpace(_supplierTradeOrderFrom) || !string.IsNullOrWhiteSpace(_supplierTradeOrderTo);

    /// <summary>是否处于「到货区间」模式（任一端填写即生效）。生效时把「本年到货」「本年退货」改按 <c>InboundDate</c>/<c>OutboundDate</c> 落区间重算。</summary>
    private bool SupplierTradeArrivalRangeMode => !string.IsNullOrWhiteSpace(_supplierTradeArrivalFrom) || !string.IsNullOrWhiteSpace(_supplierTradeArrivalTo);

    /// <summary>任一时间区间生效（两个区间互相独立、可叠加）。生效时只显示「有数据」的供应商行。</summary>
    private bool SupplierTradeAnyRangeMode => SupplierTradeOrderRangeMode || SupplierTradeArrivalRangeMode;

    /// <summary>出单区间生效时唯一有意义的列（服务端口径已切换）</summary>
    private static readonly string[] SupplierTradeOrderColumns = ["YearOrder"];
    /// <summary>到货区间生效时有意义的列（到货 + 退货，两者同源同一区间窗口）</summary>
    private static readonly string[] SupplierTradeArrivalColumns = ["Arrived", "YearReturn"];

    /// <summary>当前区间模式下「有数据」的列集合；返回 null 表示未启用任何区间（5 列全部正常展示、不筛行）</summary>
    private HashSet<string>? SupplierTradeActiveColumns()
    {
        if (!SupplierTradeAnyRangeMode) return null;
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (SupplierTradeOrderRangeMode) set.UnionWith(SupplierTradeOrderColumns);
        if (SupplierTradeArrivalRangeMode) set.UnionWith(SupplierTradeArrivalColumns);
        return set;
    }

    /// <summary>该统计列在当前区间模式下是否有数据（无区间模式时恒 true）</summary>
    private bool SupplierTradeColumnActive(string col)
        => SupplierTradeActiveColumns()?.Contains(col) ?? true;

    /// <summary>拉取供应商档案全量（统计随 GetPagedAsync 回填；失败不阻断物料执行卡组）</summary>
    private async Task LoadSupplierTradeAsync()
    {
        try
        {
            var r = await SupplierSvc.GetPagedAsync(new QueryParams
            {
                PageIndex = 1,
                PageSize = 5000,
                SupplierOrderDateFrom = ParseTradeDate(_supplierTradeOrderFrom),
                SupplierOrderDateTo = ParseTradeDate(_supplierTradeOrderTo),
                SupplierArrivalDateFrom = ParseTradeDate(_supplierTradeArrivalFrom),
                SupplierArrivalDateTo = ParseTradeDate(_supplierTradeArrivalTo)
            });
            _supplierTradeRows = OkData(r)?.Items ?? new List<SupplierProfileDto>();
        }
        catch { _supplierTradeRows = new List<SupplierProfileDto>(); }
    }

    /// <summary>出单区间变更：按新口径重新拉取统计（该区间两端皆空则回到「本年出单」口径）</summary>
    private async Task OnSupplierTradeOrderChangedAsync()
    {
        await LoadSupplierTradeAsync();
        StateHasChanged();
    }

    /// <summary>到货区间变更：按新口径重新拉取统计（该区间两端皆空则回到「本年到货/本年退货」口径）</summary>
    private async Task OnSupplierTradeArrivalChangedAsync()
    {
        await LoadSupplierTradeAsync();
        StateHasChanged();
    }

    /// <summary>清空全部区间条件（出单 + 到货），回到完全现状口径</summary>
    private async Task ClearSupplierTradeRangeAsync()
    {
        _supplierTradeOrderFrom = _supplierTradeOrderTo = "";
        _supplierTradeArrivalFrom = _supplierTradeArrivalTo = "";
        await LoadSupplierTradeAsync();
        StateHasChanged();
    }

    /// <summary>表头动态文本：各自区间生效时对应列改称「区间出单 / 区间到货 / 区间退货」（口径已切换，防误导）</summary>
    private string SupplierTradeHeader(string col) => col switch
    {
        "YearOrder" => SupplierTradeOrderRangeMode ? "区间出单" : "本年出单",
        "Arrived" => SupplierTradeArrivalRangeMode ? "区间到货[扣除退货]" : "本年到货[扣除退货]",
        "YearReturn" => SupplierTradeArrivalRangeMode ? "区间退货" : "本年退货",
        _ => col
    };

    /// <summary>区间生效时的口径提示条（说明哪些列有值、哪些列置「—」、存量列不受影响）</summary>
    private string SupplierTradeRangeHint()
    {
        if (SupplierTradeOrderRangeMode && SupplierTradeArrivalRangeMode)
            return "出单 + 到货区间模式：「区间出单」「区间到货[扣除退货]」「区间退货」按各自所选日期区间统计；其余列置「—」（口径与所选区间的维度不同源）；待收货为当前存量，不受区间影响；列表只显示所选区间内有数据的供应商。";
        if (SupplierTradeOrderRangeMode)
            return "出单区间模式：仅「区间出单」按所选出单日期区间统计；其余列置「—」（口径为自然年/全时段/当前存量，与区间不同源）；列表只显示该区间内有出单数据的供应商。";
        return "到货区间模式：仅「区间到货[扣除退货]」「区间退货」按所选到货日期区间统计；其余列置「—」（口径为自然年/全时段/当前存量，与区间不同源）；列表只显示该区间内有到货数据的供应商。";
    }

    /// <summary>区间/累计模式下的空值占位（灰色破折号，避免与真实 0 混淆）</summary>
    private static readonly MarkupString SupplierTradeDash = new("<span style=\"color:#9e9e9e\">—</span>");

    private static string SupplierMaterialText(SupplierProfileDto r) => DisplayHelper.GetMaterialTypeText(r.MaterialCategory);

    /// <summary>供应商往来可见行：区间生效时先剔除「激活列全为 0」的行，再关键字过滤 + 名称/分类 点击排序</summary>
    private List<SupplierProfileDto> SupplierTradeVisible()
    {
        IEnumerable<SupplierProfileDto> q = _supplierTradeRows;

        var active = SupplierTradeActiveColumns();
        if (active != null)
        {
            q = q.Where(r => active.Any(col =>
            {
                var v = SupplierTradeValues(r, col);
                return v.Count > 0 || v.Weight > 0m;
            }));
        }

        if (!string.IsNullOrWhiteSpace(_supplierTradeKeyword))
        {
            var kw = _supplierTradeKeyword.Trim();
            q = q.Where(r => r.SupplierName.Contains(kw, StringComparison.OrdinalIgnoreCase)
                          || SupplierMaterialText(r).Contains(kw, StringComparison.OrdinalIgnoreCase)
                          || (r.Remark?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        if (_supplierTradeSortBy != null)
        {
            var sel = _supplierTradeSortBy == "category"
                ? (Func<SupplierProfileDto, string>)(SupplierMaterialText)
                : (Func<SupplierProfileDto, string>)(r => r.SupplierName);
            q = _supplierTradeDesc
                ? q.OrderByDescending(sel, StringComparer.OrdinalIgnoreCase)
                : q.OrderBy(sel, StringComparer.OrdinalIgnoreCase);
        }
        return q.ToList();
    }

    /// <summary>供应商往来显示行：过滤排序后按行数设置截取（0=全部；无翻页）</summary>
    private List<SupplierProfileDto> SupplierTradeShown()
    {
        var all = SupplierTradeVisible();
        return _supplierTradeTake > 0 && all.Count > _supplierTradeTake
            ? all.Take(_supplierTradeTake).ToList()
            : all;
    }

    private void SortSupplierTrade(string col)
    {
        if (_supplierTradeSortBy == col) _supplierTradeDesc = !_supplierTradeDesc;
        else { _supplierTradeSortBy = col; _supplierTradeDesc = false; }
    }

    /// <summary>排序指示类名：被排序列浅蓝背景+加粗（表头不再渲染 ▲/▼ 箭头文字）</summary>
    private string SupplierTradeSortedClass(string col)
        => _supplierTradeSortBy == col ? " report-th-sorted" : "";

    /// <summary>供应商往来统计格（出单列 z单/x吨/y万；到货/待收 x吨/y万；退货 仅吨；与供应商管理列表口径一致）。区间模式下未激活列置「—」。</summary>
    private MarkupString SupplierTradeCell(SupplierProfileDto r, string col)
    {
        if (!SupplierTradeColumnActive(col))
            return SupplierTradeDash;
        var v = SupplierTradeValues(r, col);
        return OrderOverviewFormatter.RenderTradeMarkup(v.Count, v.Weight, v.Amount, v.WithCount);
    }

    /// <summary>单行 5 统计列取值（出单列含单数；到货/待收 吨+万；退货 仅吨）</summary>
    private static (bool WithCount, int Count, decimal Weight, decimal Amount) SupplierTradeValues(SupplierProfileDto r, string col) => col switch
    {
        "TotalOrder" => (true, r.TotalOrderCount, r.TotalWeight, r.TotalAmount),
        "YearOrder" => (true, r.YearOrderCount, r.YearWeight, r.YearAmount),
        "Arrived" => (false, 0, r.ArrivedWeight, r.ArrivedAmount),
        "Pending" => (false, 0, r.PendingWeight, r.PendingAmount),
        "YearReturn" => (false, 0, r.YearReturnWeight, 0m),
        _ => (false, 0, 0m, 0m)
    };

    /// <summary>供应商往来列合计（按当前显示行汇总，供卡内「合计」行）。区间模式下未激活列置「—」。</summary>
    private MarkupString SupplierTradeSummary(IEnumerable<SupplierProfileDto> rows, string col)
    {
        if (!SupplierTradeColumnActive(col))
            return SupplierTradeDash;
        var c = 0; decimal w = 0m, a = 0m;
        foreach (var r in rows)
        {
            var v = SupplierTradeValues(r, col);
            c += v.Count; w += v.Weight; a += v.Amount;
        }
        var withCount = col is "TotalOrder" or "YearOrder";
        return OrderOverviewFormatter.RenderTradeMarkup(c, w, a, withCount);
    }

    // ========== Tab5 生产执行：冷轧拔近日排程 + 段落流转 + 近日/月度生产量 + 实时委外在产 + 月度委外 ==========

    private List<ColdRollScheduleSuggestionDto> _coldRollSuggestionRows = new();
    private List<SectionParagraphFlowAnalysisDto> _paragraphRows = new();
    private List<BatchPlanSummaryRowDto> _summaryRows = new();
    private List<BatchPlanMonthlySummaryRowDto> _monthlyProductionRows = new();
    private BatchPlanOutsourcePendingDto _outsourcePendingData = new();
    private List<SectionOutsourceMonthlyRowDto> _monthlyOutsourceRows = new();
    private List<string> _monthlyLabels = new();
    private Dictionary<string, int> _vendorRowspans = new();

    private async Task LoadTab5Async()
    {
        var suggestionTask = ColdRollSvc.GetScheduleSuggestionAsync();
        var paragraphTask = SectionParagraphFlowAnalysisSvc.GetAnalysisAsync();
        var summaryTask = BatchPlanSvc.GetSummaryAsync();
        var monthlyTask = BatchPlanSvc.GetMonthlySummaryAsync();
        var outsourcePendingTask = BatchPlanSvc.GetOutsourcePendingAsync();
        var monthlyOutsourceTask = SectionOutsourceSvc.GetMonthlyOutsourceAsync();
        var internalVendorsTask = SectionOutsourceSvc.GetInternalVendorsAsync();
        await Task.WhenAll(suggestionTask, paragraphTask, summaryTask, monthlyTask, outsourcePendingTask, monthlyOutsourceTask, internalVendorsTask);

        _coldRollSuggestionRows = await suggestionTask ?? new();
        _paragraphRows = OkData(await paragraphTask) ?? new();
        _summaryRows = await summaryTask ?? new();
        _monthlyProductionRows = await monthlyTask ?? new();

        // 实时委外在产：复制 SectionOutsources.LoadPendingAsync 厂内过滤 + 空列移除 + 合计重算
        var internalVendors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var iv = await internalVendorsTask;
        if (iv.Success && iv.Data != null)
            internalVendors = new HashSet<string>(iv.Data, StringComparer.OrdinalIgnoreCase);
        var data = await outsourcePendingTask ?? new BatchPlanOutsourcePendingDto();
        var rows = data.Rows.Where(r => r.OutsourceUnit != "合计" && !internalVendors.Contains(r.OutsourceUnit)).ToList();
        if (rows.Count == 0)
        {
            _outsourcePendingData = new BatchPlanOutsourcePendingDto();
        }
        else
        {
            _outsourcePendingData.Sections = data.Sections.Where(s => rows.Any(r => r.Cells.ContainsKey(s))).ToList();
            rows.Add(new OutsourcePendingRowDto
            {
                OutsourceUnit = "合计",
                Cells = new(),
                TotalCell = new OutsourcePendingCellDto { Total = rows.Sum(r => r.TotalCell.Total) }
            });
            _outsourcePendingData.Rows = rows;
        }

        var monthlyResult = await monthlyOutsourceTask;
        _monthlyOutsourceRows = monthlyResult.Success && monthlyResult.Data != null ? monthlyResult.Data : new();
        BuildVendorRowspans();
        _monthlyLabels = Enumerable.Range(1, 12)
            .Select(m => new DateTime(DateTime.Today.Year, m, 1).ToString("yyyy-MM"))
            .ToList();
        await LoadOutsourceTradeAsync();
    }

    // ========== Tab5 生产执行·委外单位往来数据（同源委外单位档案：排除本厂 IsWorkshop，整表加载 + 身份列排序 + 搜索） ==========

    private List<OutsourceVendorProfileDto> _outsourceTradeRows = new();
    private string _outsourceTradeKeyword = "";
    private string? _outsourceTradeSortBy;   // null=原序；vendor / section
    private bool _outsourceTradeDesc;
    private int _outsourceTradeTake = 10;    // 显示行数：0=全部；默认 10
    private string _outsourceTradeSendFrom = "";      // 发出区间-开始（yyyy-MM-dd，空=不限）
    private string _outsourceTradeSendTo = "";        // 发出区间-结束（yyyy-MM-dd，闭区间含当天）
    private string _outsourceTradeRecoveryFrom = "";  // 回收区间-开始（yyyy-MM-dd，空=不限）
    private string _outsourceTradeRecoveryTo = "";    // 回收区间-结束（yyyy-MM-dd，闭区间含当天）

    /// <summary>是否处于「发出区间」模式（任一端填写即生效）。生效时把「本年委外」改按工段委外单 <c>SendOutDate</c> 落区间重算。</summary>
    private bool OutsourceTradeSendRangeMode => !string.IsNullOrWhiteSpace(_outsourceTradeSendFrom) || !string.IsNullOrWhiteSpace(_outsourceTradeSendTo);

    /// <summary>是否处于「回收区间」模式（任一端填写即生效）。生效时把「本年回收」「本年退回」改按 <c>RecoveryDate</c> 落区间重算。</summary>
    private bool OutsourceTradeRecoveryRangeMode => !string.IsNullOrWhiteSpace(_outsourceTradeRecoveryFrom) || !string.IsNullOrWhiteSpace(_outsourceTradeRecoveryTo);

    /// <summary>任一时间区间生效（两个区间互相独立、可叠加）。生效时只显示「有数据」的委外单位行。</summary>
    private bool OutsourceTradeAnyRangeMode => OutsourceTradeSendRangeMode || OutsourceTradeRecoveryRangeMode;

    /// <summary>发出区间生效时唯一有意义的列（服务端口径已切换）</summary>
    private static readonly string[] OutsourceTradeSendColumns = ["YearOrder"];
    /// <summary>回收区间生效时有意义的列（回收 + 退回，两者同源同一区间窗口）</summary>
    private static readonly string[] OutsourceTradeRecoveryColumns = ["YearRecovered", "YearReturn"];

    /// <summary>当前区间模式下「有数据」的列集合；返回 null 表示未启用任何区间（5 列全部正常展示、不筛行）</summary>
    private HashSet<string>? OutsourceTradeActiveColumns()
    {
        if (!OutsourceTradeAnyRangeMode) return null;
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (OutsourceTradeSendRangeMode) set.UnionWith(OutsourceTradeSendColumns);
        if (OutsourceTradeRecoveryRangeMode) set.UnionWith(OutsourceTradeRecoveryColumns);
        return set;
    }

    /// <summary>该统计列在当前区间模式下是否有数据（无区间模式时恒 true）</summary>
    private bool OutsourceTradeColumnActive(string col)
        => OutsourceTradeActiveColumns()?.Contains(col) ?? true;

    /// <summary>拉取委外单位档案全量（仅保留外协行：厂内 IsWorkshop=1 无往来不计，统计随 GetPagedAsync 回填；失败不阻断生产执行卡组）</summary>
    private async Task LoadOutsourceTradeAsync()
    {
        try
        {
            var r = await OutsourceVendorSvc.GetPagedAsync(new QueryParams
            {
                PageIndex = 1,
                PageSize = 5000,
                VendorSendDateFrom = ParseTradeDate(_outsourceTradeSendFrom),
                VendorSendDateTo = ParseTradeDate(_outsourceTradeSendTo),
                VendorRecoveryDateFrom = ParseTradeDate(_outsourceTradeRecoveryFrom),
                VendorRecoveryDateTo = ParseTradeDate(_outsourceTradeRecoveryTo)
            });
            _outsourceTradeRows = (OkData(r)?.Items ?? new List<OutsourceVendorProfileDto>())
                .Where(v => !v.IsWorkshop).ToList();
        }
        catch { _outsourceTradeRows = new List<OutsourceVendorProfileDto>(); }
    }

    /// <summary>发出区间变更：按新口径重新拉取统计（该区间两端皆空则回到「本年委外」口径）</summary>
    private async Task OnOutsourceTradeSendChangedAsync()
    {
        await LoadOutsourceTradeAsync();
        StateHasChanged();
    }

    /// <summary>回收区间变更：按新口径重新拉取统计（该区间两端皆空则回到「本年回收/本年退回」口径）</summary>
    private async Task OnOutsourceTradeRecoveryChangedAsync()
    {
        await LoadOutsourceTradeAsync();
        StateHasChanged();
    }

    /// <summary>清空全部区间条件（发出 + 回收），回到完全现状口径</summary>
    private async Task ClearOutsourceTradeRangeAsync()
    {
        _outsourceTradeSendFrom = _outsourceTradeSendTo = "";
        _outsourceTradeRecoveryFrom = _outsourceTradeRecoveryTo = "";
        await LoadOutsourceTradeAsync();
        StateHasChanged();
    }

    /// <summary>表头动态文本：各自区间生效时对应列改称「区间委外 / 区间回收 / 区间退回」（口径已切换，防误导）</summary>
    private string OutsourceTradeHeader(string col) => col switch
    {
        "YearOrder" => OutsourceTradeSendRangeMode ? "区间委外" : "本年委外",
        "YearRecovered" => OutsourceTradeRecoveryRangeMode ? "区间回收" : "本年回收",
        "YearReturn" => OutsourceTradeRecoveryRangeMode ? "区间退回" : "本年退回",
        _ => col
    };

    /// <summary>区间生效时的口径提示条（说明哪些列有值、哪些列置「—」、存量列不受影响）</summary>
    private string OutsourceTradeRangeHint()
    {
        if (OutsourceTradeSendRangeMode && OutsourceTradeRecoveryRangeMode)
            return "发出 + 回收区间模式：「区间委外」「区间回收」「区间退回」按各自所选日期区间统计；其余列置「—」（口径与所选区间的维度不同源）；在委外未回收为当前存量，不受区间影响；列表只显示所选区间内有数据的委外单位。";
        if (OutsourceTradeSendRangeMode)
            return "发出区间模式：仅「区间委外」按所选发出日期区间统计；其余列置「—」（口径为自然年/全时段/当前存量，与区间不同源）；列表只显示该区间内有发出数据的委外单位。";
        return "回收区间模式：仅「区间回收」「区间退回」按所选回收日期区间统计；其余列置「—」（口径为自然年/全时段/当前存量，与区间不同源）；列表只显示该区间内有回收数据的委外单位。";
    }

    /// <summary>区间/累计模式下的空值占位（灰色破折号，避免与真实 0 混淆）</summary>
    private static readonly MarkupString OutsourceTradeDash = new("<span style=\"color:#9e9e9e\">—</span>");

    private static string OutsourceSectionText(OutsourceVendorProfileDto r) => SectionKeys.ToChinese(r.SectionName) ?? r.SectionName;

    /// <summary>委外单位往来可见行：区间生效时先剔除「激活列全为 0」的行，再关键字过滤 + 单位名/工段 点击排序</summary>
    private List<OutsourceVendorProfileDto> OutsourceTradeVisible()
    {
        IEnumerable<OutsourceVendorProfileDto> q = _outsourceTradeRows;

        var active = OutsourceTradeActiveColumns();
        if (active != null)
        {
            q = q.Where(r => active.Any(col =>
            {
                var v = OutsourceTradeValues(r, col);
                return v.Count > 0 || v.Weight > 0m;
            }));
        }

        if (!string.IsNullOrWhiteSpace(_outsourceTradeKeyword))
        {
            var kw = _outsourceTradeKeyword.Trim();
            q = q.Where(r => r.VendorName.Contains(kw, StringComparison.OrdinalIgnoreCase)
                          || OutsourceSectionText(r).Contains(kw, StringComparison.OrdinalIgnoreCase));
        }
        if (_outsourceTradeSortBy != null)
        {
            var sel = _outsourceTradeSortBy == "section"
                ? (Func<OutsourceVendorProfileDto, string>)(OutsourceSectionText)
                : (Func<OutsourceVendorProfileDto, string>)(r => r.VendorName);
            q = _outsourceTradeDesc
                ? q.OrderByDescending(sel, StringComparer.OrdinalIgnoreCase)
                : q.OrderBy(sel, StringComparer.OrdinalIgnoreCase);
        }
        return q.ToList();
    }

    /// <summary>委外单位往来显示行：过滤排序后按行数设置截取（0=全部；无翻页）</summary>
    private List<OutsourceVendorProfileDto> OutsourceTradeShown()
    {
        var all = OutsourceTradeVisible();
        return _outsourceTradeTake > 0 && all.Count > _outsourceTradeTake
            ? all.Take(_outsourceTradeTake).ToList()
            : all;
    }

    private void SortOutsourceTrade(string col)
    {
        if (_outsourceTradeSortBy == col) _outsourceTradeDesc = !_outsourceTradeDesc;
        else { _outsourceTradeSortBy = col; _outsourceTradeDesc = false; }
    }

    /// <summary>排序指示类名：被排序列浅蓝背景+加粗（表头不再渲染 ▲/▼ 箭头文字）</summary>
    private string OutsourceTradeSortedClass(string col)
        => _outsourceTradeSortBy == col ? " report-th-sorted" : "";

    /// <summary>委外单位往来统计格（累计/本年 z单/x吨/y万；回收/未回收 x吨/y万；退回 仅吨；与委外单位档案列表口径一致）。区间模式下未激活列置「—」。</summary>
    private MarkupString OutsourceTradeCell(OutsourceVendorProfileDto r, string col)
    {
        if (!OutsourceTradeColumnActive(col))
            return OutsourceTradeDash;
        var v = OutsourceTradeValues(r, col);
        return OrderOverviewFormatter.RenderTradeMarkup(v.Count, v.Weight, v.Amount, v.WithCount);
    }

    /// <summary>单行 5 统计列取值（累计/本年含单数；回收/未回收 吨+万；退回 仅吨）</summary>
    private static (bool WithCount, int Count, decimal Weight, decimal Amount) OutsourceTradeValues(OutsourceVendorProfileDto r, string col) => col switch
    {
        "TotalOrder" => (true, r.TotalOrderCount, r.TotalWeight, r.TotalAmount),
        "YearOrder" => (true, r.YearOrderCount, r.YearWeight, r.YearAmount),
        "YearRecovered" => (false, 0, r.YearRecoveredWeight, r.YearRecoveredAmount),
        "Pending" => (false, 0, r.PendingWeight, r.PendingAmount),
        "YearReturn" => (false, 0, r.YearReturnWeight, 0m),
        _ => (false, 0, 0m, 0m)
    };

    /// <summary>委外单位往来列合计（按当前显示行汇总，供卡内「合计」行）。区间模式下未激活列置「—」。</summary>
    private MarkupString OutsourceTradeSummary(IEnumerable<OutsourceVendorProfileDto> rows, string col)
    {
        if (!OutsourceTradeColumnActive(col))
            return OutsourceTradeDash;
        var c = 0; decimal w = 0m, a = 0m;
        foreach (var r in rows)
        {
            var v = OutsourceTradeValues(r, col);
            c += v.Count; w += v.Weight; a += v.Amount;
        }
        var withCount = col is "TotalOrder" or "YearOrder";
        return OrderOverviewFormatter.RenderTradeMarkup(c, w, a, withCount);
    }

    /// <summary>预计算同委外单位连续行数（后端已保证同单位相邻），供「委外单位」列合并单元格 rowspan</summary>
    private void BuildVendorRowspans()
    {
        _vendorRowspans.Clear();
        for (var i = 0; i < _monthlyOutsourceRows.Count; i++)
        {
            var vendor = _monthlyOutsourceRows[i].OutsourceVendor;
            var count = 1;
            while (i + count < _monthlyOutsourceRows.Count
                   && string.Equals(_monthlyOutsourceRows[i + count].OutsourceVendor, vendor, StringComparison.OrdinalIgnoreCase))
                count++;
            _vendorRowspans[vendor] = count;
            i += count - 1;
        }
    }

    // ========== Tab6 质量管理：待检批支重汇总 + 近日成检量 + 月度成检量 ==========

    private List<FinalInspectionPlanSummaryRowDto> _inspectionPlanSummaryRows = new();
    private List<FinalInspectionSummaryRowDto> _recentInspectionRows = new();
    private List<FinalInspectionMonthlySummaryRowDto> _monthlyInspectionRows = new();
    private List<string> _monthlyInspectionLabels = new();

    // ========== NCR：不合格品实时待处理 + 不合格品月度汇总 ==========
    private List<NcrPendingCheckDto> _ncrPendingItems = new();

    /// <summary>点「生产编号」跳生产批次详情页（本页策略 ReportView ⊆ BatchView，跳转不会被拦）</summary>
    private void OpenBatchDetail(int productionBatchId)
    {
        if (productionBatchId <= 0) return;
        Navigation.NavigateTo($"/batches/{productionBatchId}");
    }
    private NcrMonthlySummaryDto? _ncrMonthlySummary;
    private List<NcrMonthlyRowDto> _ncrMonthlyRows = new();
    private List<int> _ncrCategoryRowspans = new();
    private List<int> _ncrDeptRowspans = new();

    private async Task LoadTab6Async()
    {
        var t1 = FinalInspectionPlanSvc.GetSummaryAsync();
        var t2 = FinalInspectionSvc.GetRecentSummaryAsync();
        var t3 = FinalInspectionSvc.GetMonthlySummaryAsync();
        var t4 = NcrSvc.GetPendingChecksAsync();
        var t5 = NcrSvc.GetMonthlySummaryAsync();
        await Task.WhenAll(t1, t2, t3, t4, t5);

        _inspectionPlanSummaryRows = await t1;
        var r2 = await t2;
        _recentInspectionRows = OkData(r2) ?? new();
        var r3 = await t3;
        _monthlyInspectionRows = OkData(r3) ?? new();
        _monthlyInspectionLabels = Enumerable.Range(1, 12)
            .Select(m => new DateTime(DateTime.Today.Year, m, 1).ToString("yyyy-MM"))
            .ToList();

        // NCR 两表（不合格品实时待处理 / 月度汇总）：加载失败不阻断质量管理 Tab
        var r4 = await t4;
        _ncrPendingItems = OkData(r4) ?? new();
        var r5 = await t5;
        _ncrMonthlySummary = OkData(r5);
        _ncrMonthlyRows = _ncrMonthlySummary?.Rows ?? new();
        ComputeNcrMonthlyRowspans();
    }

    /// <summary>计算月度汇总 责任类别/责任部门 合并 rowspan（后端已按 责任类别→责任部门→处置方式 排序，同组相邻）</summary>
    private void ComputeNcrMonthlyRowspans()
    {
        _ncrCategoryRowspans = new List<int>(new int[_ncrMonthlyRows.Count]);
        _ncrDeptRowspans = new List<int>(new int[_ncrMonthlyRows.Count]);

        var i = 0;
        while (i < _ncrMonthlyRows.Count)
        {
            var category = _ncrMonthlyRows[i].ResponsibilityCategory;
            var catCount = 1;
            while (i + catCount < _ncrMonthlyRows.Count
                   && string.Equals(_ncrMonthlyRows[i + catCount].ResponsibilityCategory, category, StringComparison.Ordinal))
                catCount++;
            _ncrCategoryRowspans[i] = catCount;

            var j = i;
            var catEnd = i + catCount;
            while (j < catEnd)
            {
                var dept = _ncrMonthlyRows[j].ResponsibleDept;
                var deptCount = 1;
                while (j + deptCount < catEnd
                       && string.Equals(_ncrMonthlyRows[j + deptCount].ResponsibleDept, dept, StringComparison.Ordinal))
                    deptCount++;
                _ncrDeptRowspans[j] = deptCount;
                j += deptCount;
            }

            i += catCount;
        }
    }

    // ========== NCR 显示 ==========

    /// <summary>反馈部门 = 来源 + 检验项目（中文化，与 NcrForm 自动填充口径一致）</summary>
    private static string GetNcrPendingReportDepartment(NcrPendingCheckDto item)
    {
        var sourceText = EnumHelper.GetDisplayName<NcrPendingSourceType>(item.SourceType);
        var itemText = GetNcrInspectionItemDisplay(item.InspectionItem);
        return string.IsNullOrEmpty(itemText) ? sourceText : $"{sourceText}-{itemText}";
    }

    /// <summary>检验项目中文化（识别枚举转 Display，否则原样）</summary>
    private static string GetNcrInspectionItemDisplay(string? item)
    {
        if (string.IsNullOrEmpty(item)) return "";
        return Enum.TryParse<InspectionItem>(item, true, out var enumItem)
            ? DisplayHelper.GetInspectionItemText(enumItem)
            : item;
    }

    /// <summary>物料类型（过程检验按工序名判荒管/在制；成品检验按物料名解析，与 NcrForm 口径一致）</summary>
    private static string GetNcrPendingPipeCategoryText(NcrPendingCheckDto item)
    {
        if (item.SourceType == nameof(NcrPendingSourceType.ProcessInspection)
            || item.SourceType == nameof(NcrPendingSourceType.NonconformingFeedback))
        {
            // 不合格反馈按过程检验口径：圆棒穿孔→荒管，否则在制
            var category = string.Equals(item.ProcessName, ProcessKeys.RoughTubeProcessing, StringComparison.OrdinalIgnoreCase)
                ? MaterialType.RoughTube
                : MaterialType.WorkInProgress;
            return DisplayHelper.GetMaterialTypeText(category);
        }
        if (item.SourceType == nameof(NcrPendingSourceType.FinalInspection))
        {
            var category = string.IsNullOrEmpty(item.MaterialName)
                ? MaterialType.WorkInProgress
                : (Enum.TryParse<MaterialType>(item.MaterialName, true, out var mt) ? mt : MaterialType.WorkInProgress);
            return DisplayHelper.GetMaterialTypeText(category);
        }
        return "";
    }

    /// <summary>次品支数/重量单元格格式化：80支/565Kg，为 0 的部分省略，全 0 返回空串</summary>
    private static string FormatNcrCell(int quantity, int? weight)
    {
        var parts = new List<string>();
        if (quantity > 0) parts.Add($"{quantity}支");
        if (weight is > 0) parts.Add($"{weight}Kg");
        return string.Join("/", parts);
    }

    // ========== 通用数据解包 ==========

    private static T? OkData<T>(ApiResponse<T> r) => r.Success ? r.Data : default;

    // ========== 打印 ==========

    private async Task PrintTableAsync(string tableId, string title, string? cardKey = null)
    {
        // 卡片折叠时表格未渲染：先展开再打印（getTableHtml 依赖 DOM 中的表格）
        if (cardKey != null && IsCardCollapsed(cardKey))
        {
            _cardCollapsed[cardKey] = false;
            try { await LocalStorage.SetItemAsync(CollapsedStorageKey, _cardCollapsed); }
            catch { }
            StateHasChanged();
            await Task.Delay(100);
        }
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

    /// <summary>NCR 月度汇总横向 A4 打印（列宽，与 Ncrs 月度汇总打印口径一致）</summary>
    private async Task PrintNcrMonthlySummaryAsync()
    {
        if (_ncrMonthlyRows.Count == 0)
        {
            Snackbar.Add("暂无数据可打印", Severity.Warning);
            return;
        }
        // 卡片折叠时表格未渲染：先展开再打印（月度卡默认折叠）
        if (IsCardCollapsed("quality:ncr-monthly"))
        {
            _cardCollapsed["quality:ncr-monthly"] = false;
            try { await LocalStorage.SetItemAsync(CollapsedStorageKey, _cardCollapsed); }
            catch { }
            StateHasChanged();
            await Task.Delay(100);
        }
        try
        {
            var html = await JS.InvokeAsync<string>("getTableHtml", "#report-ncr-monthly-summary");
            if (!string.IsNullOrEmpty(html))
            {
                var printHtml = "<style>" +
                    "table{width:100%!important;table-layout:fixed!important;font-size:12px!important;border-collapse:collapse!important;}" +
                    "th,td{white-space:normal!important;padding:3px 4px!important;text-align:center!important;border:1px solid #333!important;}" +
                    "</style>" + html;
                await JS.InvokeVoidAsync("printRawHtml", printHtml, "不合格品月度汇总", "landscape");
            }
            else
            {
                Snackbar.Add("未找到可打印的汇总表格", Severity.Warning);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 格式化 ==========

    /// <summary>Tab1 流量行「汇总」列：本年 12 个月重量(kg)/金额(元)分别求和后按 x吨/y万 渲染。</summary>
    private static MarkupString RenderYearTotalCell(decimal[] weightKgByMonth, decimal[] amountYuanByMonth)
        => OrderOverviewFormatter.RenderInOutCell(weightKgByMonth.Sum(), amountYuanByMonth.Sum());

    // Tab4 采购待购（kg 取整，0 空）
    private static string FormatPendingWeight(decimal kg) => kg > 0 ? ((int)kg).ToString() : string.Empty;

    // Tab4 采购在购单元格「总量[*急量]」（t）
    private static string FormatInProgressCell(PurchaseInProgressCellDto cell)
    {
        var total = cell.TotalWeight / 1000m;
        if (total <= 0) return string.Empty;
        var s = total.ToString("F1");
        var urgent = cell.UrgentWeight / 1000m;
        return urgent > 0
            ? $"{s}[<span style=\"color:#d32f2f;font-weight:700;\">*</span>{urgent.ToString("F1")}]"
            : s;
    }
    private static MarkupString RenderInProgressCell(PurchaseInProgressCellDto cell) => new(FormatInProgressCell(cell));

    // Tab4 采购月度「购X/回Y」（t）
    private static string FormatPurchaseMonthlyCell(decimal buy, decimal ret)
    {
        if (buy <= 0 && ret <= 0) return string.Empty;
        var parts = new List<string>();
        if (buy > 0) parts.Add("购" + (buy / 1000m).ToString("F1"));
        if (ret > 0) parts.Add("回" + (ret / 1000m).ToString("F1"));
        return string.Join("/", parts);
    }
    private static string FormatNowInProgress(decimal kg) => kg > 0 ? (kg / 1000m).ToString("F1") : string.Empty;

    // Tab4 穿孔：吨(t)/kg 取整/月度「发X/回Y」
    private static string FormatTon(decimal kg) => kg > 0 ? (kg / 1000m).ToString("F1") : string.Empty;
    private static string FormatKg(decimal kg) => kg > 0 ? ((int)kg).ToString() : string.Empty;
    private static string FormatSendRecoverText(decimal send, decimal rec)
    {
        if (send <= 0 && rec <= 0) return string.Empty;
        var parts = new List<string>();
        if (send > 0) parts.Add("发" + (send / 1000m).ToString("F1"));
        if (rec > 0) parts.Add("回" + (rec / 1000m).ToString("F1"));
        return string.Join("/", parts);
    }

    // Tab5 近日/月度生产量（t）
    private static string FormatT(decimal kg) => kg > 0 ? (kg / 1000m).ToString("F1") : string.Empty;

    // Tab7 待检批支重汇总单元格「X批/Y支/Zkg」，全 0 显 "-"（与成检计划页口径一致）
    private static string RenderSummaryCell(int count, int quantity, decimal weight)
        => count == 0 && quantity == 0 && weight == 0
            ? "-"
            : $"{count}批/{quantity}支/{weight.ToString("G29")}kg";

    // Tab5 冷轧拔近日排程（复用冷轧计划页建议卡口径）
    /// <summary>档位显示：None 无计划显"-"，其余走标准中文</summary>
    private static string SuggestionTierText(string v)
        => v == "None" ? "-" : DisplayHelper.GetCompletionTypeText(v);

    /// <summary>组建议流转档显示：5060 拆档显示 [在制 X；成品 Y]，其余显示建议档名</summary>
    private static string SuggestionTierDisplay(ColdRollScheduleSuggestionDto group)
        => group.InProdTier != null && group.FinishedTier != null
            ? $"[在制 {SuggestionTierText(group.InProdTier)}；成品 {SuggestionTierText(group.FinishedTier)}]"
            : group.SuggestedTier;

    /// <summary>重量(kg) → 吨显示（保留 1 位小数，0 显 "0"）</summary>
    private static string TonsText(decimal kg) => kg > 0 ? (kg / 1000m).ToString("F1") : "0";

    /// <summary>
    /// 冷轧拔近日排程「后流转」格：「{目标组}量 X t / N 台」；
    /// 无目标组、或量与台数均为 0（避免出现「0t / 0台」误导）时显「-」（2026-09-10 用户决策）。
    /// </summary>
    private static string FlowStateText(FlowStateDto? state)
        => state is not null
           && !string.IsNullOrEmpty(state.TargetGroupDisplay)
           && (state.SupplyToTargetWeight != 0m || state.SupplyToTargetMachines != 0)
            ? $"{state.TargetGroupDisplay}量 {TonsText(state.SupplyToTargetWeight)}t / {state.SupplyToTargetMachines}台"
            : "-";

    // Tab5 实时委外在产单元格「总量/[流转]/[*特急]」（t）
    private static MarkupString FormatOutsourceCell(OutsourcePendingCellDto? cell)
    {
        if (cell == null || cell.Total <= 0) return new MarkupString("");
        var sb = new System.Text.StringBuilder((cell.Total / 1000m).ToString("F1"));
        if (cell.Flow > 0) sb.Append($"/[{(cell.Flow / 1000m).ToString("F1")}]");
        if (cell.Key > 0) sb.Append($"/[<span style=\"color:#d32f2f;font-weight:600;\">*{(cell.Key / 1000m).ToString("F1")}</span>]");
        return new MarkupString(sb.ToString());
    }

    // Tab5 月度委外「发X/回Y[退Z]」（t）
    private static string FormatSendRecoverText3(decimal send, decimal recover, decimal unprocessed)
    {
        if (send <= 0 && recover <= 0 && unprocessed <= 0) return string.Empty;
        var parts = new List<string>();
        if (send > 0) parts.Add("发" + (send / 1000m).ToString("F1"));
        if (recover > 0) parts.Add("回" + (recover / 1000m).ToString("F1"));
        if (unprocessed > 0) parts.Add("[退" + (unprocessed / 1000m).ToString("F1") + "]");
        return string.Join("/", parts);
    }
    private static string FormatNowInProduction(decimal weight) => weight > 0 ? (weight / 1000m).ToString("F1") : string.Empty;

    // Tab5 段落/工段流转
    private static string RenderInt(decimal? val) => val.HasValue ? Math.Round(val.Value, 0).ToString() : "-";
    private static Color GetStatusColor(string? status) => status switch
    {
        SustainStatusKeys.Excessive => Color.Error,
        SustainStatusKeys.Insufficient => Color.Warning,
        SustainStatusKeys.Normal => Color.Success,
        _ => Color.Default
    };
    private static Color GetPlanFlowJudgmentColor(string? judgment) => judgment == PlanFlowJudgmentKeys.Accelerate ? Color.Error : Color.Default;

    // Tab3 待投料矩阵（单数 + 待投料吨）
    private static string FormatMatrixPending(int count, decimal weight)
        => count > 0 ? $"{count} 单 / {weight / 1000m:F1}吨" : "-";
    private static string FormatMatrixPurchase(int count, decimal weight)
        => count > 0 ? $"{count} 单 / {weight / 1000m:F1}吨" : "-";

    /// <summary>
    /// 成购矩阵唯一有值的行号（=「执行用料计划」档）。成购只是该档的一个分支，
    /// 其余 3 档恒为 0 不渲染（2026-09-10 用户决策，防止误读为「成购横跨 4 档」）。
    /// </summary>
    private static int PurchaseMatrixRowIndex => Array.IndexOf(RawMaterialLockRemarkKeys.All, RawMaterialLockRemarkKeys.ExecutePlan);

    // Tab3 截日（吨）
    private static string FormatCutoffCell(decimal kg) => kg > 0 ? $"{(kg / 1000m).ToString("F1")}吨" : "-";
}
