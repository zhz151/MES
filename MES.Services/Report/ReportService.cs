using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Report;
using MES.Core.DTOs.Shared;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Report;
using MES.Data;
using MES.Services.Printing;

namespace MES.Services.Report;

/// <summary>
/// 报表服务 — 跨上下文聚合查询，只读操作
/// </summary>
public class ReportService : IReportService
{
    private readonly AppDbContext _context;

    public ReportService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 获取产量报表数据（日期范围聚合）
    /// 数据来源：
    ///   - 投料荒管 → ProductionBatch (SourceMaterialType=RoughTube, InputWeight)
    ///   - 各工段产量 → ProductionRecord.Weight + OutsourceRecovery.RecoveryWeight
    ///   - 过程检验 → ProcessInspection.Weight
    ///   - 成品入库 → InventoryBatch (MaterialType=OrderFinished, InitialWeight)
    /// </summary>
    public async Task<DailyProductionReportResponse> GetDailyProductionReportAsync(DateTime fromDate, DateTime toDate)
    {
        var rangeFrom = fromDate.Date;
        var rangeTo = toDate.Date.AddDays(1); // 包含 toDate 全天

        // 1. 投料荒管 — 批次中原料类型为"荒管"的投料重量，按 InboundDate 分组
        var roughTubeData = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => b.SourceMaterialType == InventoryMaterialTypes.RoughTube && b.InboundDate.HasValue
                        && b.InboundDate >= rangeFrom && b.InboundDate < rangeTo)
            .GroupBy(b => b.InboundDate!.Value.Date)
            .Select(g => new { Date = g.Key, Weight = g.Sum(b => b.InputWeight ?? 0m) })
            .ToListAsync();

        // 2. 生产记录各工段产量 — 按 ExecDate + SectionName 分组
        var recordData = await _context.ProductionRecords
            .AsNoTracking()
            .Where(r => r.ExecDate >= rangeFrom && r.ExecDate < rangeTo)
            .GroupBy(r => new { r.ExecDate.Date, r.SectionName })
            .Select(g => new { g.Key.Date, g.Key.SectionName, Weight = g.Sum(r => r.Weight ?? 0m) })
            .ToListAsync();

        // 3. 工段委外回收 — 通过 OutsourceRecovery 关联 SectionOutsource 取 SectionName
        var outsourceData = await (
            from r in _context.OutsourceRecoveries.AsNoTracking()
            join s in _context.SectionOutsources.AsNoTracking() on r.SectionOutsourceId equals s.Id
            where r.RecoveryDate >= rangeFrom && r.RecoveryDate < rangeTo
            group new { r, s } by new { r.RecoveryDate.Date, s.SectionName } into g
            select new { g.Key.Date, g.Key.SectionName, Weight = g.Sum(x => x.r.RecoveryWeight ?? 0m) }
        ).ToListAsync();

        // 4. 过程检验 — 按 InspectionDate 分组
        var inspectionData = await _context.ProcessInspections
            .AsNoTracking()
            .Where(p => p.InspectionDate >= rangeFrom && p.InspectionDate < rangeTo)
            .GroupBy(p => p.InspectionDate.Date)
            .Select(g => new { Date = g.Key, Weight = g.Sum(p => p.Weight ?? 0m) })
            .ToListAsync();

        // 5. 成品入库 — InventoryBatch 中 MaterialType=OrderFinished，按 InboundDate 分组
        var finishedGoodsData = await _context.InventoryBatches
            .AsNoTracking()
            .Where(i => i.MaterialType == InventoryMaterialTypes.OrderFinished && i.InboundDate >= rangeFrom && i.InboundDate < rangeTo)
            .GroupBy(i => i.InboundDate.Date)
            .Select(g => new { Date = g.Key, Weight = g.Sum(i => i.InitialWeight) })
            .ToListAsync();

        // 6. 合并所有数据为透视表
        var dateSet = new HashSet<DateTime>();
        var sectionSet = new HashSet<string>();

        // 收集所有出现过的工段名称
        foreach (var d in recordData) { dateSet.Add(d.Date); sectionSet.Add(d.SectionName); }
        foreach (var d in outsourceData) { dateSet.Add(d.Date); sectionSet.Add(d.SectionName); }
        foreach (var d in roughTubeData) dateSet.Add(d.Date);
        foreach (var d in inspectionData) dateSet.Add(d.Date);
        foreach (var d in finishedGoodsData) dateSet.Add(d.Date);

        // 固定特殊列
        const string colRoughTube = "投料荒管";
        const string colInspection = "过程检验";
        const string colFinishedGoods = "成品入库";

        // 所有标准工段列（即使无数据也展示）
        var orderedSections = new List<string> { colRoughTube };
        orderedSections.AddRange(SectionDefs.All);
        orderedSections.Add(colInspection);
        orderedSections.Add(colFinishedGoods);

        // 额外收集不在标准列表中的工段（数据库中有但标准定义未覆盖的）
        var extraSections = sectionSet
            .Where(s => !SectionDefs.All.Contains(s)
                        && s != colRoughTube && s != colInspection && s != colFinishedGoods)
            .OrderBy(s => s)
            .ToList();
        orderedSections.AddRange(extraSections);

        // 构建数据行
        var weekdays = new[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
        var rows = dateSet.OrderBy(d => d).Select(date =>
        {
            var values = new Dictionary<string, decimal>();

            // 投料荒管
            values[colRoughTube] = roughTubeData
                .Where(d2 => d2.Date == date)
                .Sum(d2 => d2.Weight);

            // 过程检验
            values[colInspection] = inspectionData
                .Where(d2 => d2.Date == date)
                .Sum(d2 => d2.Weight);

            // 成品入库
            values[colFinishedGoods] = finishedGoodsData
                .Where(d2 => d2.Date == date)
                .Sum(d2 => d2.Weight);

            // 其余工段：生产记录重量 + 委外回收重量
            foreach (var section in sectionSet)
            {
                var recordWeight = recordData
                    .Where(d2 => d2.Date == date && d2.SectionName == section)
                    .Sum(d2 => d2.Weight);

                var outsourceWeight = outsourceData
                    .Where(d2 => d2.Date == date && d2.SectionName == section)
                    .Sum(d2 => d2.Weight);

                values[section] = recordWeight + outsourceWeight;
            }

            return new DailyProductionReportRow
            {
                Date = date,
                DisplayDate = $"{date:MM-dd}({weekdays[(int)date.DayOfWeek]})",
                Values = values
            };
        }).ToList();

        return new DailyProductionReportResponse
        {
            SectionColumns = orderedSections,
            Rows = rows
        };
    }

    /// <summary>
    /// 产量报表打印 — 生成 PDF
    /// </summary>
    public async Task<byte[]> PrintDailyProductionReportAsync(DateTime fromDate, DateTime toDate, List<PrintColumnDef>? columns)
    {
        var report = await GetDailyProductionReportAsync(fromDate, toDate);
        if (report.Rows.Count == 0)
            throw new BusinessException("选定日期范围内暂无数据");

        var visibleColumnKeys = columns?.Select(c => c.Key).ToList();
        var title = $"产量报表（{fromDate:yyyy-MM-dd} ~ {toDate:yyyy-MM-dd}）";
        return ReportPrintHelper.GenerateProductionReportPdf(title, report, visibleColumnKeys);
    }
}
