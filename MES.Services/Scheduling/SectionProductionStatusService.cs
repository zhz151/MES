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
        // 1a. 加载所有批次的工序组（含已完成），用于构建完整维度集合
        var allBatches = await _context.Set<ProductionBatch>()
            .AsNoTracking()
            .Include(b => b.ProcessGroups)
            .ToListAsync();

        // 1b. 加载未完成的批次，用于聚合计算
        var batches = allBatches
            .Where(b => b.Status != BatchStatus.Completed)
            .ToList();

        // 2. 构建维度集合：所有批次（含已完成）中所有工序组的(工序组名称, 工段名称)唯一组合
        //    排除"入库"工段
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

        // 3. 对每个批次，计算其最后一道工序的名称（最大 SequenceNumber 的工序组）
        var batchLastProcessMap = batches.ToDictionary(
            b => b.Id,
            b => b.ProcessGroups
                .OrderByDescending(pg => pg.SequenceNumber)
                .Select(pg => pg.ProcessName)
                .FirstOrDefault()
        );

        // 4. 按维度聚合
        var result = new List<SectionProductionStatusDto>(dimensions.Count);
        foreach (var (processGroupName, sectionName) in dimensions)
        {
            // 生产中：当前工序/工段匹配且工段未完工
            var inProduction = batches
                .Where(b => b.CurrentGroupName == processGroupName
                         && b.CurrentSectionName == sectionName
                         && b.CurrentSectionCompleted == false)
                .Sum(b => (decimal?)b.CurrentValidWeight)
                ?? 0m;

            // 待产量：下一工序/工段匹配且工段已完工
            var pendingProduction = batches
                .Where(b => b.NextProcess == processGroupName
                         && b.NextSectionName == sectionName
                         && b.CurrentSectionCompleted == true)
                .Sum(b => (decimal?)b.CurrentValidWeight)
                ?? 0m;

            var total = inProduction + pendingProduction;

            // 属成品工序量：仅统计涉及该批次最后一道工序的数据
            var finalInProduction = batches
                .Where(b => b.CurrentGroupName == processGroupName
                         && b.CurrentSectionName == sectionName
                         && b.CurrentSectionCompleted == false
                         && b.CurrentGroupName == batchLastProcessMap.GetValueOrDefault(b.Id))
                .Sum(b => (decimal?)b.CurrentValidWeight)
                ?? 0m;

            var finalPendingProduction = batches
                .Where(b => b.NextProcess == processGroupName
                         && b.NextSectionName == sectionName
                         && b.CurrentSectionCompleted == true
                         && b.NextProcess == batchLastProcessMap.GetValueOrDefault(b.Id))
                .Sum(b => (decimal?)b.CurrentValidWeight)
                ?? 0m;

            var finalTotal = finalInProduction + finalPendingProduction;

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
