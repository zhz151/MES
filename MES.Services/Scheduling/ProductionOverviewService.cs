using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services.Scheduling;

/// <summary>
/// 订单总况服务 — 聚合各工段产能负荷数据
/// </summary>
public class ProductionOverviewService
{
    private readonly AppDbContext _context;

    public ProductionOverviewService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ProductionOverviewDto> GetOverviewAsync()
    {
        var now = DateTime.Today;
        var buckets = GenerateDateBuckets(now);
        var rows = new List<OverviewRowDto>();

        // ========== 查询基础数据 ==========
        var summaries = await _context.Set<WorkOrderExecutionSummary>()
            .AsNoTracking()
            .Where(s => s.ScheduleStage >= 1)
            .Select(s => new
            {
                s.DeliveryDate,
                s.TotalWeight,
                s.PendingOutsourceFinishWeight,
                s.PendingRoughTubeWeight,
                s.InputWeight,
                s.ScheduleStage,
                s.WorkOrderNo
            })
            .ToListAsync();

        var stage1TotalWeight = summaries.Where(s => s.ScheduleStage == 1).Sum(s => (decimal)s.TotalWeight);
        var stage1OutsourceFinishWeight = summaries.Where(s => s.ScheduleStage == 1).Sum(s => (decimal)s.PendingOutsourceFinishWeight);
        var stage1InputWeight = summaries.Where(s => s.ScheduleStage == 1).Sum(s => (decimal)s.InputWeight);

        // ========== 批次数据 ==========
        var batches = await _context.Set<ProductionBatch>()
            .AsNoTracking()
            .Where(b => b.Status == BatchStatus.InProgress || b.Status == BatchStatus.None)
            .Select(b => new
            {
                b.Id,
                b.WorkOrderNo,
                b.CurrentValidWeight,
                b.CurrentGroupName,
                b.CurrentSectionName,
                b.CurrentSectionCompleted,
                b.DeliveryDate,
                b.Status
            })
            .ToListAsync();

        var batchIds = batches.Select(b => b.Id).ToList();

        var processGroups = await _context.Set<ProcessGroup>()
            .AsNoTracking()
            .Where(pg => batchIds.Contains(pg.ProductionBatchId))
            .OrderBy(pg => pg.ProductionBatchId)
            .ThenBy(pg => pg.SequenceNumber)
            .Select(pg => new ProcessGroupInfo(
                pg.ProductionBatchId,
                pg.SequenceNumber,
                pg.ProcessName,
                pg.ManufacturingSpec,
                pg.ColdRollDraw, pg.OilPipeCut, pg.Degrease, pg.Solution,
                pg.Straighten, pg.Cut, pg.ThicknessMeasure, pg.Pickle,
                pg.OuterPolish, pg.InnerGrinding, pg.OuterSpotGrinding,
                pg.Inspection, pg.WeldingHead, pg.Lubrication, pg.Warehouse))
            .ToListAsync();

        var groupsByBatch = processGroups.GroupBy(pg => pg.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // ========== 行 0: 成品采购在购 ==========
        var row0BucketTons = new List<decimal>();
        foreach (var bucket in buckets)
        {
            var tons = summaries
                .Where(s => IsInBucket(s.DeliveryDate, bucket))
                .Sum(s => s.PendingOutsourceFinishWeight);
            row0BucketTons.Add(ConvertToTons(tons));
        }

        rows.Add(new OverviewRowDto
        {
            Seq = 0,
            Category = "成品在购",
            Section = "",
            InProcurementTons = ConvertToTons(summaries.Sum(s => s.PendingOutsourceFinishWeight)),
            TotalRemainingTons = null,
            EstDays = null,
            EstDeadline = null,
            DateBucketTons = row0BucketTons
        });

        // ========== 行 1: 待投料[含在购荒管] ==========
        // 成品重量 → 原料重量按 1.1 倍换算（TotalWeight/PendingOutsourceFinishWeight 为成品重）
        const decimal rawRatio = 1.1m;
        var row1Remaining = stage1TotalWeight * rawRatio - stage1OutsourceFinishWeight * rawRatio - stage1InputWeight;
        var row1BucketTons = new List<decimal>();
        foreach (var bucket in buckets)
        {
            var stage1InBucket = summaries
                .Where(s => s.ScheduleStage == 1 && IsInBucket(s.DeliveryDate, bucket));
            var total = stage1InBucket.Sum(s => s.TotalWeight);
            var outsource = stage1InBucket.Sum(s => s.PendingOutsourceFinishWeight);
            var input = stage1InBucket.Sum(s => s.InputWeight);
            row1BucketTons.Add(ConvertToTons(Math.Max(0, total * rawRatio - outsource * rawRatio - input)));
        }

        rows.Add(new OverviewRowDto
        {
            Seq = 1,
            Category = "原料",
            Section = "待投料[含在购荒管]",
            InProcurementTons = null,
            TotalRemainingTons = ConvertToTons(row1Remaining),
            EstDays = null,
            EstDeadline = null,
            DateBucketTons = row1BucketTons
        });

        // ========== 行 2: 在购荒管 ==========
        var row2BucketTons = new List<decimal>();
        foreach (var bucket in buckets)
        {
            var tons = summaries
                .Where(s => IsInBucket(s.DeliveryDate, bucket))
                .Sum(s => s.PendingRoughTubeWeight);
            row2BucketTons.Add(ConvertToTons(tons));
        }

        rows.Add(new OverviewRowDto
        {
            Seq = 2,
            Category = "原料",
            Section = "在购荒管",
            InProcurementTons = ConvertToTons(summaries.Sum(s => s.PendingRoughTubeWeight)),
            TotalRemainingTons = null,
            EstDays = null,
            EstDeadline = null,
            DateBucketTons = row2BucketTons
        });

        // ========== 行 3-7: 各生产工段 ==========
        var sections = new[]
        {
            (Seq: 3, Section: "荒管抛光", DailyCapacity: 12m),
            (Seq: 4, Section: "50,60轧机", DailyCapacity: 11m),
            (Seq: 5, Section: "20,30轧机", DailyCapacity: 9m),
            (Seq: 6, Section: "三辊轧机", DailyCapacity: 0.5m),
            (Seq: 7, Section: "拉机", DailyCapacity: 3m),
        };

        foreach (var (seq, sectionName, dailyCapacity) in sections)
        {
            decimal totalPending = 0;
            var matchedBatchData = new List<(DateTime DeliveryDate, decimal Weight)>();

            foreach (var batch in batches)
            {
                if (batch.CurrentValidWeight == null || batch.CurrentValidWeight <= 0) continue;
                if (!groupsByBatch.TryGetValue(batch.Id, out var pgs)) continue;

                // 检查此批次的某个工序组是否属于当前工段且尚未到达
                for (int i = 0; i < pgs.Count; i++)
                {
                    var pg = pgs[i];

                    if (!ClassifySection(pg, sectionName)) continue;

                    // 判断是否尚未到达此工段
                    // 荒管抛光使用工段级比较（不依赖 CurrentSectionCompleted）
                    bool isNotReached = sectionName == "荒管抛光"
                        ? IsNotReachedBySection(
                            batch.CurrentGroupName, batch.CurrentSectionName,
                            pgs, pg, "外抛光")
                        : IsNotReached(
                            batch.CurrentGroupName, batch.CurrentSectionName,
                            batch.CurrentSectionCompleted, pgs, pg);

                    if (isNotReached)
                    {
                        var weight = (decimal)batch.CurrentValidWeight;
                        totalPending += weight;
                        matchedBatchData.Add((batch.DeliveryDate, weight));
                        break;
                    }
                }
            }

            int estDays = dailyCapacity > 0
                ? (int)Math.Ceiling(totalPending / (dailyCapacity * 1000))
                : 0;

            var rowBucketTons = new List<decimal>();
            foreach (var bucket in buckets)
            {
                var tons = matchedBatchData
                    .Where(d => IsInBucket(d.DeliveryDate, bucket))
                    .Sum(d => d.Weight);
                rowBucketTons.Add(ConvertToTons(tons));
            }

            rows.Add(new OverviewRowDto
            {
                Seq = seq,
                Category = "生产",
                Section = sectionName,
                InProcurementTons = null,
                TotalRemainingTons = ConvertToTons(totalPending),
                EstDays = estDays > 0 ? estDays : null,
                EstDeadline = estDays > 0 ? now.AddDays(estDays) : null,
                DateBucketTons = rowBucketTons
            });
        }

        return new ProductionOverviewDto
        {
            Rows = rows,
            DateBuckets = buckets.Select(b => new DateBucketDto
            {
                StartDate = b.Start,
                EndDate = b.End,
                Label = b.Label
            }).ToList(),
            GeneratedTime = now
        };
    }

    private record ProcessGroupInfo(
        int ProductionBatchId,
        int SequenceNumber,
        string ProcessName,
        string? ManufacturingSpec,
        int? ColdRollDraw, int? OilPipeCut, int? Degrease, int? Solution,
        int? Straighten, int? Cut, int? ThicknessMeasure, int? Pickle,
        int? OuterPolish, int? InnerGrinding, int? OuterSpotGrinding,
        int? Inspection, int? WeldingHead, int? Lubrication, int? Warehouse)
    {
        /// <summary>获取指定工段名称在此工序组中的执行序号，null 表示此工序组不含该工段</summary>
        public int? GetSectionSequence(string sectionName) => sectionName switch
        {
            "冷轧拔" => ColdRollDraw,
            "油管断" => OilPipeCut,
            "去油" => Degrease,
            "固溶" => Solution,
            "矫直" => Straighten,
            "断切" => Cut,
            "测壁厚" => ThicknessMeasure,
            "酸洗" => Pickle,
            "外抛光" => OuterPolish,
            "内修磨" => InnerGrinding,
            "外点磨" => OuterSpotGrinding,
            "检验" => Inspection,
            "打焊头" => WeldingHead,
            "润滑" => Lubrication,
            "入库" => Warehouse,
            _ => null
        };
    }

    private static List<(DateTime Start, DateTime End, string Label)> GenerateDateBuckets(DateTime today)
    {
        return new List<(DateTime, DateTime, string)>
        {
            (DateTime.MinValue, today, today.ToString("M/d")),
            (today.AddDays(1), today.AddDays(15), today.AddDays(15).ToString("M/d")),
            (today.AddDays(16), today.AddDays(30), today.AddDays(30).ToString("M/d")),
            (today.AddDays(31), today.AddDays(45), today.AddDays(45).ToString("M/d")),
            (today.AddDays(46), today.AddDays(60), today.AddDays(60).ToString("yy/M/d")),
            (today.AddDays(61), today.AddDays(90), today.AddDays(90).ToString("yy/M/d")),
            (today.AddDays(91), DateTime.MaxValue, "远日"),
        };
    }

    private static bool IsInBucket(DateTime date, (DateTime Start, DateTime End, string Label) bucket)
    {
        return date >= bucket.Start && date <= bucket.End;
    }

    /// <summary>
    /// 判定工序组是否属于指定工段
    /// 荒管抛光：工序名称为"荒管处理"且带有抛光工段（OuterPolish 有值）
    /// 冷轧已细分为 60冷轧/50冷轧/30冷轧/20冷轧/三辊冷轧，无需解析 OD
    /// </summary>
    private static bool ClassifySection(ProcessGroupInfo pg, string sectionName)
    {
        // 荒管抛光
        if (sectionName == "荒管抛光")
            return pg.ProcessName == "荒管处理" && pg.OuterPolish.HasValue;

        // 拉机
        if (sectionName == "拉机")
            return pg.ProcessName == "冷拔" && pg.ColdRollDraw.HasValue;

        // 以下仅适用于冷轧
        if (!pg.ProcessName.Contains("冷轧")) return false;

        return sectionName switch
        {
            "50,60轧机" => pg.ProcessName is "50冷轧" or "60冷轧",
            "20,30轧机" => pg.ProcessName is "20冷轧" or "30冷轧",
            "三辊轧机" => pg.ProcessName == "三辊冷轧",
            _ => false
        };
    }

    /// <summary>
    /// 判断批次是否尚未到达指定工序组位置
    /// </summary>
    private static bool IsNotReached(
        string? currentGroupName,
        string? currentSectionName,
        bool? currentSectionCompleted,
        List<ProcessGroupInfo> pgs,
        ProcessGroupInfo targetPg)
    {
        if (string.IsNullOrEmpty(currentGroupName))
            return true; // 批次未开始任何工序

        // 找到批次当前的工序组
        var currentPg = pgs.FirstOrDefault(x => x.ProcessName == currentGroupName);
        if (currentPg == null || currentPg.SequenceNumber < targetPg.SequenceNumber)
            return true;

        if (currentPg.SequenceNumber == targetPg.SequenceNumber)
        {
            // 同工序，但工段尚未完成
            if (currentSectionName != null
                && (currentSectionCompleted == null || currentSectionCompleted == false))
            {
                return true; // 当前正在此工段加工
            }
        }

        return false; // 已到达或已越过
    }

    /// <summary>
    /// 基于工段序号判断批次是否尚未到达目标工段（用于荒管抛光）
    /// 不依赖 CurrentSectionCompleted，而是比较当前工段序号与目标工段序号
    /// </summary>
    private static bool IsNotReachedBySection(
        string? currentGroupName,
        string? currentSectionName,
        List<ProcessGroupInfo> pgs,
        ProcessGroupInfo targetPg,
        string targetSectionName)
    {
        if (string.IsNullOrEmpty(currentGroupName))
            return true;

        var currentPg = pgs.FirstOrDefault(x => x.ProcessName == currentGroupName);
        if (currentPg == null || currentPg.SequenceNumber < targetPg.SequenceNumber)
            return true;

        if (currentPg.SequenceNumber == targetPg.SequenceNumber)
        {
            var targetSeq = targetPg.GetSectionSequence(targetSectionName);
            var currentSeq = currentSectionName != null ? targetPg.GetSectionSequence(currentSectionName) : null;

            if (targetSeq.HasValue && currentSeq.HasValue)
            {
                // 当前工段序号 < 目标工段序号 → 尚未达到
                return currentSeq.Value < targetSeq.Value;
            }
        }

        return false;
    }

    private static decimal ConvertToTons(decimal kg)
    {
        return Math.Round(kg / 1000m, 0);
    }
}
