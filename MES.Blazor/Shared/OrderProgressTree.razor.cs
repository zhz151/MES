using Microsoft.AspNetCore.Components;
using MES.Core.Constants;
using MES.Core.DTOs.Order;
using MES.Core.Helpers;
using MES.Blazor.Helpers;
using MudBlazor;

namespace MES.Blazor.Shared;

/// <summary>
/// 订单进度树：一级=订单号，二级=订单号+主号，三级=阶段分支，四级=叶子重量(kg)，
/// 五级=叶子下的批次名单（默认折叠，点摘要行展开；生产执行叶按「在产 / 在途」两段、成品检验叶单段；
/// 批次号可点击弹「批次执行进度」，见 <see cref="OpenBatchProgressAsync"/>）。
/// 三级分支分两套：非完结=原料锁定/生产执行/成品检验/订单成品入库；
/// 完结=生产投料/在制品入库/次品入库/备料成品/订单成品入库（用户拍板「投料+产出」两维度口径）。
/// 头部信息两行化：订单头=订单号/签订/业务员/客户/交期截止/延期罚款/总重/项次数；
/// 主号头行1=主号号(X01)+规格要素，行2=执行关注/紧急性/预计完成；二级按主号从小到大混排（含完结）。
/// 本组件只渲染传进来的 <see cref="Tree"/>，取数与页头（打印/返回）留在使用方。
/// ⚠️ 前提：本组件渲染于 <c>MudPaper</c>/<c>MudCard</c> 内（首页卡、/orders/progress 页），**不在任何 MudDialog 内**，
/// 故批次号弹窗可直接弹根级对话框；若将来把本组件塞进弹窗，需重新评估嵌套弹窗行为。
/// </summary>
public partial class OrderProgressTree
{
    /// <summary>弹「批次执行进度」用（api/batch/{id}/tracking 已放宽为仅需登录，无需权限门控）</summary>
    [Inject] private IDialogService DialogService { get; set; } = null!;

    /// <summary>进度树数据（使用方查询后传入；null 时本组件不渲染任何内容）</summary>
    [Parameter] public OrderProgressTreeDto? Tree { get; set; }

    /// <summary>
    /// 紧凑态（首页「订单进度查询」卡）：**全部主号默认折叠**，只出订单头 + 主号汇总行，点主号才展开分支与重量叶。
    /// false（订单进度页 /orders/progress）＝原行为：非完结主号默认展开、完结主号默认折叠。
    /// </summary>
    [Parameter] public bool Compact { get; set; }

    /// <summary>已折叠主号集合（点击主号标题行切换）</summary>
    private readonly HashSet<string> _collapsedMains = new(StringComparer.Ordinal);

    /// <summary>
    /// 已按哪一份树初始化过默认折叠态。用引用比较挡住父组件重渲染：否则用户手动展开的主号
    /// 会在下一次 OnParametersSet 被重置回默认折叠（同一份树实例期间不重算）。
    /// </summary>
    private OrderProgressTreeDto? _collapsedInitializedFor;

    /// <summary>
    /// 已展开的名单叶子集合（空集 = 全部折叠）。用「已展开」而非「已折叠」：
    /// 默认空集即全折叠，换单/重查时清空自动回落折叠态，无需为新叶子预置 key。
    /// key = LeafKey(mainNo, branchKey, leafKey)。
    /// </summary>
    private readonly HashSet<string> _expandedLeaves = new(StringComparer.Ordinal);

    protected override void OnParametersSet()
    {
        if (Tree is null || ReferenceEquals(_collapsedInitializedFor, Tree))
            return;

        _collapsedInitializedFor = Tree;
        _collapsedMains.Clear();
        _expandedLeaves.Clear();
        foreach (var main in Tree.MainNos)
        {
            if (Compact || main.IsCompleted)
                _collapsedMains.Add(main.ProductionMainNo);
        }
    }

    private bool IsCollapsed(string mainNo) => _collapsedMains.Contains(mainNo);

    private void ToggleMain(string mainNo)
    {
        if (!_collapsedMains.Add(mainNo))
            _collapsedMains.Remove(mainNo);
    }

    /// <summary>名单叶子唯一键（主号 | 分支 Key | 叶子 Key；分支/叶子 Key 在其父级内唯一）</summary>
    private static string LeafKey(string mainNo, string branchKey, string leafKey)
        => $"{mainNo}|{branchKey}|{leafKey}";

    private bool IsLeafExpanded(string leafKey) => _expandedLeaves.Contains(leafKey);

    private void ToggleLeaf(string leafKey)
    {
        if (!_expandedLeaves.Add(leafKey))
            _expandedLeaves.Remove(leafKey);
    }

    /// <summary>折叠态摘要：各段「标签 N 批」以「 · 」相连（如「在产 14 批 · 在途 41 批」）</summary>
    private static string LeafSummary(MainProgressLeafDto leaf)
        => string.Join(" · ", leaf.BatchSegments.Select(s => $"{s.Label} {s.BatchCount} 批"));

    /// <summary>
    /// 弹「批次执行进度」。⚠️ MudBlazor 6.19 的 MaxWidth.ExtraLarge=1920px，叠加 FullWidth 会满屏留白，
    /// 故固定 Large 且不开 FullWidth（同 <c>FinalInspectionPlan.OpenBatchProgressAsync</c>）。
    /// </summary>
    private async Task OpenBatchProgressAsync(int batchId, string? batchNo)
    {
        if (batchId <= 0)
            return;

        var parameters = new DialogParameters
        {
            { nameof(BatchProgressDialog.BatchId), batchId },
            { nameof(BatchProgressDialog.BatchNo), batchNo }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Large, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<BatchProgressDialog>("批次执行进度", parameters, options);
    }

    /// <summary>
    /// 主号分支按固定语义序：
    /// 非完结=原料锁定 → 生产执行 → 成品检验 → 订单成品入库；
    /// 完结=生产投料 → 在制品入库 → 次品入库 → 备料成品 → 订单成品入库（用户拍板序，固定不因空分支跳过占位）。
    /// </summary>
    private static IEnumerable<MainProgressBranchDto> BranchesOf(OrderMainProgressDto main)
    {
        if (main.IsCompleted)
        {
            if (main.ProductionInput != null) yield return main.ProductionInput;
            if (main.SurplusInbound != null) yield return main.SurplusInbound;
            if (main.DefectInbound != null) yield return main.DefectInbound;
            if (main.FinishedStockInbound != null) yield return main.FinishedStockInbound;
            if (main.Warehousing != null) yield return main.Warehousing;
            yield break;
        }

        if (main.RawMaterialLock != null) yield return main.RawMaterialLock;
        if (main.Production != null) yield return main.Production;
        if (main.FinalInspection != null) yield return main.FinalInspection;
        if (main.Warehousing != null) yield return main.Warehousing;
    }

    private static string StageText(int stage) => IntStatusDisplayHelper.GetScheduleStageText(stage);

    private static string? UrgencyText(string? key) => UrgencyLevelKeys.ToChinese(key);

    /// <summary>
    /// 「投料产出」汇总分段（用户 2026-09-14 拍板）。口径：
    /// ① 投料 = 投料叶 − 退货（退货挂次品叶，见 DTO `ReturnWeightKg`）；
    /// ② 订单成品入库 = 订单成品入库分支「入库」叶；
    /// ③ 余次备产出入库 = 在制品入库 + 次品入库 + 备料成品 三支入库叶合计**再减退货**；
    /// ④ 投料产出率 = (订单成品入库 + 余次备产出入库) ÷ 投料；⑤ 产出成品比 = 订单成品入库 ÷ 总产出。
    /// 三项重量全为 0 → 返回 null（不渲染该行）；分母 ≤ 0 的比率项不渲染。两项比率 Highlight=true（前端高亮）。
    /// </summary>
    private static List<OpSummarySegment>? BuildCompletionSummary(
        string inputLabel, decimal inputNet, decimal orderFinishedIn, decimal otherInbound)
    {
        if (inputNet <= 0m && orderFinishedIn <= 0m && otherInbound <= 0m)
            return null;

        var outputTotal = orderFinishedIn + otherInbound;
        var segments = new List<OpSummarySegment>
        {
            new($"{inputLabel}{FmtKg(inputNet)}kg"),
            new($"订单成品入库{FmtKg(orderFinishedIn)}kg"),
            new($"余次备产出入库{FmtKg(otherInbound)}kg"),
        };
        if (inputNet > 0m)
            segments.Add(new($"投料产出率{FormatPercent(outputTotal / inputNet)}", true));
        if (outputTotal > 0m)
            segments.Add(new($"产出成品比{FormatPercent(orderFinishedIn / outputTotal)}", true));

        return segments;
    }

    /// <summary>主号级「投料产出」汇总（投料项带生产类型后缀，如「生产投料[在制生产]」）</summary>
    private static List<OpSummarySegment>? MainCompletion(OrderMainProgressDto main)
    {
        var (inputNet, orderFinishedIn, otherInbound) = Nets(main);
        return BuildCompletionSummary(main.ProductionInput?.Title ?? "生产投料", inputNet, orderFinishedIn, otherInbound);
    }

    /// <summary>
    /// 订单级「投料产出」汇总（用户 2026-09-14 拍板）：仅当该订单下主号非空且**全部已完结**时渲染；
    /// 各主号三项净量直接求和（跨主号聚合，故投料项不加生产类型后缀）。
    /// </summary>
    private List<OpSummarySegment>? OrderCompletion()
    {
        if (Tree?.MainNos is not { Count: > 0 } mains || !mains.All(m => m.IsCompleted))
            return null;

        decimal inputNet = 0m, orderFinishedIn = 0m, otherInbound = 0m;
        foreach (var main in mains)
        {
            var (input, finished, other) = Nets(main);
            inputNet += input;
            orderFinishedIn += finished;
            otherInbound += other;
        }

        return BuildCompletionSummary("生产投料", inputNet, orderFinishedIn, otherInbound);
    }

    /// <summary>单主号三项净量：投料（投料叶−退货）/ 订单成品入库 / 余次备产出入库（三支入库叶合计−退货）</summary>
    private static (decimal InputNet, decimal OrderFinishedIn, decimal OtherInbound) Nets(OrderMainProgressDto main)
    {
        var defectInbound = main.DefectInbound?.Leaves.Sum(l => l.WeightKg) ?? 0m;
        var returnWeight = main.DefectInbound?.Leaves.Sum(l => l.ReturnWeightKg) ?? 0m;

        var inputNet = LeafWeight(main.ProductionInput, "Input") - returnWeight;
        var orderFinishedIn = LeafWeight(main.Warehousing, "Inbound");
        var otherInbound = LeafWeight(main.SurplusInbound, "Inbound")
            + defectInbound + LeafWeight(main.FinishedStockInbound, "Inbound") - returnWeight;
        return (inputNet, orderFinishedIn, otherInbound);
    }

    /// <summary>投料产出汇总渲染段（Text=段文本；Highlight=true 时前端高亮，用于两项比率）</summary>
    private sealed record OpSummarySegment(string Text, bool Highlight = false);

    /// <summary>取分支指定 Key 叶的重量（分支或叶不存在按 0）</summary>
    private static decimal LeafWeight(MainProgressBranchDto? branch, string key)
        => branch?.Leaves.FirstOrDefault(l => l.Key == key)?.WeightKg ?? 0m;

    /// <summary>比率 → 百分比一位小数（如 0.9 → "90.0%"），四舍五入 AwayFromZero</summary>
    private static string FormatPercent(decimal ratio)
        => Math.Round(ratio * 100m, 1, MidpointRounding.AwayFromZero).ToString("0.0") + "%";

    /// <summary>执行关注色 class（语义沿用原 chip 配色：暂停红/原料锁橙/生产蓝/成检靛）</summary>
    private static string StageTextClass(int stage) => stage switch
    {
        0 => "op-v-pause",
        2 => "op-v-lock",
        3 => "op-v-prod",
        4 => "op-v-inspect",
        _ => "",
    };

    /// <summary>紧急性色 class（A+红/A橙/B蓝）</summary>
    private static string UrgencyClass(string? key) => key switch
    {
        UrgencyLevelKeys.APlusUrgent => "op-v-u-aplus",
        UrgencyLevelKeys.AUrgent => "op-v-u-a",
        UrgencyLevelKeys.BOrder => "op-v-u-b",
        _ => "",
    };

    /// <summary>长度状态中文（存枚举名字符串）</summary>
    private static string LengthText(string? lengthStatus) => DisplayHelper.GetLengthStatusText(lengthStatus);

    /// <summary>交货状态中文（存枚举名字符串）</summary>
    private static string DeliveryStateText(string? deliveryState) => DisplayHelper.GetDeliveryStateText(deliveryState);

    /// <summary>日期 yyyy-MM-dd</summary>
    private static string FmtDate(DateTime? value) => DisplayHelper.FormatNullableDate(value);

    /// <summary>延期罚款 有/无</summary>
    private static string PenaltyText(bool value) => value ? "有" : "无";

    /// <summary>重量整数化显示（去小数位，列表页 §10.7 同款截断口径）</summary>
    private static string FmtKg(decimal kg) => DisplayHelper.FormatDecimalAsInt(kg);
}
