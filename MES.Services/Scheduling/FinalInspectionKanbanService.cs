using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services.Scheduling;

/// <summary>
/// 成检看板服务 — 三档分组：待到料/待检验/检验中
/// </summary>
public class FinalInspectionKanbanService : IFinalInspectionKanbanService
{
    private readonly AppDbContext _context;

    public FinalInspectionKanbanService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<FinalInspectionKanbanDto>> GetKanbanAsync()
    {
        // === 预查询辅助数据 ===

        // 1. MaterialReceiveCheck（排除强制完成）
        var receiveChecks = await _context.MaterialReceiveChecks
            .AsNoTracking()
            .Where(rc => !rc.IsForceCompleted)
            .Select(rc => new { rc.ProductionBatchId, rc.ReceiveDate })
            .ToListAsync();

        var receivedIds = receiveChecks.Select(r => r.ProductionBatchId).ToHashSet();
        var receiveDateMap = receiveChecks.ToDictionary(r => r.ProductionBatchId, r => r.ReceiveDate);

        // 2. FinalInspections 最大检验日期
        var inspectionDateMap = await _context.FinalInspections
            .AsNoTracking()
            .GroupBy(fi => fi.ProductionBatchId)
            .Select(g => new { ProductionBatchId = g.Key, MaxDate = g.Max(fi => (DateTime?)fi.InspectionDate) })
            .ToDictionaryAsync(g => g.ProductionBatchId, g => g.MaxDate);

        var inspectedIds = inspectionDateMap.Keys.ToHashSet();

        // 3. InventoryBatch 已入库 ProductionBatchNo 集合
        var warehousedBatchNos = await _context.InventoryBatches
            .AsNoTracking()
            .Where(ib => ib.ProductionBatchNo != null)
            .Select(ib => ib.ProductionBatchNo!)
            .Distinct()
            .ToListAsync();

        var warehousedSet = warehousedBatchNos.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 4. WorkOrderExecutionSummary
        var summaries = await _context.Set<WorkOrderExecutionSummary>()
            .AsNoTracking()
            .Select(s => new SummaryProjection
            {
                WorkOrderNo = s.WorkOrderNo,
                ScheduleStage = s.ScheduleStage,
                UrgencyLevel = s.UrgencyLevel,
                Salesman = s.Salesman
            })
            .ToListAsync();

        var summaryMap = summaries.ToDictionary(s => s.WorkOrderNo, StringComparer.OrdinalIgnoreCase);

        // === 待到料：ProductionBatch at 成品检验 stage，无 MaterialReceiveCheck ===
        var awaitingMaterial = await BuildAwaitingMaterialAsync(receivedIds, summaryMap);

        // === 待检验 + 检验中：有 MaterialReceiveCheck 的批次 ===
        var inProcess = await BuildInProcessAsync(
            receivedIds, receiveDateMap, inspectedIds, inspectionDateMap,
            warehousedSet, summaryMap);

        var result = new List<FinalInspectionKanbanDto>();
        result.AddRange(awaitingMaterial);
        result.AddRange(inProcess);
        return result;
    }

    /// <summary>
    /// 待到料：InProgress 批次中 ProcessGroup 判定为"成品检验"且未到料的
    /// </summary>
    private async Task<List<FinalInspectionKanbanDto>> BuildAwaitingMaterialAsync(
        HashSet<int> receivedIds,
        Dictionary<string, SummaryProjection> summaryMap)
    {
        // 查询在产且工段=检验的批次（缩小数据范围）
        var candidates = await _context.ProductionBatches.AsNoTracking()
            .Where(b => (b.Status == BatchStatus.None || b.Status == BatchStatus.InProgress) &&
                        ((b.CurrentSectionCompleted == false && b.CurrentSectionName == "检验") ||
                         (b.CurrentSectionCompleted != false && b.NextSectionName == "检验")))
            .Select(b => new BatchProjection
            {
                Id = b.Id,
                BatchNo = b.BatchNo,
                TagNo = b.TagNo,
                PlantGrade = b.PlantGrade,
                CurrentValidWeight = b.CurrentValidWeight,
                WorkOrderNo = b.WorkOrderNo,
                Specification = b.Specification,
                LengthStatus = b.LengthStatus,
                MinLength = b.MinLength,
                MaxLength = b.MaxLength,
                CurrentSectionCompleted = b.CurrentSectionCompleted,
                CurrentSectionName = b.CurrentSectionName,
                CurrentGroupName = b.CurrentGroupName,
                NextSectionName = b.NextSectionName,
                NextProcess = b.NextProcess
            })
            .ToListAsync();

        if (candidates.Count == 0) return new();

        var batchIds = candidates.Select(c => c.Id).ToList();

        // 加载 ProcessGroups 进行 SequenceNumber 判定
        var pgList = await _context.Set<ProcessGroup>().AsNoTracking()
            .Where(pg => batchIds.Contains(pg.ProductionBatchId))
            .Select(pg => new { pg.ProductionBatchId, pg.ProcessName, pg.SequenceNumber })
            .ToListAsync();

        var pgLookup = pgList
            .GroupBy(pg => pg.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<FinalInspectionKanbanDto>();

        foreach (var b in candidates)
        {
            // 跳过已到料的
            if (receivedIds.Contains(b.Id)) continue;
            if (!pgLookup.TryGetValue(b.Id, out var pgs) || pgs.Count == 0) continue;

            var maxSeq = pgs.Max(pg => pg.SequenceNumber);

            bool isFinalInspection = false;
            if (b.CurrentSectionCompleted == false && b.CurrentSectionName == "检验" && b.CurrentGroupName != null)
            {
                var seq = pgs.Where(pg => pg.ProcessName == b.CurrentGroupName)
                    .Select(pg => (int?)pg.SequenceNumber).FirstOrDefault();
                if (seq == maxSeq) isFinalInspection = true;
            }
            else if (b.CurrentSectionCompleted != false && b.NextSectionName == "检验" && b.NextProcess != null)
            {
                var seq = pgs.Where(pg => pg.ProcessName == b.NextProcess)
                    .Select(pg => (int?)pg.SequenceNumber).FirstOrDefault();
                if (seq == maxSeq) isFinalInspection = true;
            }

            if (!isFinalInspection) continue;

            var dto = MapFromBatch(b, summaryMap);
            dto.KanbanStage = "待到料";
            result.Add(dto);
        }

        return result;
    }

    /// <summary>
    /// 待检验 + 检验中：从 MaterialReceiveCheck 出发
    /// </summary>
    private async Task<List<FinalInspectionKanbanDto>> BuildInProcessAsync(
        HashSet<int> receivedIds,
        Dictionary<int, DateTime> receiveDateMap,
        HashSet<int> inspectedIds,
        Dictionary<int, DateTime?> inspectionDateMap,
        HashSet<string> warehousedSet,
        Dictionary<string, SummaryProjection> summaryMap)
    {
        if (receivedIds.Count == 0) return new();

        // 批量加载 ProductionBatch 数据
        var batchMap = await _context.ProductionBatches.AsNoTracking()
            .Where(b => receivedIds.Contains(b.Id))
            .Select(b => new BatchProjection
            {
                Id = b.Id,
                BatchNo = b.BatchNo,
                TagNo = b.TagNo,
                PlantGrade = b.PlantGrade,
                CurrentValidWeight = b.CurrentValidWeight,
                WorkOrderNo = b.WorkOrderNo,
                Specification = b.Specification,
                LengthStatus = b.LengthStatus,
                MinLength = b.MinLength,
                MaxLength = b.MaxLength,
            })
            .ToDictionaryAsync(b => b.Id);

        var result = new List<FinalInspectionKanbanDto>();

        foreach (var batchId in receivedIds)
        {
            if (!batchMap.TryGetValue(batchId, out var b)) continue;

            bool isInspected = inspectedIds.Contains(batchId);
            bool isWarehoused = b.BatchNo != null && warehousedSet.Contains(b.BatchNo);

            string stage;
            if (!isInspected)
                stage = "待检验";
            else if (!isWarehoused)
                stage = "检验中";
            else
                continue; // 已入库，脱离看板

            var dto = MapFromBatch(b, summaryMap);
            dto.ReceiveDate = receiveDateMap.GetValueOrDefault(batchId);
            dto.MaxInspectionDate = inspectionDateMap.GetValueOrDefault(batchId);
            dto.KanbanStage = stage;
            result.Add(dto);
        }

        return result;
    }

    private static FinalInspectionKanbanDto MapFromBatch(
        BatchProjection b,
        Dictionary<string, SummaryProjection> summaryMap)
    {
        var dto = new FinalInspectionKanbanDto
        {
            ProductionBatchId = b.Id,
            BatchNo = b.BatchNo,
            TagNo = b.TagNo,
            PlantGrade = b.PlantGrade,
            CurrentValidWeight = b.CurrentValidWeight,
            WorkOrderNo = b.WorkOrderNo,
            Specification = b.Specification,
            LengthStatus = b.LengthStatus,
            MinLength = b.MinLength,
            MaxLength = b.MaxLength
        };

        // 从 WorkOrderExecutionSummary 补充排程信息
        if (b.WorkOrderNo != null && summaryMap.TryGetValue(b.WorkOrderNo, out var s))
        {
            dto.ScheduleStage = s.ScheduleStage;
            dto.UrgencyLevel = s.UrgencyLevel;
            dto.Salesman = s.Salesman;
        }

        return dto;
    }

    // ========== 中间投影类 ==========

    private class BatchProjection
    {
        public int Id { get; set; }
        public string? BatchNo { get; set; }
        public string? TagNo { get; set; }
        public string? PlantGrade { get; set; }
        public decimal? CurrentValidWeight { get; set; }
        public string? WorkOrderNo { get; set; }
        public string? Specification { get; set; }
        public string? LengthStatus { get; set; }
        public decimal? MinLength { get; set; }
        public decimal? MaxLength { get; set; }
        public bool? CurrentSectionCompleted { get; set; }
        public string? CurrentSectionName { get; set; }
        public string? CurrentGroupName { get; set; }
        public string? NextSectionName { get; set; }
        public string? NextProcess { get; set; }
    }

    private class SummaryProjection
    {
        public string WorkOrderNo { get; set; } = "";
        public int ScheduleStage { get; set; }
        public string? UrgencyLevel { get; set; }
        public string? Salesman { get; set; }
    }
}
