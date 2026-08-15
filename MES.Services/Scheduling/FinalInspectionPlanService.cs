using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Enums;
using MES.Core.Interfaces.Batch;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.DataExchange;
using MES.Core.Interfaces.Equipment;
using MES.Core.Interfaces.Infrastructure;
using MES.Core.Interfaces.Materials;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.StandardRegister;
using MES.Core.Interfaces.Quality;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Interfaces.Warehouse;
using MES.Core.Interfaces.WorkOrder;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Batch;
using MES.Services.Printing;

namespace MES.Services.Scheduling;

/// <summary>
/// 成检计划服务 — 三档分组：待到料/待检验/检验中
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
        // === 预查询辅助数据 ===

        // 1. MaterialReceiveCheck（排除强制完成）
        var receiveChecks = await _context.MaterialReceiveChecks
            .AsNoTracking()
            .Where(rc => !rc.IsForceCompleted)
            .Select(rc => new { rc.ProductionBatchId, rc.ReceiveDate })
            .ToListAsync();

        var receivedIds = receiveChecks.Select(r => r.ProductionBatchId).ToHashSet();
        // 同一批次可能存在多条未强制完成的到料记录（预检+终检/多次到料），按批次分组取最近到料日期，避免 ToDictionary 重复键崩溃
        var receiveDateMap = receiveChecks
            .GroupBy(r => r.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.Max(r => r.ReceiveDate));

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

        var result = new List<FinalInspectionPlanDto>();
        result.AddRange(awaitingMaterial);
        result.AddRange(inProcess);

        // === 5. 批量加载 FinalInspections，填充各项检验日期和数量 ===
        var allBatchIds = result.Select(r => r.ProductionBatchId).Distinct().ToList();
        if (allBatchIds.Count > 0)
        {
            var allInspections = await _context.FinalInspections
                .AsNoTracking()
                .Where(fi => allBatchIds.Contains(fi.ProductionBatchId))
                .ToListAsync();

            var inspectionLookup = allInspections
                .GroupBy(fi => fi.ProductionBatchId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var dto in result)
            {
                if (!inspectionLookup.TryGetValue(dto.ProductionBatchId, out var inspList) || inspList.Count == 0)
                    continue;

                dto.PmiDate = GetInspectionDate(inspList, InspectionItem.PMIInspection);
                dto.VisualDate = GetInspectionDate(inspList, InspectionItem.VisualInspection);
                dto.DimensionDate = GetInspectionDate(inspList, InspectionItem.Dimension);
                dto.EndoscopyDate = GetInspectionDate(inspList, InspectionItem.Endoscopy);
                dto.HydroDate = GetInspectionDate(inspList, InspectionItem.HydrostaticPressure);
                dto.UnderwaterPneumaticDate = GetInspectionDate(inspList, InspectionItem.UnderwaterPneumatic);
                dto.EddyCurrentDate = GetInspectionDate(inspList, InspectionItem.EddyCurrent);
                dto.UltrasonicDate = GetInspectionDate(inspList, InspectionItem.Ultrasonic);
                dto.PortColoringDate = GetInspectionDate(inspList, InspectionItem.PortColoring);
                dto.InspectionCount = inspList.Select(fi => fi.InspectionItem).Distinct().Count();
                dto.TotalQuantity = inspList.Max(fi => (int?)(fi.Quantity ?? 0)) ?? 0;
                dto.QualifiedQuantity = inspList.Min(fi => (int?)(fi.QualifiedQuantity ?? 0)) ?? 0;
                dto.DefectReworkQuantity = inspList.Sum(fi => fi.DefectReworkQuantity ?? 0);
                dto.DefectWarehouseQuantity = inspList.Sum(fi => fi.DefectWarehouseQuantity ?? 0);
                dto.DefectScrapQuantity = inspList.Sum(fi => fi.DefectScrapQuantity ?? 0);
            }
        }

        return result;
    }

    /// <summary>
    /// 待到料：InProgress 批次中 ProcessGroup 判定为"成品检验"且未到料的
    /// </summary>
    private async Task<List<FinalInspectionPlanDto>> BuildAwaitingMaterialAsync(
        HashSet<int> receivedIds,
        Dictionary<string, SummaryProjection> summaryMap)
    {
        // 查询在产且工段=检验的批次（缩小数据范围）
        var candidates = await _context.ProductionBatches.AsNoTracking()
            .Where(b => (b.Status == BatchStatus.None || b.Status == BatchStatus.InProgress) &&
                        ((b.CurrentSectionCompleted == false && b.CurrentSectionName == SectionKeys.Inspection) ||
                         (b.CurrentSectionCompleted != false && b.NextSectionName == SectionKeys.Inspection)))
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

        var result = new List<FinalInspectionPlanDto>();

        foreach (var b in candidates)
        {
            // 跳过已到料的
            if (receivedIds.Contains(b.Id)) continue;
            if (!pgLookup.TryGetValue(b.Id, out var pgs) || pgs.Count == 0) continue;

            var maxSeq = pgs.Max(pg => pg.SequenceNumber);

            bool isFinalInspection = false;
            if (b.CurrentSectionCompleted == false && b.CurrentSectionName == SectionKeys.Inspection && b.CurrentGroupName != null)
            {
                var seq = pgs.Where(pg => pg.ProcessName == b.CurrentGroupName)
                    .Select(pg => (int?)pg.SequenceNumber).FirstOrDefault();
                if (seq == maxSeq) isFinalInspection = true;
            }
            else if (b.CurrentSectionCompleted != false && b.NextSectionName == SectionKeys.Inspection && b.NextProcess != null)
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
    private async Task<List<FinalInspectionPlanDto>> BuildInProcessAsync(
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

        var result = new List<FinalInspectionPlanDto>();

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

    private static FinalInspectionPlanDto MapFromBatch(
        BatchProjection b,
        Dictionary<string, SummaryProjection> summaryMap)
    {
        var dto = new FinalInspectionPlanDto
        {
            ProductionBatchId = b.Id,
            BatchNo = b.BatchNo,
            TagNo = b.TagNo,
            PlantGrade = b.PlantGrade,
            CurrentValidWeight = b.CurrentValidWeight,
            WorkOrderNo = b.WorkOrderNo,
            Specification = b.Specification,
            LengthStatus = string.IsNullOrEmpty(b.LengthStatus) ? null : Enum.Parse<LengthStatus>(b.LengthStatus),
            MinLength = b.MinLength,
            MaxLength = b.MaxLength
        };

        // 从 WorkOrderExecutionSummary 补充排程信息（无可空时标记为「无此工单」）
        if (b.WorkOrderNo != null && summaryMap.TryGetValue(b.WorkOrderNo, out var s))
        {
            dto.ScheduleStage = s.ScheduleStage;
            dto.UrgencyLevel = s.UrgencyLevel;
            dto.Salesman = s.Salesman;
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

    // ========== 中间投影类 ==========

    private class BatchProjection
    {
        public int Id { get; set; }
        public string? BatchNo { get; set; }
        public string? TagNo { get; set; }
        public string? PlantGrade { get; set; }
        public int? CurrentValidWeight { get; set; }
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

    /// <summary>打印选中行（Mode A：前端已准备数据）</summary>
    public Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        var pdfBytes = FinalInspectionPlanPrintHelper.GeneratePdf(title, items, columns);
        return Task.FromResult(pdfBytes);
    }
}
