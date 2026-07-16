using Microsoft.EntityFrameworkCore;
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
using MES.Core.Constants;
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

namespace MES.Services.Scheduling;

/// <summary>
/// 订单总况服务 — 聚合各工段产能负荷数据
/// </summary>
public class ProductionOverviewService : IProductionOverviewService
{
    private readonly AppDbContext _context;
    private readonly IConfigParameterService _configService;
    private readonly IDailyProductionCapacityService _dailyCapacityService;
    private readonly Dictionary<string, Dictionary<string, decimal>> _configMaps = new();

    public ProductionOverviewService(
        AppDbContext context,
        IConfigParameterService configService,
        IDailyProductionCapacityService dailyCapacityService)
    {
        _context = context;
        _configService = configService;
        _dailyCapacityService = dailyCapacityService;
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

    public async Task<ProductionOverviewDto> GetOverviewAsync()
    {
        var now = DateTime.Today;
        var bucket1 = (int)await GetConfigAsync("DateBucket", "Bucket1", 15m);
        var bucket2 = (int)await GetConfigAsync("DateBucket", "Bucket2", 30m);
        var bucket3 = (int)await GetConfigAsync("DateBucket", "Bucket3", 45m);
        var bucket4 = (int)await GetConfigAsync("DateBucket", "Bucket4", 60m);
        var bucket5 = (int)await GetConfigAsync("DateBucket", "Bucket5", 90m);
        var buckets = GenerateDateBuckets(now, bucket1, bucket2, bucket3, bucket4, bucket5);
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
        // 成品重量 → 原料重量按配置倍率换算（TotalWeight/PendingOutsourceFinishWeight 为成品重）
        var rawRatio = await GetConfigAsync("ProcessingDiscount", "RawMaterialRatio", 1.1m);
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
        var capacities = await _dailyCapacityService.GetAllAsync();
        var capacityMap = capacities.ToDictionary(c => c.ProcessName, c => c.DailyCapacity);
        var dailyPolish = capacityMap.GetValueOrDefault("荒管抛光", 12m);
        var dailyMill50_60 = capacityMap.GetValueOrDefault("50,60轧机", 11m);
        var dailyMill20_30 = capacityMap.GetValueOrDefault("20,30轧机", 9m);
        var dailyThreeRoll = capacityMap.GetValueOrDefault("三辊轧机", 0.5m);
        var dailyDrawBench = capacityMap.GetValueOrDefault("拉机", 3m);
        var sections = new[]
        {
            (Seq: 3, Section: "荒管抛光", DailyCapacity: dailyPolish),
            (Seq: 4, Section: "50,60轧机", DailyCapacity: dailyMill50_60),
            (Seq: 5, Section: "20,30轧机", DailyCapacity: dailyMill20_30),
            (Seq: 6, Section: "三辊轧机", DailyCapacity: dailyThreeRoll),
            (Seq: 7, Section: "拉机", DailyCapacity: dailyDrawBench),
        };

        int maxProdEstDays = 0;

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
                            pgs, pg, SectionDefs.OuterPolish)
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

            if (estDays > maxProdEstDays) maxProdEstDays = estDays;

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

        // ========== 行 8: 成品检验（成检计划中 待检验+检验中 的重量汇总） ==========
        var rcBatchIds = await _context.MaterialReceiveChecks
            .AsNoTracking()
            .Where(rc => !rc.IsForceCompleted)
            .Select(rc => rc.ProductionBatchId)
            .ToListAsync();
        var receivedIds = rcBatchIds.ToHashSet();

        var inspectedBatchIds = await _context.FinalInspections
            .AsNoTracking()
            .Select(fi => fi.ProductionBatchId)
            .Distinct()
            .ToListAsync();
        var inspectedIds = inspectedBatchIds.ToHashSet();

        var warehouseBatchNos = await _context.InventoryBatches
            .AsNoTracking()
            .Where(ib => ib.ProductionBatchNo != null)
            .Select(ib => ib.ProductionBatchNo!)
            .Distinct()
            .ToListAsync();
        var warehousedNos = warehouseBatchNos.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fiBatches = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => receivedIds.Contains(b.Id))
            .Select(b => new { b.Id, b.CurrentValidWeight, b.BatchNo })
            .ToListAsync();

        decimal fiPendingWeight = 0;
        foreach (var b in fiBatches)
        {
            var isInspected = inspectedIds.Contains(b.Id);
            var isWarehoused = b.BatchNo != null && warehousedNos.Contains(b.BatchNo);
            // 待检验：未检验；检验中：已检验但未入库
            if (!isInspected || (isInspected && !isWarehoused))
            {
                fiPendingWeight += (b.CurrentValidWeight ?? 0);
            }
        }

        var row8BucketTons = buckets.Select(_ => 0m).ToList();
        rows.Add(new OverviewRowDto
        {
            Seq = 8,
            Category = "成品检验",
            Section = "",
            InProcurementTons = null,
            TotalRemainingTons = ConvertToTons(fiPendingWeight),
            EstDays = null,
            EstDeadline = null,
            DateBucketTons = row8BucketTons
        });

        // ========== 行 9: 总估算 ==========
        // 预计天数 = Max(生产工段天数) + 原料待产量(吨) / 20,30轧机日产(吨/天)
        var rawPendingTons = row1Remaining > 0
            ? row1Remaining / 1000m
            : 0m;
        var extraDays = dailyMill20_30 > 0
            ? (int)Math.Ceiling(rawPendingTons / dailyMill20_30)
            : 0;
        var totalEstDays = maxProdEstDays + extraDays;

        rows.Add(new OverviewRowDto
        {
            Seq = 9,
            Category = "总估算",
            Section = "",
            InProcurementTons = null,
            TotalRemainingTons = null,
            EstDays = totalEstDays > 0 ? totalEstDays : null,
            EstDeadline = totalEstDays > 0 ? now.AddDays(totalEstDays) : null,
            DateBucketTons = buckets.Select(_ => 0m).ToList()
        });

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
            SectionDefs.ColdRollDraw => ColdRollDraw,
            SectionDefs.OilPipeCut => OilPipeCut,
            SectionDefs.Degrease => Degrease,
            SectionDefs.Solution => Solution,
            SectionDefs.Straighten => Straighten,
            SectionDefs.Cut => Cut,
            SectionDefs.ThicknessMeasure => ThicknessMeasure,
            SectionDefs.Pickle => Pickle,
            SectionDefs.OuterPolish => OuterPolish,
            SectionDefs.InnerGrinding => InnerGrinding,
            SectionDefs.OuterSpotGrinding => OuterSpotGrinding,
            SectionDefs.Inspection => Inspection,
            SectionDefs.WeldingHead => WeldingHead,
            SectionDefs.Lubrication => Lubrication,
            SectionDefs.Warehouse => Warehouse,
            _ => null
        };
    }

    private static List<(DateTime Start, DateTime End, string Label)> GenerateDateBuckets(DateTime today, int bucket1 = 15, int bucket2 = 30, int bucket3 = 45, int bucket4 = 60, int bucket5 = 90)
    {
        return new List<(DateTime, DateTime, string)>
        {
            (DateTime.MinValue, today, today.ToString("M/d")),
            (today.AddDays(1), today.AddDays(bucket1), today.AddDays(bucket1).ToString("M/d")),
            (today.AddDays(bucket1 + 1), today.AddDays(bucket2), today.AddDays(bucket2).ToString("M/d")),
            (today.AddDays(bucket2 + 1), today.AddDays(bucket3), today.AddDays(bucket3).ToString("M/d")),
            (today.AddDays(bucket3 + 1), today.AddDays(bucket4), today.AddDays(bucket4).ToString("yy/M/d")),
            (today.AddDays(bucket4 + 1), today.AddDays(bucket5), today.AddDays(bucket5).ToString("yy/M/d")),
            (today.AddDays(bucket5 + 1), DateTime.MaxValue, "远日"),
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
            return pg.ProcessName == ProcessNames.RoughTubeProcessing && pg.OuterPolish.HasValue;

        // 拉机
        if (sectionName == "拉机")
            return pg.ProcessName == ProcessNames.ColdDraw && pg.ColdRollDraw.HasValue;

        // 以下仅适用于冷轧
        if (!ProcessNames.IsColdRoll(pg.ProcessName)) return false;

        return sectionName switch
        {
            "50,60轧机" => pg.ProcessName is ProcessNames.ColdRoll50 or ProcessNames.ColdRoll60,
            "20,30轧机" => pg.ProcessName is ProcessNames.ColdRoll20 or ProcessNames.ColdRoll30,
            "三辊轧机" => pg.ProcessName == ProcessNames.ThreeRollColdRoll,
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
