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
    // 方案B（生产执行/质量管理/物料执行）：默认折叠「月度/历史」类低频卡，核心实时卡展开
    // 业务总况/原料需求 为单区域大表 Tab（非多卡），不参与折叠
    // 现订单负荷总量/仓库报表 为嵌入组件 Tab，无可打印汇总卡，不参与折叠

    /// <summary>卡折叠状态字典：key=卡片标识 → true=折叠；未记录时取默认折叠集</summary>
    private readonly Dictionary<string, bool> _cardCollapsed = new(StringComparer.OrdinalIgnoreCase);
    private const string CollapsedStorageKey = "reportoverview_card_collapsed";

    /// <summary>默认折叠的卡（首次进入/未持久化时的展示策略）</summary>
    private static readonly HashSet<string> DefaultCollapsedCards = new(StringComparer.OrdinalIgnoreCase)
    {
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

    private void RefreshTab(int index)
    {
        if (index == 1 || index == 6) { _refreshKey++; return; }
        if (!_loaded[index]) return;
        _loaded[index] = false;
        _failed[index] = false;
        _ = ActivateTabAsync(index);
    }

    // ========== Tab1 业务总况：订单接单·出库及现负荷汇总 ==========

    private OrderInOutSummaryDto? _inOutSummary;
    private int _currentMonthIndex => DateTime.Today.Month - 1;

    /// <summary>订单交期预估（两小表：订单完成预估 / 延期交货订单预估，x单/y吨，订单级口径）</summary>
    private OrderDeliveryEstimateDto? _deliveryEstimate;

    private async Task LoadTab1Async()
    {
        var t1 = OrderService.GetInOutSummaryAsync(DateTime.Today.Year);
        var t2 = OrderService.GetDeliveryEstimateAsync();
        await Task.WhenAll(t1, t2);

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
    private NcrMonthlySummaryDto? _ncrMonthlySummary;
    private List<NcrMonthlyRowDto> _ncrMonthlyRows = new();
    private List<int> _ncrCategoryRowspans = new();
    private List<int> _ncrDeptRowspans = new();
    private List<(int Qty, int? Weight)> _ncrDeptTotals = new();
    private List<(int Qty, int? Weight)> _ncrCategoryTotals = new();

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

    /// <summary>计算月度汇总三级合并 rowspan（后端已按 责任类别→责任部门→处置方式 排序，同组相邻）+ 部门/类别全年合计</summary>
    private void ComputeNcrMonthlyRowspans()
    {
        _ncrCategoryRowspans = new List<int>(new int[_ncrMonthlyRows.Count]);
        _ncrDeptRowspans = new List<int>(new int[_ncrMonthlyRows.Count]);
        _ncrDeptTotals = new List<(int, int?)>(new (int, int?)[_ncrMonthlyRows.Count]);
        _ncrCategoryTotals = new List<(int, int?)>(new (int, int?)[_ncrMonthlyRows.Count]);

        var i = 0;
        while (i < _ncrMonthlyRows.Count)
        {
            var category = _ncrMonthlyRows[i].ResponsibilityCategory;
            var catCount = 1;
            while (i + catCount < _ncrMonthlyRows.Count
                   && string.Equals(_ncrMonthlyRows[i + catCount].ResponsibilityCategory, category, StringComparison.Ordinal))
                catCount++;
            _ncrCategoryRowspans[i] = catCount;
            _ncrCategoryTotals[i] = (
                _ncrMonthlyRows.Skip(i).Take(catCount).Sum(r => r.TotalQuantity),
                _ncrMonthlyRows.Skip(i).Take(catCount).Sum(r => r.TotalWeight ?? 0));

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
                _ncrDeptTotals[j] = (
                    _ncrMonthlyRows.Skip(j).Take(deptCount).Sum(r => r.TotalQuantity),
                    _ncrMonthlyRows.Skip(j).Take(deptCount).Sum(r => r.TotalWeight ?? 0));
                j += deptCount;
            }

            i += catCount;
        }
    }

    // ========== NCR 显示 ==========

    /// <summary>反馈部门 = 来源 + 检验项目（中文化，与 NcrForm 自动填充口径一致）</summary>
    private static string GetNcrPendingReportDepartment(NcrPendingCheckDto item)
    {
        var sourceText = EnumHelper.GetDisplayName<ReportTemplateType>(item.SourceType);
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

    // Tab1 接单/出库/库存（t）
    private static string FormatInOutWeight(decimal kg) => kg == 0m ? "-" : $"{kg / 1000m:F1}";

    // Tab1 订单交期预估（两小表，x单/y吨，急中急子集 [*a/b] 标红）
    private static MarkupString FormatDeliveryBucket(OrderDeliveryBucketDto b)
    {
        if (b.Count <= 0 && b.Weight <= 0) return new MarkupString("-");
        var s = $"{b.Count}单/{b.Weight.ToString("F1")}吨";
        if (b.UrgentCount > 0 || b.UrgentWeight > 0)
            s += $"[<span style=\"color:#d32f2f;font-weight:700;\">*{b.UrgentCount}/{b.UrgentWeight.ToString("F1")}</span>]";
        return new MarkupString(s);
    }

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

    /// <summary>重量(kg) → 吨显示（G29 去零）</summary>
    private static string TonsText(decimal kg) => kg > 0 ? (kg / 1000m).ToString("G29") : "0";

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
    private static string RenderDecimal(decimal? val) => val.HasValue ? val.Value.ToString("F1") : "-";
    private static Color GetStatusColor(string? status) => status switch
    {
        "偏少" => Color.Error,
        "过多" => Color.Warning,
        "正常" => Color.Success,
        _ => Color.Default
    };
    private static Color GetPlanFlowJudgmentColor(string? judgment) => judgment == "加速" ? Color.Warning : Color.Default;

    // Tab3 待投料矩阵（单数 + 待投料吨）
    private static string FormatMatrixPending(int count, decimal weight)
        => count > 0 ? $"{count} 单 / {weight / 1000m:F1}吨" : "-";
    private static string FormatMatrixPurchase(int count, decimal weight)
        => count > 0 ? $"{count} 单 / {weight / 1000m:F1}吨" : "-";

    // Tab3 截日（吨）
    private static string FormatCutoffCell(decimal kg) => kg > 0 ? $"{(kg / 1000m).ToString("F1")}吨" : "-";
}
