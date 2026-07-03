using Microsoft.EntityFrameworkCore;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Storage;

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
    private readonly IConfigParameterService _configService;
    private readonly IWorkOrderExecutionService _workOrderExecutionService;
    private readonly Dictionary<string, Dictionary<string, decimal>> _configMaps = new();

    public DataFixService(
        AppDbContext context,
        IProductionRecordService productionRecordService,
        IPurchaseOrderService purchaseOrderService,
        ISubcontractOrderService subcontractOrderService,
        ILogger<DataFixService> logger,
        IConfigParameterService configService,
        IWorkOrderExecutionService workOrderExecutionService)
    {
        _context = context;
        _productionRecordService = productionRecordService;
        _purchaseOrderService = purchaseOrderService;
        _subcontractOrderService = subcontractOrderService;
        _logger = logger;
        _configService = configService;
        _workOrderExecutionService = workOrderExecutionService;
    }

    private async Task<decimal> GetConfigAsync(string category, string key, decimal defaultValue)
    {
        if (!_configMaps.TryGetValue(category, out var map))
        {
            map = await _configService.GetConfigMapAsync(category);
            _configMaps[category] = map;
        }
        return map.GetValueOrDefault(key, defaultValue);
    }

    public async Task<DataFixReport> FixAllAsync()
    {
        var report = new DataFixReport();

        // 整个修复过程在单个事务中执行：任一步骤失败则全部回滚
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            report.SequenceNumbersFixed = await FixSequenceNumbersAsync();
            report.OutsourceStatusFixed = await FixSectionOutsourceStatusAsync();
            report.BatchTrackingFixed = await FixBatchTrackingAsync();
            await FixPurchaseOrdersAsync();
            await FixSubcontractOrdersAsync();
            report.EquipmentFixed = await FixEquipmentTrackingAsync();
            await FixWorkOrderSummariesAsync();

            await transaction.CommitAsync();
            _logger.LogInformation("全字段修复完成，总计 {Total} 条", report.Total);
        }
        catch
        {
            await transaction.RollbackAsync();
            _logger.LogError("全字段修复失败，已回滚");
            throw;
        }

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
                var newSeq = pg.GetSectionSequence( rec.SectionName);
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

                var newSeq = pg.GetSectionSequence( insp.SectionName);
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

                var newSeq = pg.GetSectionSequence( os.SectionName);
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

        var outsourceRecoveryRatio = await GetConfigAsync("WarehouseThreshold", "OutsourceRecoveryRatio", 0.99m);
        var allOutsources = await _context.Set<SectionOutsource>().ToListAsync();
        int fixedCount = 0;

        foreach (var os in allOutsources)
        {
            var totalRecovered = recoveryLookup.GetValueOrDefault(os.Id, 0m);
            var correctStatus = (os.SendWeight.HasValue && os.SendWeight.Value > 0
                                 && totalRecovered >= os.SendWeight.Value * outsourceRecoveryRatio)
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
        var allBatchIds = await _context.Set<ProductionBatch>()
            .Select(b => b.Id)
            .ToListAsync();

        if (allBatchIds.Count == 0) return 0;

        // 全部批次参与跟踪字段刷新（包含已完成/强制完成，确保全工量等字段被计算）
        await _productionRecordService.BatchUpdateBatchTrackingAsync(allBatchIds);

        _logger.LogInformation("批次跟踪字段修复完成: {Count} 个批次", allBatchIds.Count);
        return allBatchIds.Count;
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
        var equipments = await _context.Set<MES.Data.Entities.Equipment>().ToListAsync();

        // 一次查询所有点检记录的最大日期（按设备分组）
        var maxInsDates = await _context.Set<InspectionRecord>()
            .Where(ir => ir.ActualDate != null)
            .GroupBy(ir => ir.EquipmentId)
            .Select(g => new { EquipmentId = g.Key, MaxDate = g.Max(ir => (DateTime?)ir.ActualDate) })
            .ToDictionaryAsync(x => x.EquipmentId, x => x.MaxDate);

        // 一次查询所有保养记录的最大日期（按设备分组）
        var maxMaintDates = await _context.Set<MaintenanceOrder>()
            .Where(mo => mo.ActualDate != null)
            .GroupBy(mo => mo.EquipmentId)
            .Select(g => new { EquipmentId = g.Key, MaxDate = g.Max(mo => (DateTime?)mo.ActualDate) })
            .ToDictionaryAsync(x => x.EquipmentId, x => x.MaxDate);

        // 一次查询所有维修记录的最大日期（按设备分组）
        var maxRepairDates = await _context.Set<RepairOrder>()
            .Where(ro => ro.RepairEndTime != null)
            .GroupBy(ro => ro.EquipmentId)
            .Select(g => new { EquipmentId = g.Key, MaxDate = g.Max(ro => (DateTime?)ro.RepairEndTime) })
            .ToDictionaryAsync(x => x.EquipmentId, x => x.MaxDate);

        int fixedCount = 0;
        foreach (var eq in equipments)
        {
            bool changed = false;

            var maxInspectionDate = maxInsDates.GetValueOrDefault(eq.Id);
            if (maxInspectionDate != eq.LastInspectionDate)
            {
                eq.LastInspectionDate = maxInspectionDate;
                changed = true;
            }

            var maxMaintDate = maxMaintDates.GetValueOrDefault(eq.Id);
            if (maxMaintDate != eq.LastMaintDate)
            {
                eq.LastMaintDate = maxMaintDate;
                changed = true;
            }

            var maxRepairDate = maxRepairDates.GetValueOrDefault(eq.Id);
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

    private static string GetDeliveryStateChinese(DeliveryState state)
    {
        return state switch
        {
            DeliveryState.SolutionAnnealedAndPickled => "固溶酸洗",
            DeliveryState.SolutionAnnealedAndPickledUTube => "固溶酸洗-U型管",
            DeliveryState.SolutionAnnealedAndPickledExternalPolished => "固溶酸洗-外抛光",
            DeliveryState.SolutionAnnealedAndPickledInternalPolished => "固溶酸洗-内抛光",
            DeliveryState.SolutionAnnealedAndPickledBothPolished => "固溶酸洗-内外抛光",
            DeliveryState.SolutionAnnealedAndPickledCoiled => "固溶酸洗-盘管",
            DeliveryState.Bright => "光亮",
            DeliveryState.BrightUTube => "光亮-U型管",
            DeliveryState.BrightCoiled => "光亮-盘管",
            DeliveryState.Hard => "硬态",
            _ => state.ToString()
        };
    }

    private static string GetRawMaterialTypeChinese(RawMaterialType type)
    {
        return type switch
        {
            RawMaterialType.SemiFinished => "荒管",
            RawMaterialType.SemiProduct => "半成品",
            RawMaterialType.RoundBar => "圆棒",
            _ => type.ToString()
        };
    }

    // ==================== 工具方法 ====================

    // ==================== 8. 刷新工单汇总读模型（用料计划字段） ====================

    private async Task FixWorkOrderSummariesAsync()
    {
        _logger.LogInformation("开始刷新工单执行状况汇总");
        await _workOrderExecutionService.RefreshAllAsync();
        _logger.LogInformation("工单执行状况汇总刷新完成");
    }
}
