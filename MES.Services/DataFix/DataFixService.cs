using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Interfaces.Batch;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.DataExchange;
using MES.Core.Interfaces.Materials;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Order;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Quality;
using MES.Services.Extensions;
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
            report.ProcessGroupSectionNumbersFixed = await FixProcessGroupSectionNumbersAsync();
            report.SequenceNumbersFixed = await FixSequenceNumbersAsync();
            report.OutsourceStatusFixed = await FixSectionOutsourceStatusAsync();
            report.BatchTrackingFixed = await FixBatchTrackingAsync();
            await FixPurchaseOrdersAsync();
            await FixSubcontractOrdersAsync();
            report.EquipmentFixed = await FixEquipmentTrackingAsync();
            await FixWorkOrderSummariesAsync();
            report.SalesOrderSnapshotFixed = await FixSalesOrderSnapshotsAsync();

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
            // 孤儿记录（批次外键指向已删/不存在的批次，导入禁 FK 约束时可能产生）跳过，避免 NRE
            if (rec.ProductionBatch == null) continue;

            var key = $"{rec.ProductionBatch.BatchNo}|{rec.ProcessName}|{rec.ManufacturingSpec ?? ""}";
            if (pgLookup.TryGetValue(key, out var pg))
            {
                bool changed = false;

                // 修正 SequenceNumber
                var newSeq = pg.GetSectionSequence(rec.SectionName);
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
            // 孤儿记录跳过，避免 NRE
            if (insp.ProductionBatch == null) continue;

            var key = $"{insp.ProductionBatch.BatchNo}|{insp.ProcessName}|{insp.ManufacturingSpec ?? ""}";
            if (pgLookup.TryGetValue(key, out var pg))
            {
                bool changed = false;

                var newSeq = pg.GetSectionSequence(insp.SectionName);
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
            // 孤儿记录跳过，避免 NRE
            if (os.ProductionBatch == null) continue;

            var key = $"{os.ProductionBatch.BatchNo}|{os.ProcessName}|{os.ManufacturingSpec ?? ""}";
            if (pgLookup.TryGetValue(key, out var pg))
            {
                bool changed = false;

                var newSeq = pg.GetSectionSequence(os.SectionName);
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

        // PicklingInRecord（入缸）：按 ProcessGroupId 直接定位工序组对齐序号（语义A：序号=工段步骤号）
        var picklingPgMap = processGroups.ToDictionary(pg => pg.Id);
        var picklingRecords = await _context.Set<PicklingInRecord>().ToListAsync();
        foreach (var pr in picklingRecords)
        {
            if (pr.ProcessGroupId > 0 && picklingPgMap.TryGetValue(pr.ProcessGroupId, out var pg))
            {
                var newSeq = pg.GetSectionSequence(pr.SectionName);
                if (newSeq.HasValue && pr.SequenceNumber != newSeq.Value)
                {
                    pr.SequenceNumber = newSeq.Value;
                    totalFixed++;
                }
            }
        }

        // MaterialReceiveCheck（成检到料）：序号 = 检验工段步骤号（pg.Inspection），按 ProcessGroupId 定位
        var receiveChecks = await _context.Set<MaterialReceiveCheck>().ToListAsync();
        foreach (var rc in receiveChecks)
        {
            if (rc.ProcessGroupId > 0 && picklingPgMap.TryGetValue(rc.ProcessGroupId, out var pg))
            {
                var newSeq = pg.Inspection;
                if (newSeq.HasValue && rc.SequenceNumber != newSeq.Value)
                {
                    rc.SequenceNumber = newSeq.Value;
                    totalFixed++;
                }
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("组内序号修复完成: {Count} 条", totalFixed);
        return totalFixed;
    }

    // ==================== 1.5 工序组工段步骤号连续化 ====================

    /// <summary>
    /// 按批次将工序组全部非空工段步骤号重排为 1..N 连续（仅补缺号、不改变相对执行顺序）。
    /// 记录序号 = 工段步骤号（语义A），工段步骤号缺号不连续会直接导致 FlowJudgment 规则2 误报疑问，故先连续化。
    /// </summary>
    private async Task<int> FixProcessGroupSectionNumbersAsync()
    {
        var processGroups = await _context.Set<ProcessGroup>().ToListAsync();
        var groupsByBatch = processGroups
            .Where(pg => pg.ProductionBatchId > 0)
            .GroupBy(pg => pg.ProductionBatchId);

        // 工段定义顺序（同序号冲突时的确定性排序键）
        var sectionOrder = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < SectionKeys.All.Length; i++)
            sectionOrder[SectionKeys.All[i]] = i;

        int totalFixed = 0;

        foreach (var batchGroup in groupsByBatch)
        {
            // 收集该批次全部非空工段（工段名 → 当前步骤号 → 所属工序组）
            var sections = new List<(string SectionName, int CurrentSeq, ProcessGroup Pg)>();
            foreach (var pg in batchGroup)
            {
                foreach (var (name, seq) in pg.GetNonEmptySections())
                    sections.Add((name, seq, pg));
            }

            if (sections.Count == 0) continue;

            // 按当前步骤号升序稳定排序（保持相对执行顺序），再按工序组序号 + 工段定义顺序保证确定性
            var ordered = sections
                .OrderBy(s => s.CurrentSeq)
                .ThenBy(s => s.Pg.SequenceNumber)
                .ThenBy(s => sectionOrder.GetValueOrDefault(SectionKeys.ToKey(s.SectionName) ?? "", int.MaxValue))
                .ToList();

            // 重新编号 1..N 连续
            for (int i = 0; i < ordered.Count; i++)
            {
                var (sectionName, currentSeq, pg) = ordered[i];
                var newSeq = i + 1;
                if (currentSeq != newSeq)
                {
                    pg.SetSectionNumber(sectionName, newSeq);
                    totalFixed++;
                }
            }
        }

        if (totalFixed > 0)
            await _context.SaveChangesAsync();

        _logger.LogInformation("工序组工段步骤号连续化完成: {Count} 处", totalFixed);
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
        var equipments = await _context.Set<MES.Data.Entities.Equipment.Equipment>().ToListAsync();

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

    // ==================== 7. 刷新工单汇总读模型（用料计划字段） ====================

    private async Task FixWorkOrderSummariesAsync()
    {
        _logger.LogInformation("开始刷新工单执行状况汇总");
        await _workOrderExecutionService.RefreshAllAsync();
        _logger.LogInformation("工单执行状况汇总刷新完成");
    }

    // ==================== 8. 修复订单客户快照字段 ====================

    private async Task<int> FixSalesOrderSnapshotsAsync()
    {
        var salesOrders = await _context.SalesOrders
            .Where(so => string.IsNullOrEmpty(so.CustomerName) || string.IsNullOrEmpty(so.Salesman) || string.IsNullOrEmpty(so.EndCustomer))
            .ToListAsync();

        int fixedCount = 0;
        foreach (var so in salesOrders)
        {
            // CustomerId FK 已移除，快照字段已独立维护，此修复脚本保留骨架以备手动处理
            bool changed = false;
            if (string.IsNullOrEmpty(so.CustomerName))
            {
                so.CustomerName = "未知客户";
                changed = true;
            }

            if (string.IsNullOrEmpty(so.Salesman))
            {
                so.Salesman = "未知";
                changed = true;
            }

            if (string.IsNullOrEmpty(so.EndCustomer))
            {
                so.EndCustomer = "未知客户";
                changed = true;
            }

            // 仅实际修改的订单才计数，避免报告虚高
            if (changed) fixedCount++;
        }

        if (fixedCount > 0)
            await _context.SaveChangesAsync();

        _logger.LogInformation("订单客户快照字段修复完成: {Count} 条", fixedCount);
        return fixedCount;
    }
}
