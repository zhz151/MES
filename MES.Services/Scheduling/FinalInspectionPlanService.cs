using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Core.Interfaces.Scheduling;
using MES.Data;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Order;
using MES.Data.Entities.Quality;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Services.Printing;

namespace MES.Services.Scheduling;

/// <summary>
/// 成检计划服务 — 三档分组：待到料/待检验/检验中
/// 候选口径：批次状态 == InFinalInspection（成检，已完成生产、处于成品检验阶段）的批次。
/// 行粒度 = 生产编号 + 成检类型（成检类型取批次「成检附加」InspectionStage，空默认正式成检）。
/// </summary>
public class FinalInspectionPlanService : IFinalInspectionPlanService
{
    private readonly AppDbContext _context;

    public FinalInspectionPlanService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<FinalInspectionPlanDto>> GetKanbanAsync()
    {
        // === 1. MaterialReceiveCheck（含强制完成）按「批次+成检类型」归一化 ===
        // 强制完成到料 = 到料后执行有特殊情况，由批次首页通知引导转「完成」→ 该批次脱离成检候选。
        // 本看板对这些批次行主动跳过（强制完成 ≠ 待到料/待检验/检验中，且不应作为「完成检验待入库」）。
        var receiveChecks = await _context.MaterialReceiveChecks
            .AsNoTracking()
            .Select(rc => new { rc.ProductionBatchId, rc.ReceiveDate, rc.InspectionType, rc.IsForceCompleted })
            .ToListAsync();

        var forcedKeys = receiveChecks
            .Where(rc => rc.IsForceCompleted)
            .Select(rc => (rc.ProductionBatchId, NormalizeInspectionType(rc.InspectionType)))
            .ToHashSet();

        var receivedKeys = receiveChecks
            .Where(rc => !rc.IsForceCompleted)
            .Select(rc => (rc.ProductionBatchId, NormalizeInspectionType(rc.InspectionType)))
            .ToHashSet();
        // 同一批次+类型可能存在多条到料记录，按键分组取最近到料日期，避免 ToDictionary 重复键崩溃
        // 值类型用 DateTime? 保证缺键时 GetValueOrDefault 返回 null 而非 0001-01-01
        var receiveDateMap = receiveChecks
            .Where(rc => !rc.IsForceCompleted)
            .GroupBy(rc => (rc.ProductionBatchId, NormalizeInspectionType(rc.InspectionType)))
            .ToDictionary(g => g.Key, g => g.Max(rc => (DateTime?)rc.ReceiveDate));

        // === 2. FinalInspections 最大检验日期（按「批次+成检类型」）===
        // ⚠️ NormalizeInspectionType 是 C# 私有方法，IQueryable 内 GroupBy 键表达式无法翻译成 SQL（真库会抛 500，InMemory 测不出）
        // 必须先取原始行 ToList 后再内存归一化分组
        var inspectionRows = await _context.FinalInspections
            .AsNoTracking()
            .Select(fi => new { fi.ProductionBatchId, fi.InspectionType, fi.InspectionDate })
            .ToListAsync();

        var inspectionAgg = inspectionRows
            .GroupBy(fi => (BatchId: fi.ProductionBatchId, Type: NormalizeInspectionType(fi.InspectionType)))
            .Select(g => new { ProductionBatchId = g.Key.BatchId, Type = g.Key.Type, MaxDate = g.Max(fi => (DateTime?)fi.InspectionDate) })
            .ToList();

        var inspectedKeys = inspectionAgg
            .Select(a => (a.ProductionBatchId, a.Type))
            .ToHashSet();
        var inspectionDateMap = inspectionAgg
            .GroupBy(a => (a.ProductionBatchId, a.Type))
            .ToDictionary(g => g.Key, g => g.Max(a => a.MaxDate));

        // === 3. InventoryBatch 已入库 ProductionBatchNo 集合 ===
        var warehousedBatchNos = await _context.InventoryBatches
            .AsNoTracking()
            .Where(ib => ib.ProductionBatchNo != null)
            .Select(ib => ib.ProductionBatchNo!)
            .Distinct()
            .ToListAsync();

        var warehousedSet = warehousedBatchNos.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // === 4. WorkOrderExecutionSummary ===
        var summaries = await _context.Set<WorkOrderExecutionSummary>()
            .AsNoTracking()
            .Select(s => new SummaryProjection
            {
                WorkOrderNo = s.WorkOrderNo,
                ScheduleStage = s.ScheduleStage,
                UrgencyLevel = s.UrgencyLevel
            })
            .ToListAsync();

        var summaryMap = summaries.ToDictionary(s => s.WorkOrderNo, StringComparer.OrdinalIgnoreCase);

        // === 5. 候选批次：仅批次状态为「成检」（InFinalInspection）===
        var candidates = await _context.ProductionBatches.AsNoTracking()
            .Where(b => b.Status == BatchStatus.InFinalInspection)
            .Select(b => new BatchProjection
            {
                Id = b.Id,
                Status = b.Status,
                BatchNo = b.BatchNo,
                InspectionStage = b.InspectionStage,
                OrderItemIds = b.OrderItemIds,
                PlantGrade = b.PlantGrade,
                Specification = b.Specification,
                LengthStatus = b.LengthStatus,
                WorkOrderNo = b.WorkOrderNo,
                SalesOrderNo = b.SalesOrderNo,
                ProductionMainNo = b.ProductionMainNo,
                Salesman = b.Salesman,
                EndCustomer = b.EndCustomer,
                ProductionType = b.ProductionType,
                SourceHeatNo = b.SourceHeatNo,
                SourceName = b.SourceName,
                ManufacturingItem = b.ManufacturingItem,
                ManufacturingStatus = b.ManufacturingStatus,
                DeliveryState = b.DeliveryState,
                CutRequirement = b.CutRequirement,
                TheoreticalOutputQty = b.TheoreticalOutputQty,
                TheoreticalOutputWeight = b.TheoreticalOutputWeight,
                TheoreticalUnitWeight = b.TheoreticalUnitWeight,
                ProductUnitWeight = b.ProductUnitWeight
            })
            .ToListAsync();

        if (candidates.Count == 0) return new();

        // === 6. 断切成品记录（生产支数/重量三态口径）===
        var batchIds = candidates.Select(c => c.Id).ToList();
        var cutRecordsByBatch = new Dictionary<int, List<ProductionRecord>>();
        foreach (var chunk in ChunkBatchIds(batchIds, 1000))
        {
            var cutRows = await _context.ProductionRecords
                .AsNoTracking()
                .Where(pr => chunk.Contains(pr.ProductionBatchId)
                          && pr.SectionName == SectionKeys.Cut
                          && pr.ProductStatus == ProductStatuses.Finished
                          && pr.IsPreCut != true) // 预成切不计入成品切割支数
                .ToListAsync();
            foreach (var r in cutRows)
            {
                if (!cutRecordsByBatch.TryGetValue(r.ProductionBatchId, out var list))
                    cutRecordsByBatch[r.ProductionBatchId] = list = new List<ProductionRecord>();
                list.Add(r);
            }
        }

        // === 7. FinalInspections 全量（顶层加载，供四档判定 + 各项检验日期/数量填充复用）===
        var allInspections = await _context.FinalInspections
            .AsNoTracking()
            .Where(fi => batchIds.Contains(fi.ProductionBatchId))
            .ToListAsync();

        var inspectionLookup = allInspections
            .GroupBy(fi => fi.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // === 8. 技术要求（ProductRequirement）→ 各批次要求检验项集合 ===
        // 判定「完成检验待入库」+ 填充「技术要求检验项」组共用；非工单批次兜底 PMI+表检+尺寸
        var requiredByBatch = await BuildRequiredByBatchAsync(candidates);

        // === 9. 组装看板：待到料 / 待检验 / 检验中 / 完成检验待入库 ===
        var result = new List<FinalInspectionPlanDto>();

        foreach (var b in candidates)
        {
            var typeStr = NormalizeInspectionType(b.InspectionStage);
            var key = (b.Id, typeStr);

            var dto = MapFromBatch(b, summaryMap, cutRecordsByBatch.GetValueOrDefault(b.Id) ?? new List<ProductionRecord>());
            dto.ReceiveDate = receiveDateMap.GetValueOrDefault(key);
            dto.MaxInspectionDate = inspectionDateMap.GetValueOrDefault(key);

            // 技术要求检验项（非工单批次兜底 PMI+表检+尺寸；与「完成检验待入库」判定同源）
            // 看板行要求项 = 对应成检类型的要求项（正式成检行→正式要求项；预成检行→预成检要求项）
            var batchReq = requiredByBatch.GetValueOrDefault(b.Id);
            var required = string.Equals(typeStr, nameof(InspectionType.PreInspection), StringComparison.OrdinalIgnoreCase)
                ? (batchReq?.PreRequired ?? BuildBaseRequired())
                : (batchReq?.FinalRequired ?? BuildBaseRequired());
            ApplyRequiredItems(dto, required);

            // 成检到料已「强制完成」：属异常完成批次（到料后执行有特殊情况的既定出口），
            // 不属于待到料/待检验/检验中任一档，看板主动跳过；由批次首页通知引导转「完成」后自然脱离候选。
            if (forcedKeys.Contains(key))
                continue;

            // 该批次本成检类型是否已有检验记录（预/终独立判定，不互相认可：
            // 检验项「预」只需预成检、「终」只需正式成检、「预+终」预成检与正式成检均需检验）
            var batchInspections = inspectionLookup.GetValueOrDefault(b.Id);
            var hasInspectionForType = batchInspections != null
                && batchInspections.Any(fi => string.Equals(NormalizeInspectionType(fi.InspectionType), typeStr, StringComparison.OrdinalIgnoreCase));

            // 四档判定顺序：检验 > 到料 > 裸批次（⚠️ 必须先判检验，否则「有检验无到料」的批次会被误归入待到料）
            if (hasInspectionForType)
            {
                if (b.BatchNo != null && warehousedSet.Contains(b.BatchNo))
                    continue; // 已入库，脱离看板
                dto.KanbanStage = KanbanStageKeys.Inspecting;
                // 该行全部要求项均有本成检类型的检验记录且未入库 → 完成检验待入库
                if (IsAllRequiredInspected(required, batchInspections!, typeStr))
                    dto.KanbanStage = KanbanStageKeys.CompletedAwaitingInbound;
            }
            else if (receivedKeys.Contains(key))
            {
                dto.KanbanStage = KanbanStageKeys.WaitingInspection;
            }
            else
            {
                dto.KanbanStage = KanbanStageKeys.WaitingMaterial;
            }

            result.Add(dto);
        }

        // === 10. 批量填充各项检验日期与数量（按「批次+成检类型」匹配）===
        FillInspectionData(result, inspectionLookup);

        return result;
    }

    /// <summary>按「批次+成检类型」填充各项检验日期和数量（与看板行粒度一致）</summary>
    private static void FillInspectionData(List<FinalInspectionPlanDto> result, Dictionary<int, List<FinalInspection>> inspectionLookup)
    {
        foreach (var dto in result)
        {
            if (!inspectionLookup.TryGetValue(dto.ProductionBatchId, out var inspList) || inspList.Count == 0)
                continue;

            var typeStr = dto.InspectionType?.ToString() ?? nameof(InspectionType.FormalInspection);
            // ⚠️ 与看板判定（hasInspectionForType）一致：检验记录类型经 NormalizeInspectionType 归一（空=正式成检），
            // 否则 InspectionType=null 的记录判定属正式成检但日期不填充，导致「已检项」被误判为待检
            var matching = inspList
                .Where(fi => string.Equals(NormalizeInspectionType(fi.InspectionType), typeStr, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matching.Count == 0) continue;

            dto.PmiDate = GetInspectionDate(matching, InspectionItem.PMIInspection);
            dto.VisualDate = GetInspectionDate(matching, InspectionItem.VisualInspection);
            dto.DimensionDate = GetInspectionDate(matching, InspectionItem.Dimension);
            dto.EndoscopyDate = GetInspectionDate(matching, InspectionItem.Endoscopy);
            dto.HydroDate = GetInspectionDate(matching, InspectionItem.HydrostaticPressure);
            dto.UnderwaterPneumaticDate = GetInspectionDate(matching, InspectionItem.UnderwaterPneumatic);
            dto.EddyCurrentDate = GetInspectionDate(matching, InspectionItem.EddyCurrent);
            dto.UltrasonicDate = GetInspectionDate(matching, InspectionItem.Ultrasonic);
            dto.PortColoringDate = GetInspectionDate(matching, InspectionItem.PortColoring);
            dto.InspectionCount = matching.Select(fi => fi.InspectionItem).Distinct().Count();
            // 检验支数：按检验项目分组汇总 Quantity，跨检验项目取最大（与成检追踪口径一致）
            dto.TotalQuantity = matching
                .GroupBy(fi => fi.InspectionItem)
                .Max(g => (int?)g.Sum(fi => fi.Quantity ?? 0)) ?? 0;
            dto.DefectReworkQuantity = matching.Sum(fi => fi.DefectReworkQuantity ?? 0);
            dto.DefectWarehouseQuantity = matching.Sum(fi => fi.DefectWarehouseQuantity ?? 0);
            dto.DefectScrapQuantity = matching.Sum(fi => fi.DefectScrapQuantity ?? 0);
            // 理论合格支：检验支数 - 三个次品汇总（负值归零，防御跨项目重复计数；与成检追踪口径一致）
            dto.QualifiedQuantity = Math.Max(0,
                dto.TotalQuantity - dto.DefectReworkQuantity - dto.DefectWarehouseQuantity - dto.DefectScrapQuantity);
        }
    }

    private static FinalInspectionPlanDto MapFromBatch(
        BatchProjection b,
        Dictionary<string, SummaryProjection> summaryMap,
        List<ProductionRecord> cutRecords)
    {
        var typeStr = NormalizeInspectionType(b.InspectionStage);
        var inspectionType = EnumHelper.TryParse<InspectionType>(typeStr) ?? InspectionType.FormalInspection;

        var dto = new FinalInspectionPlanDto
        {
            ProductionBatchId = b.Id,
            BatchNo = b.BatchNo,
            InspectionType = inspectionType,
            // 是否交付态（信息列，随批次当前制造状态实时计算；预成检由 DTO 显示层统一显 "-"）
            IsDeliveryStatus = !string.IsNullOrEmpty(b.ManufacturingStatus)
                && !string.IsNullOrEmpty(b.DeliveryState)
                && string.Equals(b.ManufacturingStatus, b.DeliveryState, StringComparison.OrdinalIgnoreCase)
                ? "是" : "否",
            ProductionType = string.IsNullOrEmpty(b.ProductionType)
                ? null : EnumHelper.TryParse<ProductionType>(b.ProductionType),
            ManufacturingItem = ParseMaterialType(b.ManufacturingItem),
            ManufacturingStatus = string.IsNullOrEmpty(b.ManufacturingStatus)
                ? null : EnumHelper.TryParse<DeliveryState>(b.ManufacturingStatus),
            DeliveryState = string.IsNullOrEmpty(b.DeliveryState)
                ? null : EnumHelper.TryParse<DeliveryState>(b.DeliveryState),
            PlantGrade = b.PlantGrade,
            Specification = b.Specification,
            LengthStatus = string.IsNullOrEmpty(b.LengthStatus)
                ? null : EnumHelper.TryParse<LengthStatus>(b.LengthStatus),
            SourceHeatNo = b.SourceHeatNo,
            SourceName = b.SourceName,
            WorkOrderNo = b.WorkOrderNo,
            SalesOrderNo = b.SalesOrderNo,
            ProductionMainNo = b.ProductionMainNo,
            Salesman = b.Salesman,
            EndCustomer = b.EndCustomer
        };

        // 生产支数/生产重量（三态口径，参照成检追踪 MapSourceToEntity）：
        //   1) 无需成品切割（CutRequirement=false）→ 批次理论成品支数
        //   2) 需成品切割 + 长度状态=定尺 → 断切成品记录切后支数(PostCutQuantity)汇总
        //   3) 需成品切割 + 长度状态<>定尺 → 断切成品记录加工支数(Quantity)汇总
        // 生产重量：非定尺=批次理论成品重量；定尺=产品单支重 × 生产支数（单支重缺失回退理论单支重）
        if (b.CutRequirement != true)
        {
            dto.ProductionCutQuantity = b.TheoreticalOutputQty ?? 0;
        }
        else if (string.Equals(b.LengthStatus, nameof(LengthStatus.Fixed), StringComparison.OrdinalIgnoreCase))
        {
            dto.ProductionCutQuantity = cutRecords.Sum(pr => pr.PostCutQuantity ?? 0);
        }
        else
        {
            dto.ProductionCutQuantity = cutRecords.Sum(pr => pr.Quantity ?? 0);
        }

        if (string.Equals(b.LengthStatus, nameof(LengthStatus.Fixed), StringComparison.OrdinalIgnoreCase))
        {
            var unitWeight = b.ProductUnitWeight ?? b.TheoreticalUnitWeight;
            dto.ProductionWeight = unitWeight.HasValue
                ? unitWeight.Value * dto.ProductionCutQuantity
                : null;
        }
        else
        {
            dto.ProductionWeight = b.TheoreticalOutputWeight;
        }

        // 排程信息（无可空时标记为「无此工单」）
        if (b.WorkOrderNo != null && summaryMap.TryGetValue(b.WorkOrderNo, out var s))
        {
            dto.ScheduleStage = s.ScheduleStage;
            dto.UrgencyLevel = s.UrgencyLevel;
        }
        else
        {
            dto.ScheduleStage = -1;
        }

        return dto;
    }

    // ========== 辅助方法 ==========

    private static DateTime? GetInspectionDate(List<FinalInspection> inspections, InspectionItem item)
    {
        return inspections
            .Where(fi => fi.InspectionItem == item)
            .Max(fi => (DateTime?)fi.InspectionDate);
    }

    /// <summary>
    /// 归一化成检类型：空视为正式成检（批次「成检附加」为空 / 检验记录类型为空时的默认值）
    /// </summary>
    private static string NormalizeInspectionType(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? nameof(InspectionType.FormalInspection) : raw;

    /// <summary>
    /// 构建各候选批次的「要求检验项」集合（键=批次Id），按成检阶段拆分（预成检要求项/正式成检要求项）：
    /// 10 个成品检验项按 ProductRequirement 4 值枚举（终=正式/预=预成检/预+终=两者/ -=不要求）拆分；
    /// 无要求记录（非工单批次）→ 兜底 {PMI,表检,尺寸} 为预成检与正式成检共同要求项（恒必检）。
    /// 关联链（⚠️ OrderItemIds 存的是「项次序号 Sequence」，非 OrderItem.Id）：
    ///   批次 OrderItemIds 非空 → (批次 SalesOrderNo, Sequence 列表)；
    ///   为空 → 经批次 WorkOrderNo 取工单 (WorkOrder.SalesOrderNo, WorkOrder.OrderItemIds)；
    ///   → OrderItem(OrderNumber, Sequence) → OrderItem.Id → ProductRequirement.OrderItemId。
    /// </summary>
    private async Task<Dictionary<int, BatchRequirement>> BuildRequiredByBatchAsync(List<BatchProjection> candidates)
    {
        var result = new Dictionary<int, BatchRequirement>();

        // 批次自身 OrderItemIds 非空 → 直接可用；为空 → 经工单号取工单（存量成检批次 OrderItemIds 全空）
        var selfBatches = candidates.Where(b => !string.IsNullOrWhiteSpace(b.OrderItemIds)).ToList();
        var woBatches = candidates.Where(b => string.IsNullOrWhiteSpace(b.OrderItemIds)).ToList();

        // 工单关联查询（批次 OrderItemIds 为空时回退）
        var woRefs = new Dictionary<string, (string OrderNo, List<int> Seqs)>(StringComparer.OrdinalIgnoreCase);
        var woNos = woBatches.Select(b => b.WorkOrderNo)
            .Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).Distinct().ToList();
        foreach (var chunk in ChunkStrings(woNos, 1000))
        {
            var rows = await _context.WorkOrders.AsNoTracking()
                .Where(w => chunk.Contains(w.WorkOrderNo))
                .Select(w => new { w.WorkOrderNo, w.SalesOrderNo, w.OrderItemIds })
                .ToListAsync();
            foreach (var r in rows)
            {
                if (!woRefs.ContainsKey(r.WorkOrderNo))
                    woRefs[r.WorkOrderNo] = (r.SalesOrderNo ?? "", ParseOrderItemIds(r.OrderItemIds).ToList());
            }
        }

        // 每批次 → (订单号, Sequence 集合)
        var batchRefs = new List<(int BatchId, string OrderNo, List<int> Seqs)>();
        foreach (var b in selfBatches)
        {
            var seqs = ParseOrderItemIds(b.OrderItemIds).ToList();
            if (seqs.Count > 0) batchRefs.Add((b.Id, b.SalesOrderNo ?? "", seqs));
        }
        foreach (var b in woBatches)
        {
            if (string.IsNullOrWhiteSpace(b.WorkOrderNo)) continue;
            if (!woRefs.TryGetValue(b.WorkOrderNo, out var wo)) continue;
            if (wo.Seqs.Count == 0) continue;
            batchRefs.Add((b.Id, wo.OrderNo, wo.Seqs));
        }

        // (订单号, Sequence) → 要求检验项集合（JOIN OrderItem + ProductRequirement，分块避免 IN 参数上限）
        var requiredByPair = new Dictionary<(string OrderNo, int Seq), BatchRequirement>();
        foreach (var orderGroup in batchRefs
            .SelectMany(r => r.Seqs.Select(s => (OrderNo: r.OrderNo, Seq: s)))
            .GroupBy(p => p.OrderNo, StringComparer.OrdinalIgnoreCase))
        {
            var orderNo = orderGroup.Key;
            var seqs = orderGroup.Select(p => p.Seq).Distinct().ToList();
            foreach (var seqChunk in ChunkBatchIds(seqs, 1000))
            {
                var oiRows = await _context.OrderItems.AsNoTracking()
                    .Where(oi => oi.OrderNumber == orderNo && seqChunk.Contains(oi.Sequence))
                    .Select(oi => new { oi.Id, oi.Sequence })
                    .ToListAsync();
                if (oiRows.Count == 0) continue;

                var oiIdChunks = ChunkBatchIds(oiRows.Select(o => o.Id).ToList(), 1000);
                foreach (var oiChunk in oiIdChunks)
                {
                    var reqRows = await _context.ProductRequirements.AsNoTracking()
                        .Where(pr => oiChunk.Contains(pr.OrderItemId))
                        .Select(pr => new RequirementProjection
                        {
                            OrderItemId = pr.OrderItemId,
                            PmiInspection = pr.PmiInspection,
                            SurfaceInspection = pr.SurfaceInspection,
                            Dimension = pr.Dimension,
                            Endoscopy = pr.Endoscopy,
                            HydrostaticTest = pr.HydrostaticTest,
                            UnderwaterPressure = pr.UnderwaterPressure,
                            EddyCurrent = pr.EddyCurrent,
                            UltrasonicTest = pr.UltrasonicTest,
                            PortColoring = pr.PortColoring
                        }).ToListAsync();
                    foreach (var r in reqRows)
                    {
                        var oiRow = oiRows.First(o => o.Id == r.OrderItemId);
                        var key = (orderNo, oiRow.Sequence);
                        if (!requiredByPair.TryGetValue(key, out var req))
                            requiredByPair[key] = req = new BatchRequirement();
                        ApplyStage(req, r.PmiInspection, InspectionItem.PMIInspection);
                        ApplyStage(req, r.SurfaceInspection, InspectionItem.VisualInspection);
                        ApplyStage(req, r.Dimension, InspectionItem.Dimension);
                        ApplyStage(req, r.Endoscopy, InspectionItem.Endoscopy);
                        ApplyStage(req, r.HydrostaticTest, InspectionItem.HydrostaticPressure);
                        ApplyStage(req, r.UnderwaterPressure, InspectionItem.UnderwaterPneumatic);
                        ApplyStage(req, r.EddyCurrent, InspectionItem.EddyCurrent);
                        ApplyStage(req, r.UltrasonicTest, InspectionItem.Ultrasonic);
                        ApplyStage(req, r.PortColoring, InspectionItem.PortColoring);
                    }
                }
            }
        }

        // 汇总到批次
        foreach (var b in candidates)
        {
            var req = new BatchRequirement();
            foreach (var r in batchRefs.Where(x => x.BatchId == b.Id))
            {
                foreach (var seq in r.Seqs)
                {
                    if (requiredByPair.TryGetValue((r.OrderNo, seq), out var pair))
                    {
                        req.PreRequired.UnionWith(pair.PreRequired);
                        req.FinalRequired.UnionWith(pair.FinalRequired);
                    }
                }
            }
            // 无任何要求记录（或全部配为「-」）→ 非工单批次兜底：{PMI,表检,尺寸} 预成检与正式成检均要求（恒必检）
            if (!req.HasAny)
            {
                req.PreRequired.UnionWith(BuildBaseRequired());
                req.FinalRequired.UnionWith(BuildBaseRequired());
            }
            result[b.Id] = req;
        }
        return result;
    }

    /// <summary>恒必检兜底：PMI + 表检 + 尺寸（非工单批次无技术要求时的默认要求项集合，预成检与正式成检均要求）</summary>
    private static HashSet<InspectionItem> BuildBaseRequired()
        => new() { InspectionItem.PMIInspection, InspectionItem.VisualInspection, InspectionItem.Dimension };

    /// <summary>按检验阶段将检验项拆入预成检/正式成检要求集合（终→正式；预→预成检；预+终→两者；-→不要求）</summary>
    private static void ApplyStage(BatchRequirement req, InspectionRequirementStage stage, InspectionItem item)
    {
        switch (stage)
        {
            case InspectionRequirementStage.FinalOnly:
                req.FinalRequired.Add(item);
                break;
            case InspectionRequirementStage.PreOnly:
                req.PreRequired.Add(item);
                break;
            case InspectionRequirementStage.PreAndFinal:
                req.PreRequired.Add(item);
                req.FinalRequired.Add(item);
                break;
            case InspectionRequirementStage.None:
            default:
                break;
        }
    }

    /// <summary>将要求项集合写入 DTO（9 项 bool + 必检项数 ReqCount）</summary>
    private static void ApplyRequiredItems(FinalInspectionPlanDto dto, HashSet<InspectionItem> required)
    {
        dto.ReqCount = required.Count;
        dto.ReqPmi = required.Contains(InspectionItem.PMIInspection);
        dto.ReqVisual = required.Contains(InspectionItem.VisualInspection);
        dto.ReqDimension = required.Contains(InspectionItem.Dimension);
        dto.ReqEndoscopy = required.Contains(InspectionItem.Endoscopy);
        dto.ReqHydro = required.Contains(InspectionItem.HydrostaticPressure);
        dto.ReqUnderwater = required.Contains(InspectionItem.UnderwaterPneumatic);
        dto.ReqEddy = required.Contains(InspectionItem.EddyCurrent);
        dto.ReqUltrasonic = required.Contains(InspectionItem.Ultrasonic);
        dto.ReqPortColoring = required.Contains(InspectionItem.PortColoring);
    }

    /// <summary>
    /// 该行类型的已检验项是否覆盖全部要求项。
    /// ⚠️ 预/终独立判定，不互相认可：已检项 = 该批次**本成检类型**（typeStr）的检验记录检验项目去重
    /// （检验项「预」只需预成检、「终」只需正式成检、「预+终」预成检与正式成检均需检验）。
    /// </summary>
    private static bool IsAllRequiredInspected(HashSet<InspectionItem> required, List<FinalInspection> batchInspections, string typeStr)
    {
        var inspected = batchInspections
            .Where(fi => string.Equals(NormalizeInspectionType(fi.InspectionType), typeStr, StringComparison.OrdinalIgnoreCase))
            .Select(fi => fi.InspectionItem)
            .ToHashSet();
        return required.IsSubsetOf(inspected);
    }

    /// <summary>逗号分隔的 OrderItemIds → 去非法 int（存的是「项次序号 Sequence」，非 OrderItem.Id）</summary>
    private static IEnumerable<int> ParseOrderItemIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) yield break;
        foreach (var part in raw.Split(','))
        {
            if (int.TryParse(part.Trim(), out var id))
                yield return id;
        }
    }

    /// <summary>制造物品字符串 → MaterialType 枚举（参照成检追踪 ParseMaterialType）</summary>
    private static MaterialType? ParseMaterialType(string? value)
    {
        return value switch
        {
            "OrderFinishedProduct" => MaterialType.OrderFinished,
            "PreparedMaterial" or "PreparedFinished" or "StockFinished" => MaterialType.Finished,
            "SurplusStock" => MaterialType.Surplus,
            "IntermediateProduct" => MaterialType.SemiFinished,
            _ => Enum.TryParse<MaterialType>(value, true, out var r) ? r : null
        };
    }

    private static IEnumerable<List<int>> ChunkBatchIds(List<int> ids, int chunkSize)
    {
        for (int i = 0; i < ids.Count; i += chunkSize)
            yield return ids.GetRange(i, Math.Min(chunkSize, ids.Count - i));
    }

    private static IEnumerable<List<string>> ChunkStrings(List<string> values, int chunkSize)
    {
        for (int i = 0; i < values.Count; i += chunkSize)
            yield return values.GetRange(i, Math.Min(chunkSize, values.Count - i));
    }

    // ========== 中间投影类 ==========

    private class BatchProjection
    {
        public int Id { get; set; }
        public BatchStatus Status { get; set; }
        public string? BatchNo { get; set; }
        public string? InspectionStage { get; set; }
        public string? OrderItemIds { get; set; }
        public string? PlantGrade { get; set; }
        public string? Specification { get; set; }
        public string? LengthStatus { get; set; }
        public string? WorkOrderNo { get; set; }
        public string? SalesOrderNo { get; set; }
        public string? ProductionMainNo { get; set; }
        public string Salesman { get; set; } = "";
        public string? EndCustomer { get; set; }
        public string? ProductionType { get; set; }
        public string? SourceHeatNo { get; set; }
        public string? SourceName { get; set; }
        public string ManufacturingItem { get; set; } = "";
        public string? ManufacturingStatus { get; set; }
        public string DeliveryState { get; set; } = "";
        public bool CutRequirement { get; set; }
        public int? TheoreticalOutputQty { get; set; }
        public int? TheoreticalOutputWeight { get; set; }
        public decimal? TheoreticalUnitWeight { get; set; }
        public decimal? ProductUnitWeight { get; set; }
    }

    private class SummaryProjection
    {
        public string WorkOrderNo { get; set; } = "";
        public int ScheduleStage { get; set; }
        public string? UrgencyLevel { get; set; }
    }

    private class RequirementProjection
    {
        public int OrderItemId { get; set; }
        public InspectionRequirementStage PmiInspection { get; set; }
        public InspectionRequirementStage SurfaceInspection { get; set; }
        public InspectionRequirementStage Dimension { get; set; }
        public InspectionRequirementStage Endoscopy { get; set; }
        public InspectionRequirementStage HydrostaticTest { get; set; }
        public InspectionRequirementStage UnderwaterPressure { get; set; }
        public InspectionRequirementStage EddyCurrent { get; set; }
        public InspectionRequirementStage UltrasonicTest { get; set; }
        public InspectionRequirementStage PortColoring { get; set; }
    }

    /// <summary>
    /// 批次要求检验项集合（按成检阶段拆分）：
    /// PreRequired=预成检要求项；FinalRequired=正式成检要求项（看板行显示/判定用）
    /// </summary>
    private class BatchRequirement
    {
        public HashSet<InspectionItem> PreRequired { get; } = new();
        public HashSet<InspectionItem> FinalRequired { get; } = new();

        public bool HasAny => PreRequired.Count > 0 || FinalRequired.Count > 0;
    }

    /// <summary>
    /// 获取成检计划「待检批支重汇总」：行=9 个成品检验项，列=待到料/待检验/检验中/汇总数据。
    /// 某检验项统计「要求该检验项（Req=true）且 本成检类型尚未完成该检验（对应检验日期为空）」的看板批次，
    /// 按看板档位分列；已检完该检验项的批次（含「完成检验待入库」档，日期非空）不计入；
    /// 每列 = 批次数/生产支数/生产重量(kg)，预+正式合并、按批次去重；汇总数据列 = 三档之和。
    /// </summary>
    public async Task<List<FinalInspectionPlanSummaryRowDto>> GetSummaryAsync()
    {
        var kanban = await GetKanbanAsync();
        var rows = new List<FinalInspectionPlanSummaryRowDto>();
        if (kanban.Count == 0) return rows;

        // 9 个成品检验项 → DTO 要求项 bool + 对应检验日期（待检判定：要求项 且 日期为空）
        // 日期由 FillInspectionData 按「批次+成检类型」填充，与看板行粒度一致
        var items = new (string Name, Func<FinalInspectionPlanDto, bool> Req, Func<FinalInspectionPlanDto, bool> Inspected)[]
        {
            ("PMI检验", x => x.ReqPmi, x => x.PmiDate.HasValue),
            ("表检", x => x.ReqVisual, x => x.VisualDate.HasValue),
            ("尺寸", x => x.ReqDimension, x => x.DimensionDate.HasValue),
            ("内窥", x => x.ReqEndoscopy, x => x.EndoscopyDate.HasValue),
            ("水压", x => x.ReqHydro, x => x.HydroDate.HasValue),
            ("水下气压", x => x.ReqUnderwater, x => x.UnderwaterPneumaticDate.HasValue),
            ("涡流", x => x.ReqEddy, x => x.EddyCurrentDate.HasValue),
            ("超声波", x => x.ReqUltrasonic, x => x.UltrasonicDate.HasValue),
            ("端口着色", x => x.ReqPortColoring, x => x.PortColoringDate.HasValue),
        };

        foreach (var (name, req, inspected) in items)
        {
            // 待检批次 = 要求该检验项 且 尚未完成该检验（日期为空）
            var pending = kanban.Where(x => req(x) && !inspected(x)).ToList();

            var (wmC, wmQ, wmW) = SummarizePending(pending, KanbanStageKeys.WaitingMaterial);
            var (wiC, wiQ, wiW) = SummarizePending(pending, KanbanStageKeys.WaitingInspection);
            var (iC, iQ, iW) = SummarizePending(pending, KanbanStageKeys.Inspecting);

            rows.Add(new FinalInspectionPlanSummaryRowDto
            {
                InspectionItemName = name,
                WaitingMaterialCount = wmC, WaitingMaterialQuantity = wmQ, WaitingMaterialWeight = wmW,
                WaitingInspectionCount = wiC, WaitingInspectionQuantity = wiQ, WaitingInspectionWeight = wiW,
                InspectingCount = iC, InspectingQuantity = iQ, InspectingWeight = iW,
                TotalCount = wmC + wiC + iC,
                TotalQuantity = wmQ + wiQ + iQ,
                TotalWeight = wmW + wiW + iW,
            });
        }
        return rows;
    }

    /// <summary>按看板档位汇总待检批次（按批次去重：同一批次预/正式行生产支数/重量同源取首条）</summary>
    private static (int Count, int Quantity, decimal Weight) SummarizePending(
        List<FinalInspectionPlanDto> items, string stage)
    {
        var distinct = items
            .Where(x => string.Equals(x.KanbanStage, stage, StringComparison.Ordinal))
            .GroupBy(x => x.ProductionBatchId)
            .Select(g => g.First())
            .ToList();

        return (
            distinct.Count,
            distinct.Sum(x => x.ProductionCutQuantity),
            distinct.Sum(x => x.ProductionWeight ?? 0m)
        );
    }

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    public Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        var pdfBytes = FinalInspectionPlanPrintHelper.GeneratePdf(title, items, columns);
        return Task.FromResult(pdfBytes);
    }
}
