using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Extensions;

namespace MES.Services.Scheduling;

/// <summary>
/// 生产工段待产量现况服务 — 按(工序组, 工段)维度汇总批次现有效原料重量
/// </summary>
public class SectionProductionStatusService : ISectionProductionStatusService
{
    private readonly AppDbContext _context;

    public SectionProductionStatusService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<SectionProductionStatusDto>> GetStatusAsync()
    {
        // 1. 加载未完成的批次（含工序组），用于构建维度集合并聚合计算
        var allBatches = await _context.Set<ProductionBatch>()
            .AsNoTracking()
            .Include(b => b.ProcessGroups)
            .Where(b => b.Status != BatchStatus.Completed)
            .ToListAsync();

        // 2. 构建维度集合：所有工序组的(工序组名称, 工段名称)唯一组合（排除"入库"工段）
        var dimensions = allBatches
            .SelectMany(b => b.ProcessGroups)
            .SelectMany(pg => pg.GetNonEmptySections()
                .Where(s => s.SectionName != SectionDefs.Warehouse)
                .Select(s => (ProcessGroupName: pg.ProcessName, s.SectionName)))
            .Distinct()
            .OrderBy(d => d.ProcessGroupName)
            .ThenBy(d => d.SectionName)
            .ToList();

        if (dimensions.Count == 0)
            return new List<SectionProductionStatusDto>();

        // 3. 每批次的最后一道工序名称映射
        var batchLastProcessMap = allBatches.ToDictionary(
            b => b.Id,
            b => b.ProcessGroups
                .OrderByDescending(pg => pg.SequenceNumber)
                .Select(pg => pg.ProcessName)
                .FirstOrDefault()
        );

        // 4. 预聚合字典：O(1) 查找替代 O(dimensions × batches) 嵌套扫描
        // 生产中：(CurrentGroupName, CurrentSectionName) → 重量合计
        var inProdLookup = allBatches
            .Where(b => b.CurrentSectionCompleted == false
                     && !string.IsNullOrEmpty(b.CurrentGroupName)
                     && !string.IsNullOrEmpty(b.CurrentSectionName))
            .GroupBy(b => (Group: b.CurrentGroupName, Section: b.CurrentSectionName))
            .ToDictionary(g => g.Key, g => g.Sum(b => b.CurrentValidWeight ?? 0m));

        // 待产量：(NextProcess, NextSectionName) → 重量合计
        var pendingLookup = allBatches
            .Where(b => b.CurrentSectionCompleted == true
                     && !string.IsNullOrEmpty(b.NextProcess)
                     && !string.IsNullOrEmpty(b.NextSectionName))
            .GroupBy(b => (Group: b.NextProcess, Section: b.NextSectionName))
            .ToDictionary(g => g.Key, g => g.Sum(b => b.CurrentValidWeight ?? 0m));

        // 5. 属成品工序量的预聚合字典（仅统计处于最后工序的批次）
        // 生产中 — 当前工序组 = 该批次的最后工序
        var finalInProdLookup = allBatches
            .Where(b => b.CurrentSectionCompleted == false
                     && !string.IsNullOrEmpty(b.CurrentGroupName)
                     && !string.IsNullOrEmpty(b.CurrentSectionName)
                     && batchLastProcessMap.TryGetValue(b.Id, out var last)
                     && !string.IsNullOrEmpty(last)
                     && b.CurrentGroupName == last)
            .GroupBy(b => (Group: b.CurrentGroupName, Section: b.CurrentSectionName))
            .ToDictionary(g => g.Key, g => g.Sum(b => b.CurrentValidWeight ?? 0m));

        // 待产量 — 下一工序 = 该批次的最后工序
        var finalPendingLookup = allBatches
            .Where(b => b.CurrentSectionCompleted == true
                     && !string.IsNullOrEmpty(b.NextProcess)
                     && !string.IsNullOrEmpty(b.NextSectionName)
                     && batchLastProcessMap.TryGetValue(b.Id, out var last)
                     && !string.IsNullOrEmpty(last)
                     && b.NextProcess == last)
            .GroupBy(b => (Group: b.NextProcess, Section: b.NextSectionName))
            .ToDictionary(g => g.Key, g => g.Sum(b => b.CurrentValidWeight ?? 0m));

        // 6. 按维度填充
        var result = new List<SectionProductionStatusDto>(dimensions.Count);
        foreach (var (processGroupName, sectionName) in dimensions)
        {
            var key = (Group: processGroupName, Section: sectionName);
            var inProduction = inProdLookup.GetValueOrDefault(key);
            var pendingProduction = pendingLookup.GetValueOrDefault(key);
            var total = inProduction + pendingProduction;

            // 属成品工序量：仅统计此维度下处于最后工序的批次重量
            var finalTotal = finalInProdLookup.GetValueOrDefault(key)
                           + finalPendingLookup.GetValueOrDefault(key);

            result.Add(new SectionProductionStatusDto
            {
                ProcessGroupName = processGroupName,
                SectionName = sectionName,
                InProduction = inProduction > 0 ? inProduction : null,
                PendingProduction = pendingProduction > 0 ? pendingProduction : null,
                Total = total > 0 ? total : null,
                FinalProcessTotal = finalTotal > 0 ? finalTotal : null,
            });
        }

        return result;
    }
}
