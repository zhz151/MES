using MES.Core.Constants;
using MES.Core.Enums;
using MES.Data.Entities.Batch;
using MES.Data.Entities.WorkOrder;
using MES.Services.Extensions;

namespace MES.Services.Helpers;

/// <summary>
/// 「生产执行 8 节点待量」共享计算器 —— 原 G17（<c>WorkOrderExecutionService.ComputeSummary</c> 内联块）与本类同源，
/// 消除「工单执行快照」与「订单进度树」两侧的口径漂移。
///
/// 共用方：
/// 1) <c>WorkOrderExecutionService</c>：工单执行读模型刷新，把结果写入
///    <see cref="WorkOrderExecutionSummary"/> 的 8 个 <c>PendingSection*</c> 字段（快照口径）；
/// 2) <c>OrderProgressQueryService</c>：订单进度树「生产执行」分支，实时重算 + 顺带带出各节点批次名单。
///
/// ⚠️ 前置条件：入参批次的 <see cref="ProductionBatch.ProcessGroups"/> 必须已加载
/// （查询侧须 <c>Include(b =&gt; b.ProcessGroups)</c>）。未加载时该集合为空 → 全部批次判定为「不适用」→ 8 节点待量恒为 0，
/// **不会抛异常**，属静默口径错误。
/// </summary>
public static class ProductionPendingNodeHelper
{
    /// <summary>
    /// 8 个固定节点单源表：(叶子 Key, 中文标签, 工序组名, 工段名)。
    /// ⚠️ 顺序 = 原 G17 <c>nodeDefs</c> 顺序，同时是 <c>ProductionAttentionProcess</c> 取最小序号工序时的 tie-break 依据，
    /// 任何重排都会让「生产关注工序」漂移，禁止改动顺序。
    /// 本表同时取代原 <c>OrderProgressQueryService.ProductionNodeDefs</c>（那份只含 Key/Label）。
    /// </summary>
    public static readonly (string Key, string Label, string ProcessName, string SectionName)[] NodeDefs =
    [
        (ProcessKeys.RoughTubeProcessing, "荒管处理", ProcessKeys.RoughTubeProcessing, SectionKeys.OuterPolish),
        (ProcessKeys.InProcessRepair, "在制修检", ProcessKeys.InProcessRepair, SectionKeys.Inspection),
        (ProcessKeys.ColdRoll60, "60冷轧", ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw),
        (ProcessKeys.ColdRoll50, "50冷轧", ProcessKeys.ColdRoll50, SectionKeys.ColdRollDraw),
        (ProcessKeys.ColdRoll30, "30冷轧", ProcessKeys.ColdRoll30, SectionKeys.ColdRollDraw),
        (ProcessKeys.ColdRoll20, "20冷轧", ProcessKeys.ColdRoll20, SectionKeys.ColdRollDraw),
        (ProcessKeys.ThreeRollColdRoll, "三辊冷轧", ProcessKeys.ThreeRollColdRoll, SectionKeys.ColdRollDraw),
        (ProcessKeys.ColdDraw, "冷拔", ProcessKeys.ColdDraw, SectionKeys.ColdRollDraw),
    ];

    /// <summary>节点名单中的批次引用（重量为该批 CurrentValidWeight，未填按 0）</summary>
    public sealed record BatchRef(int BatchId, string BatchNo, decimal WeightKg);

    /// <summary>
    /// 单节点待量：<paramref name="TotalKg"/> = 在产 + 在途合计（= 该节点「待产」量，语义为「尚未完成此节点」）。
    /// <paramref name="InProgress"/> = 批次已在本节点工序组内（batchCurrentSeq == targetSeq）；
    /// <paramref name="InTransit"/> = 批次尚未做到本节点工序组（batchCurrentSeq &lt; targetSeq）。
    /// 恒有 Σ在产 + Σ在途 == TotalKg（分段只是对同一命中集合分区）。
    /// 两段名单均按生产编号 Ordinal 升序（稳定可测，不依赖 ProcessGroups 组序）。
    /// </summary>
    public sealed record NodePending(
        decimal TotalKg,
        IReadOnlyList<BatchRef> InProgress,
        IReadOnlyList<BatchRef> InTransit);

    /// <summary>
    /// 参与「生产执行 8 节点待量」的批次：排除「成检 / 完成」两态（原 G17 <c>group14Batches</c> 原义）。
    /// </summary>
    public static List<ProductionBatch> ActiveBatches(IEnumerable<ProductionBatch> batches)
        => batches.Where(b => b.Status != BatchStatus.InFinalInspection && b.Status != BatchStatus.Completed).ToList();

    /// <summary>
    /// 按 8 节点计算待量（返回 Key → <see cref="NodePending"/>，OrdinalIgnoreCase）。
    /// <paramref name="batches"/> 应已由 <see cref="ActiveBatches"/> 过滤（方法内保留「已完成 → 跳过」的二次守卫，与原实现一致）。
    /// 本方法为纯内存计算，可对「同主号全部批次的并集」一次调用（逐批可加）。
    /// </summary>
    public static Dictionary<string, NodePending> Compute(IReadOnlyList<ProductionBatch> batches)
    {
        // 预置容器：每节点一段在产 + 一段在途（避免命中时反复建集合）
        var inProgress = new Dictionary<string, List<BatchRef>>(StringComparer.OrdinalIgnoreCase);
        var inTransit = new Dictionary<string, List<BatchRef>>(StringComparer.OrdinalIgnoreCase);
        var totals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in NodeDefs)
        {
            inProgress[def.Key] = new List<BatchRef>();
            inTransit[def.Key] = new List<BatchRef>();
            totals[def.Key] = 0m;
        }

        foreach (var batch in batches)
        {
            if (batch.ProcessGroups == null || batch.ProcessGroups.Count == 0)
                continue;

            // 已完成的批次视为到达所有节点
            if (batch.Status == BatchStatus.Completed)
                continue;

            // 构建本批次 ProcessName → SequenceNumber 映射
            var pgMap = batch.ProcessGroups
                .Where(pg => !string.IsNullOrEmpty(pg.ProcessName))
                .ToDictionary(pg => pg.ProcessName, pg => pg.SequenceNumber, StringComparer.OrdinalIgnoreCase);

            // 获取批次当前工序的 SequenceNumber（未投产 = 0）
            int batchCurrentSeq = 0;
            if (!string.IsNullOrEmpty(batch.CurrentGroupName))
            {
                var currentSeqOpt = batch.ProcessGroups
                    .Where(pg => pg.ProcessName.Equals(batch.CurrentGroupName, StringComparison.OrdinalIgnoreCase))
                    .Select(pg => pg.SequenceNumber)
                    .Cast<int?>()
                    .FirstOrDefault();
                batchCurrentSeq = currentSeqOpt ?? 0;
            }

            var batchWeight = batch.CurrentValidWeight ?? 0m;
            var batchRef = new BatchRef(batch.Id, batch.BatchNo, batchWeight);

            foreach (var (key, _, pn, sn) in NodeDefs)
            {
                // 该批次无此工序组 → 节点不适用
                if (!pgMap.TryGetValue(pn, out var targetSeq))
                    continue;

                if (batchCurrentSeq < targetSeq)
                {
                    // 批次未到达此工序组 → 检查该工序组是否确实包含目标工段
                    // 仅当该工序组定义了目标工段时才计入待量
                    var targetPg = batch.ProcessGroups
                        .FirstOrDefault(pg => pg.ProcessName.Equals(pn, StringComparison.OrdinalIgnoreCase));
                    if (targetPg == null) continue;

                    var targetSectionSeq = targetPg.GetSectionSequence(sn);
                    if (targetSectionSeq == null) continue; // 该工序组不含此工段

                    totals[key] += batchWeight;
                    inTransit[key].Add(batchRef);
                }
                else if (batchCurrentSeq == targetSeq)
                {
                    // === 1. 工段级到达检查：荒管处理·外抛光、在制修检·检验 ===
                    // 批次已到达此工序组但尚未到达指定工段时，仍需计入待量
                    if (pn is ProcessKeys.RoughTubeProcessing or ProcessKeys.InProcessRepair)
                    {
                        var targetPg = batch.ProcessGroups
                            .FirstOrDefault(pg => pg.ProcessName.Equals(pn, StringComparison.OrdinalIgnoreCase));
                        if (targetPg == null) continue;

                        // 获取目标工段在该工序组中的执行序号（如 OuterPolish=5）
                        var targetSectionSeq = targetPg.GetSectionSequence(sn);
                        if (targetSectionSeq == null) continue; // 该工序组不含此工段

                        // 批次无当前工段 → 在工序组内但未开始任何工段 → 计入待量
                        if (string.IsNullOrEmpty(batch.CurrentSectionName))
                        {
                            totals[key] += batchWeight;
                            inProgress[key].Add(batchRef);
                            continue;
                        }

                        // 批次当前不在该工序组 → 已越过（例如已到后续工序）→ 不计
                        if (batch.CurrentGroupName == null ||
                            !batch.CurrentGroupName.Equals(pn, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // 均在同一工序组内，比较工段执行序号
                        var currentSectionSeq = targetPg.GetSectionSequence(batch.CurrentSectionName);
                        if (currentSectionSeq == null || currentSectionSeq.Value < targetSectionSeq.Value)
                        {
                            // 当前工段序号 < 目标工段序号 → 尚未到达目标工段 → 计入待量
                            totals[key] += batchWeight;
                            inProgress[key].Add(batchRef);
                        }
                        continue;
                    }

                    // === 3. 冷轧/冷拔系列 — 检查批次是否正在做指定工段且未完成 ===
                    var isAtSection = batch.CurrentGroupName != null
                        && batch.CurrentGroupName.Equals(pn, StringComparison.OrdinalIgnoreCase)
                        && batch.CurrentSectionName == sn;

                    if (isAtSection && batch.CurrentSectionCompleted != true)
                    {
                        totals[key] += batchWeight;
                        inProgress[key].Add(batchRef);
                    }
                }
            }
        }

        var result = new Dictionary<string, NodePending>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in NodeDefs)
        {
            result[def.Key] = new NodePending(
                totals[def.Key],
                SortByBatchNo(inProgress[def.Key]),
                SortByBatchNo(inTransit[def.Key]));
        }
        return result;
    }

    /// <summary>段内批次按生产编号 Ordinal 升序（显式排序，勿依赖集合插入序）</summary>
    private static List<BatchRef> SortByBatchNo(List<BatchRef> batches)
        => batches.OrderBy(b => b.BatchNo, StringComparer.Ordinal).ToList();

    /// <summary>节点 Key → 待量合计（供「生产关注工序」等按工序名取值的旧写法沿用）</summary>
    public static Dictionary<string, decimal> Totals(IReadOnlyDictionary<string, NodePending> pending)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in NodeDefs)
            result[def.Key] = pending.TryGetValue(def.Key, out var node) ? node.TotalKg : 0m;
        return result;
    }

    /// <summary>
    /// 把计算结果写入 <see cref="WorkOrderExecutionSummary"/> 的 8 个 <c>PendingSection*</c> 字段
    /// （赋值为 0 时写 null，与原实现一致）。
    /// </summary>
    public static void ApplyTo(WorkOrderExecutionSummary summary, IReadOnlyDictionary<string, NodePending> pending)
    {
        decimal Val(string key) => pending.TryGetValue(key, out var node) ? node.TotalKg : 0m;

        summary.PendingSectionRoughTube = Val(ProcessKeys.RoughTubeProcessing) > 0 ? Val(ProcessKeys.RoughTubeProcessing) : null;
        summary.PendingSectionWarehouseFix = Val(ProcessKeys.InProcessRepair) > 0 ? Val(ProcessKeys.InProcessRepair) : null;
        summary.PendingSection60Roll = Val(ProcessKeys.ColdRoll60) > 0 ? Val(ProcessKeys.ColdRoll60) : null;
        summary.PendingSection50Roll = Val(ProcessKeys.ColdRoll50) > 0 ? Val(ProcessKeys.ColdRoll50) : null;
        summary.PendingSection30Roll = Val(ProcessKeys.ColdRoll30) > 0 ? Val(ProcessKeys.ColdRoll30) : null;
        summary.PendingSection20Roll = Val(ProcessKeys.ColdRoll20) > 0 ? Val(ProcessKeys.ColdRoll20) : null;
        summary.PendingSectionThreeRoll = Val(ProcessKeys.ThreeRollColdRoll) > 0 ? Val(ProcessKeys.ThreeRollColdRoll) : null;
        summary.PendingSectionDrawBench = Val(ProcessKeys.ColdDraw) > 0 ? Val(ProcessKeys.ColdDraw) : null;
    }
}
