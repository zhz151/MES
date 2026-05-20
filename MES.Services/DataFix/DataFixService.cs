using Microsoft.EntityFrameworkCore;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using Microsoft.Extensions.Logging;

namespace MES.Services.DataFix;

/// <summary>
/// 数据修复服务 — 一键修复所有系统计算字段
/// </summary>
public class DataFixService : IDataFixService
{
    private readonly AppDbContext _context;
    private readonly IProductionRecordService _productionRecordService;
    private readonly IPurchaseOrderService _purchaseOrderService;
    private readonly ISubcontractOrderService _subcontractOrderService;
    private readonly ILogger<DataFixService> _logger;

    public DataFixService(
        AppDbContext context,
        IProductionRecordService productionRecordService,
        IPurchaseOrderService purchaseOrderService,
        ISubcontractOrderService subcontractOrderService,
        ILogger<DataFixService> logger)
    {
        _context = context;
        _productionRecordService = productionRecordService;
        _purchaseOrderService = purchaseOrderService;
        _subcontractOrderService = subcontractOrderService;
        _logger = logger;
    }

    public async Task<DataFixReport> FixAllAsync()
    {
        var report = new DataFixReport();

        report.SequenceNumbersFixed = await FixSequenceNumbersAsync();
        report.OutsourceStatusFixed = await FixSectionOutsourceStatusAsync();
        report.BatchTrackingFixed = await FixBatchTrackingAsync();
        await FixPurchaseOrdersAsync();
        await FixSubcontractOrdersAsync();
        report.EquipmentFixed = await FixEquipmentTrackingAsync();

        _logger.LogInformation("全字段修复完成，总计 {Total} 条", report.Total);
        return report;
    }

    // ==================== 1. 修复组内序号 ====================

    private async Task<int> FixSequenceNumbersAsync()
    {
        var processGroups = await _context.Set<ProcessGroup>()
            .Include(pg => pg.ProductionBatch)
            .ToListAsync();

        var pgLookup = new Dictionary<string, ProcessGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var pg in processGroups)
        {
            var key = $"{pg.ProductionBatch.BatchNo}|{pg.ProcessName}|{pg.ManufacturingSpec ?? ""}";
            if (!pgLookup.ContainsKey(key))
                pgLookup[key] = pg;
        }

        int totalFixed = 0;

        // ProductionRecord
        var records = await _context.Set<ProductionRecord>()
            .Include(r => r.ProductionBatch)
            .ToListAsync();
        foreach (var rec in records)
        {
            var key = $"{rec.ProductionBatch.BatchNo}|{rec.ProcessName}|{rec.ManufacturingSpec ?? ""}";
            if (pgLookup.TryGetValue(key, out var pg))
            {
                bool changed = false;

                // 修正 SequenceNumber
                var newSeq = GetSectionSequenceNumber(pg, rec.SectionName);
                if (newSeq.HasValue && rec.SequenceNumber != newSeq.Value)
                {
                    rec.SequenceNumber = newSeq.Value;
                    changed = true;
                }

                // 修正 ProcessGroupId（旧版导入可能指向了错误的工序组）
                if (rec.ProcessGroupId != pg.Id)
                {
                    rec.ProcessGroupId = pg.Id;
                    changed = true;
                }

                if (changed) totalFixed++;
            }
        }

        // ProcessInspection
        var inspections = await _context.Set<ProcessInspection>()
            .Include(r => r.ProductionBatch)
            .ToListAsync();
        foreach (var insp in inspections)
        {
            var key = $"{insp.ProductionBatch.BatchNo}|{insp.ProcessName}|{insp.ManufacturingSpec ?? ""}";
            if (pgLookup.TryGetValue(key, out var pg))
            {
                bool changed = false;

                var newSeq = GetSectionSequenceNumber(pg, insp.SectionName);
                if (newSeq.HasValue && insp.SequenceNumber != newSeq.Value)
                {
                    insp.SequenceNumber = newSeq.Value;
                    changed = true;
                }

                if (insp.ProcessGroupId != pg.Id)
                {
                    insp.ProcessGroupId = pg.Id;
                    changed = true;
                }

                if (changed) totalFixed++;
            }
        }

        // SectionOutsource
        var outsources = await _context.Set<SectionOutsource>()
            .Include(r => r.ProductionBatch)
            .ToListAsync();
        foreach (var os in outsources)
        {
            var key = $"{os.ProductionBatch.BatchNo}|{os.ProcessName}|{os.ManufacturingSpec ?? ""}";
            if (pgLookup.TryGetValue(key, out var pg))
            {
                bool changed = false;

                var newSeq = GetSectionSequenceNumber(pg, os.SectionName);
                if (newSeq.HasValue && os.SequenceNumber != newSeq.Value)
                {
                    os.SequenceNumber = newSeq.Value;
                    changed = true;
                }

                if (os.ProcessGroupId != pg.Id)
                {
                    os.ProcessGroupId = pg.Id;
                    changed = true;
                }

                if (changed) totalFixed++;
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("组内序号修复完成: {Count} 条", totalFixed);
        return totalFixed;
    }

    // ==================== 2. 修复工段委外状态 ====================

    private async Task<int> FixSectionOutsourceStatusAsync()
    {
        // 用 SQL 聚合查询替代逐条加载
        var recoveryStats = await _context.Set<OutsourceRecovery>()
            .GroupBy(or => or.SectionOutsourceId)
            .Select(g => new
            {
                SectionOutsourceId = g.Key,
                TotalRecoveredWeight = g.Sum(or => (or.RecoveryWeight ?? 0) + (or.UnprocessedWeight ?? 0))
            })
            .ToListAsync();

        var recoveryLookup = recoveryStats.ToDictionary(x => x.SectionOutsourceId, x => x.TotalRecoveredWeight);

        var allOutsources = await _context.Set<SectionOutsource>().ToListAsync();
        int fixedCount = 0;

        foreach (var os in allOutsources)
        {
            var totalRecovered = recoveryLookup.GetValueOrDefault(os.Id, 0m);
            var correctStatus = (os.SendWeight.HasValue && os.SendWeight.Value > 0
                                 && totalRecovered >= os.SendWeight.Value * 0.99m)
                ? SectionOutsourceStatus.Recovered
                : SectionOutsourceStatus.PendingRecovery;

            if (os.Status != correctStatus)
            {
                os.Status = correctStatus;
                fixedCount++;
            }
        }

        if (fixedCount > 0)
            await _context.SaveChangesAsync();

        _logger.LogInformation("工段委外状态修复完成: {Count} 条", fixedCount);
        return fixedCount;
    }

    // ==================== 3. 修复批次跟踪字段 ====================

    private async Task<int> FixBatchTrackingAsync()
    {
        var batchIds = await _context.Set<ProductionBatch>()
            .Where(b => b.IsForceCompleted != true)
            .Select(b => b.Id)
            .ToListAsync();

        if (batchIds.Count == 0) return 0;

        await _productionRecordService.BatchUpdateBatchTrackingAsync(batchIds);

        _logger.LogInformation("批次跟踪字段修复完成: {Count} 个批次", batchIds.Count);
        return batchIds.Count;
    }

    // ==================== 4. 修复采购订单 ====================

    private async Task FixPurchaseOrdersAsync()
    {
        await _purchaseOrderService.SyncAllAsync();
        _logger.LogInformation("采购订单同步完成");
    }

    // ==================== 5. 修复委外订单 ====================

    private async Task FixSubcontractOrdersAsync()
    {
        await _subcontractOrderService.SyncAllAsync();
        _logger.LogInformation("委外订单同步完成");
    }

    // ==================== 6. 修复设备日期字段 ====================

    private async Task<int> FixEquipmentTrackingAsync()
    {
        var equipments = await _context.Set<Equipment>().ToListAsync();
        int fixedCount = 0;

        foreach (var eq in equipments)
        {
            bool changed = false;

            // 最近点检日期
            var maxInspectionDate = await _context.Set<InspectionRecord>()
                .Where(ir => ir.EquipmentId == eq.Id && ir.ActualDate != null)
                .MaxAsync(ir => (DateTime?)ir.ActualDate);
            if (maxInspectionDate != eq.LastInspectionDate)
            {
                eq.LastInspectionDate = maxInspectionDate;
                changed = true;
            }

            // 最近保养日期
            var maxMaintDate = await _context.Set<MaintenanceOrder>()
                .Where(mo => mo.EquipmentId == eq.Id && mo.ActualDate != null)
                .MaxAsync(mo => (DateTime?)mo.ActualDate);
            if (maxMaintDate != eq.LastMaintDate)
            {
                eq.LastMaintDate = maxMaintDate;
                changed = true;
            }

            // 最近维修日期
            var maxRepairDate = await _context.Set<RepairOrder>()
                .Where(ro => ro.EquipmentId == eq.Id && ro.RepairEndTime != null)
                .MaxAsync(ro => (DateTime?)ro.RepairEndTime);
            if (maxRepairDate != eq.LastRepairDate)
            {
                eq.LastRepairDate = maxRepairDate;
                changed = true;
            }

            if (changed) fixedCount++;
        }

        if (fixedCount > 0)
            await _context.SaveChangesAsync();

        _logger.LogInformation("设备日期字段修复完成: {Count} 台", fixedCount);
        return fixedCount;
    }

    // ==================== 工具方法 ====================

    private static int? GetSectionSequenceNumber(ProcessGroup pg, string sectionName)
    {
        return sectionName switch
        {
            "冷轧拔" => pg.ColdRollDraw,
            "油管断" => pg.OilPipeCut,
            "切管" => pg.OilPipeCut,
            "去油" => pg.Degrease,
            "脱脂" => pg.Degrease,
            "固溶" => pg.Solution,
            "矫直" => pg.Straighten,
            "断切" => pg.Cut,
            "测壁厚" => pg.ThicknessMeasure,
            "测厚" => pg.ThicknessMeasure,
            "酸洗" => pg.Pickle,
            "外抛光" => pg.OuterPolish,
            "外抛" => pg.OuterPolish,
            "内修磨" => pg.InnerGrinding,
            "内磨" => pg.InnerGrinding,
            "外点磨" => pg.OuterSpotGrinding,
            "探伤" => pg.Inspection,
            "检验" => pg.Inspection,
            "打焊头" => pg.WeldingHead,
            "焊头" => pg.WeldingHead,
            "润滑" => pg.Lubrication,
            "入库" => pg.Warehouse,
            _ => null
        };
    }
}
