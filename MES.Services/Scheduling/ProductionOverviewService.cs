using Microsoft.EntityFrameworkCore;
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
using MES.Core.Helpers;
using MES.Services.Helpers;

namespace MES.Services.Scheduling;

/// <summary>
/// 订单总况服务 — 聚合各工段产能负荷数据
/// </summary>
public class ProductionOverviewService : IProductionOverviewService
{
    private readonly AppDbContext _context;
    private readonly IConfigParameterService _configService;
    private readonly IDailyProductionCapacityService _dailyCapacityService;
    private readonly IFinalInspectionPlanService _finalInspectionPlanService;
    private readonly Dictionary<string, Dictionary<string, decimal>> _configMaps = new();

    public ProductionOverviewService(
        AppDbContext context,
        IConfigParameterService configService,
        IDailyProductionCapacityService dailyCapacityService,
        IFinalInspectionPlanService finalInspectionPlanService)
    {
        _context = context;
        _configService = configService;
        _dailyCapacityService = dailyCapacityService;
        _finalInspectionPlanService = finalInspectionPlanService;
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
        var bucket1 = (int)await GetConfigAsync("DateBucket", "Bucket1", 7m);
        var bucket2 = (int)await GetConfigAsync("DateBucket", "Bucket2", 15m);
        var bucket3 = (int)await GetConfigAsync("DateBucket", "Bucket3", 30m);
        var bucket4 = (int)await GetConfigAsync("DateBucket", "Bucket4", 45m);
        var bucket5 = (int)await GetConfigAsync("DateBucket", "Bucket5", 60m);
        var buckets = ProductionSummaryHelper.GenerateDateBuckets(now, bucket1, bucket2, bucket3, bucket4, bucket5);
        var rows = new List<OverviewRowDto>();

        // ========== 查询基础数据 ==========
        var summaries = await _context.Set<WorkOrderExecutionSummary>()
            .AsNoTracking()
            .Where(s => s.ScheduleStage >= 2)
            .Select(s => new
            {
                s.DeliveryDate,
                s.EstimatedProcessCompletionDate,
                s.TotalWeight,
                s.FinishPlanWeight,
                s.FinishInWeight,
                s.InputWeight,
                s.FlowOutputRatio,
                s.RawMaterialLockRemark,
                s.ScheduleStage,
                s.WorkOrderNo
            })
            .ToListAsync();

        // ScheduleStage==2（原料锁定）工单：行0 成品在购 / 行1 待投料 与原锁计划同源
        var stage2Summaries = summaries.Where(s => s.ScheduleStage == 2).ToList();

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
                b.Status,
                b.ManufacturingItem,
                b.Specification
            })
            .ToListAsync();

        var batchIds = batches.Select(b => b.Id).ToList();

        // 延期分类行（订单延期-在产/成检）专用批次：覆盖在产/未产/成检，供按工单号关联统计理论成品重量
        var delayBatches = await _context.Set<ProductionBatch>()
            .AsNoTracking()
            .Where(b => b.Status == BatchStatus.InProgress
                        || b.Status == BatchStatus.None
                        || b.Status == BatchStatus.InFinalInspection)
            .Select(b => new
            {
                b.WorkOrderNo,
                b.Status,
                b.TheoreticalOutputWeight
            })
            .ToListAsync();

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
                pg.ColdRollDraw, pg.OilPipeCut, pg.Degrease, pg.EmulsionWash, pg.UltrasonicWash, pg.ClothPolish,
                pg.BrightAnnealing, pg.Solution, pg.Straighten, pg.Cut, pg.ThicknessMeasure, pg.Pickle,
                pg.OuterPolish, pg.InnerPolish, pg.InnerGrinding, pg.OuterSpotGrinding, pg.SandBlasting,
                pg.ShotBlasting, pg.Inspection, pg.WeldingHead, pg.Welding, pg.Lubrication, pg.Packing,
                pg.Warehouse, pg.Extra1, pg.Extra2))
            .ToListAsync();

        var groupsByBatch = processGroups.GroupBy(pg => pg.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // ========== 行 1: 完善计划（原「待计划」，2026-08-19 用户决策与待投料量汇总三档严格对齐） ==========
        // 待投料口径与原锁计划「待投料」一致：
        // 成品重量 → 原料重量按配置倍率换算（TotalWeight 为成品重）
        // 成购扣减 = 成品计划量 − 已到货量（缺口口径，外购由供应商生产、本厂不投料）
        // 质量补料（A）：(总重−成购)×1.1×(1−流转比/100)——投料已满足但产出不足，按流转比缺口折算，不减已投料
        // 其他：(总重−成购)×1.1 − 已投料；逐工单 Max(0) 后再汇总（与原锁计划待投料矩阵同口径）
        // 完善计划 = 原锁计划「待投料量汇总」中 D完善计划（ImprovePlan）工单的合计待投料重量
        var rawRatio = await GetConfigAsync("ProcessingDiscount", "RawMaterialRatio", 1.1m);
        var row1Remaining = stage2Summaries.Sum(s => ProductionSummaryHelper.CalcPending(s.TotalWeight, s.FinishPlanWeight, s.FinishInWeight, s.InputWeight, s.FlowOutputRatio, s.RawMaterialLockRemark, rawRatio));
        var improvePlanSummaries = stage2Summaries
            .Where(s => RawMaterialLockRemarkKeys.ToKey(s.RawMaterialLockRemark) == RawMaterialLockRemarkKeys.ImprovePlan)
            .ToList();
        var pendingPlanRemaining = improvePlanSummaries.Sum(s => ProductionSummaryHelper.CalcPending(s.TotalWeight, s.FinishPlanWeight, s.FinishInWeight, s.InputWeight, s.FlowOutputRatio, s.RawMaterialLockRemark, rawRatio));
        var row1BucketTons = new List<decimal>();
        foreach (var bucket in buckets)
        {
            var tons = improvePlanSummaries
                .Where(s => IsInBucket(s.DeliveryDate, bucket))
                .Sum(s => ProductionSummaryHelper.CalcPending(s.TotalWeight, s.FinishPlanWeight, s.FinishInWeight, s.InputWeight, s.FlowOutputRatio, s.RawMaterialLockRemark, rawRatio));
            row1BucketTons.Add(ConvertToTons(tons));
        }

        rows.Add(new OverviewRowDto
        {
            Seq = 1,
            Category = "原料",
            Section = "完善计划",
            CategoryNo = 1,
            RowNo = 1,
            PendingPlanTons = ConvertToTons(pendingPlanRemaining),
            InProcurementTons = null,
            // 原「待产量」列（待投料量）数值已删除（2026-08-19 用户决策），该行仅「待计划量」列承载数值
            TotalRemainingTons = null,
            EstDays = null,
            EstDeadline = null,
            DateBucketTons = row1BucketTons
        });

        // ========== 行 2: 执行计划（原「在购荒管」，2026-08-19 用户决策改为执行计划待投料） ==========
        // 执行计划 = 原锁计划「待投料量汇总」中 C执行计划（ExecutePlan）工单的合计待投料重量
        var executePlanSummaries = stage2Summaries
            .Where(s => RawMaterialLockRemarkKeys.ToKey(s.RawMaterialLockRemark) == RawMaterialLockRemarkKeys.ExecutePlan)
            .ToList();
        var executePlanRemaining = executePlanSummaries.Sum(s => ProductionSummaryHelper.CalcPending(s.TotalWeight, s.FinishPlanWeight, s.FinishInWeight, s.InputWeight, s.FlowOutputRatio, s.RawMaterialLockRemark, rawRatio));
        var row2BucketTons = new List<decimal>();
        foreach (var bucket in buckets)
        {
            var tons = executePlanSummaries
                .Where(s => IsInBucket(s.DeliveryDate, bucket))
                .Sum(s => ProductionSummaryHelper.CalcPending(s.TotalWeight, s.FinishPlanWeight, s.FinishInWeight, s.InputWeight, s.FlowOutputRatio, s.RawMaterialLockRemark, rawRatio));
            row2BucketTons.Add(ConvertToTons(tons));
        }

        rows.Add(new OverviewRowDto
        {
            Seq = 2,
            Category = "原料",
            Section = "执行计划",
            CategoryNo = 1,
            RowNo = 2,
            PendingPlanTons = ConvertToTons(executePlanRemaining),
            InProcurementTons = null,
            TotalRemainingTons = null,
            EstDays = null,
            EstDeadline = null,
            DateBucketTons = row2BucketTons
        });

        // ========== 行 3: 外购成品（原「成品在购」；与原锁计划「外购成品」成购缺口同口径） ==========
        // 成购 = 成品计划量 − 已到货量（缺口口径，外购由供应商生产、本厂不投料）
        var row0BucketTons = new List<decimal>();
        foreach (var bucket in buckets)
        {
            var tons = stage2Summaries
                .Where(s => IsInBucket(s.DeliveryDate, bucket))
                .Sum(s => Math.Max(0m, s.FinishPlanWeight - s.FinishInWeight));
            row0BucketTons.Add(ConvertToTons(tons));
        }

        rows.Add(new OverviewRowDto
        {
            Seq = 3,
            Category = "原料",
            Section = "外购成品",
            CategoryNo = 1,
            RowNo = 3,
            InProcurementTons = ConvertToTons(stage2Summaries.Sum(s => Math.Max(0m, s.FinishPlanWeight - s.FinishInWeight))),
            TotalRemainingTons = null,
            EstDays = null,
            EstDeadline = null,
            DateBucketTons = row0BucketTons
        });

        // ========== 行 4: 原料汇总（待计划量=完善计划+执行计划、在购量=外购成品，日期桶三行求和） ==========
        var row4BucketTons = new List<decimal>();
        for (int i = 0; i < buckets.Count; i++)
        {
            row4BucketTons.Add(rows[0].DateBucketTons[i] + rows[1].DateBucketTons[i] + rows[2].DateBucketTons[i]);
        }

        rows.Add(new OverviewRowDto
        {
            Seq = 4,
            Category = "原料",
            Section = "汇总",
            PendingPlanTons = (rows[0].PendingPlanTons ?? 0) + (rows[1].PendingPlanTons ?? 0),
            InProcurementTons = rows[2].InProcurementTons,
            TotalRemainingTons = null,
            EstDays = null,
            EstDeadline = null,
            DateBucketTons = row4BucketTons,
            IsSummary = true
        });

        // ========== 行 5-N: 各生产工段（荒管抛光固定首行 + 冷轧/冷拔机台组动态遍历） ==========
        var capacities = await _dailyCapacityService.GetAllAsync();
        var capacityMap = capacities.ToDictionary(c => c.ProcessName, c => c.DailyCapacity, StringComparer.OrdinalIgnoreCase);
        var dailyPolish = capacityMap.GetValueOrDefault(ProductionOverviewRowKeys.Polish, 0m);
        // 行名显示：荒管抛光行走 DictValueDefinitions 配置表（DictKey=ProductionOverviewRowKey）优先，未配置回退规范中文；
        // 机台组行显示名直接取组 DisplayName（2026-08-30 用户决策：组显示名联动）；
        // "[累]" 为投料-在产行固定口径前缀（与行名本体分离）。
        string RowDisplay(string key) => "[累]" + (DictValueDisplayHelper.GetText(DictValueDefaults.ProductionOverviewRowKey, key) ?? key);

        // 完全遍历机台组配置（含全部组）：行 Key=组 GroupKey、显示名=组 DisplayName、行序=DisplayOrder、
        // 日产能档案键=组 Key（DailyProductionCapacities.ProcessName 存组 Key，2026-08-30 用户决策）。
        var machineGroups = await _context.ColdRollMachineGroupConfigs
            .AsNoTracking()
            .OrderBy(g => g.DisplayOrder)
            .ThenBy(g => g.Id)
            .ToListAsync();

        var sections = new List<(int Seq, int RowNo, string Key, string Section, decimal DailyCapacity, string[] ProcessKeys)>
        {
            // 荒管抛光固定首行（不在机台组体系）
            (Seq: 5, RowNo: 1, Key: ProductionOverviewRowKeys.Polish,
             Section: RowDisplay(ProductionOverviewRowKeys.Polish), DailyCapacity: dailyPolish,
             ProcessKeys: Array.Empty<string>()),
        };
        var prodSeq = 6;
        var prodRowNo = 2;
        foreach (var g in machineGroups)
        {
            var groupKeys = SplitProcessKeys(g.ProcessKeys);
            var groupCapacity = capacityMap.GetValueOrDefault(g.GroupKey, 0m);
            sections.Add((prodSeq++, prodRowNo++, g.GroupKey, "[累]" + g.DisplayName, groupCapacity, groupKeys));
        }
        // 生产工段行之后的汇总/成检/整体完工/延期行 Seq 起点（动态跟随机台组数量）
        var nextSeq = 5 + sections.Count;

        int maxProdEstDays = 0;

        foreach (var (seq, rowNo, sectionKey, sectionName, dailyCapacity, groupProcessKeys) in sections)
        {
            decimal totalPending = 0;
            decimal inProgressPending = 0;
            decimal finishedPending = 0;
            var matchedBatchData = new List<(DateTime DeliveryDate, decimal Weight)>();

            // 荒管抛光行按原始口径不拆分产类（无冷轧/冷拔产类逻辑）
            bool splitByProductStatus = sectionKey != ProductionOverviewRowKeys.Polish;

            foreach (var batch in batches)
            {
                if (batch.CurrentValidWeight == null || batch.CurrentValidWeight <= 0) continue;
                if (!groupsByBatch.TryGetValue(batch.Id, out var pgs)) continue;

                // 检查此批次的某个工序组是否属于当前工段且尚未到达
                for (int i = 0; i < pgs.Count; i++)
                {
                    var pg = pgs[i];

                    if (!ClassifySection(pg, sectionKey, groupProcessKeys)) continue;

                    // 判断是否尚未到达此工段
                    // 荒管抛光使用工段级比较（不依赖 CurrentSectionCompleted）
                    bool isNotReached = sectionKey == ProductionOverviewRowKeys.Polish
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
                        if (splitByProductStatus)
                        {
                            // 参考生产记录「产类」逻辑：待执行工序组的制造规格 == 批次成品规格 且 成品类物品 → 成品，否则在制
                            var productStatus = ClassifyPendingProductStatus(
                                pg, batch.ManufacturingItem, batch.Specification);
                            if (productStatus == ProductStatuses.Finished)
                                finishedPending += weight;
                            else
                                inProgressPending += weight;
                        }
                        matchedBatchData.Add((batch.DeliveryDate, weight));
                        // ⚠️ 不 break：同一批次可含多个匹配本工段行的工序组（如冷轧50+冷轧60 两道次），
                        // 每道未到达的工序组各计一次构成「合重量」（5060 行 = 50 机待产 + 60 机待产）
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
                Category = "投料-在产",
                Section = sectionName,
                CategoryNo = 2,
                RowNo = rowNo,
                InProcurementTons = null,
                TotalRemainingTons = ConvertToTons(totalPending),
                PendingInProgressTons = splitByProductStatus ? ConvertToTons(inProgressPending) : null,
                PendingFinishedTons = splitByProductStatus ? ConvertToTons(finishedPending) : null,
                EstDays = estDays > 0 ? estDays : null,
                EstDeadline = estDays > 0 ? now.AddDays(estDays) : null,
                DateBucketTons = rowBucketTons
            });
        }

        // ========== 行 10: 生产汇总 ==========
        // ⚠️ 按批次去重统计（区别于工段行的按节点匹配统计）：未产+在产批次有效重量各计一次，
        // 与工段行之和口径不同（同一批次可跨多工段重复计入），故汇总行单独计算，前端以「(现周转)」后缀标注口径
        var productionSummaryWeight = batches
            .Where(b => b.CurrentValidWeight.HasValue && b.CurrentValidWeight.Value > 0)
            .Sum(b => b.CurrentValidWeight!.Value);
        var prodBucketTons = buckets.Select(bucket =>
            ConvertToTons(batches
                .Where(b => b.CurrentValidWeight.HasValue && b.CurrentValidWeight.Value > 0
                            && IsInBucket(b.DeliveryDate, bucket))
                .Sum(b => b.CurrentValidWeight!.Value)))
            .ToList();

        rows.Add(new OverviewRowDto
        {
            Seq = nextSeq,
            Category = "投料-在产",
            Section = "汇总",
            InProcurementTons = null,
            TotalRemainingTons = ConvertToTons(productionSummaryWeight),
            EstDays = null,
            EstDeadline = null,
            DateBucketTons = prodBucketTons,
            IsSummary = true
        });

        // ========== 行 11: 成品检验（与成检计划看板口径对齐） ==========
        // 复用成检计划看板：候选=批次状态 InFinalInspection；档位=待到料/待检验/检验中/完成检验待入库。
        // 本行汇总「待检验+检验中」两档的生产重量（非定尺=批次理论成品重量；定尺=单支重×生产支数），
        // 预/正式合并、按批次去重——与成检计划 GetSummaryAsync.SummarizePending 完全同口径。
        var kanban = await _finalInspectionPlanService.GetKanbanAsync();
        var fiPendingWeight = kanban
            .Where(x => x.KanbanStage is KanbanStageKeys.WaitingInspection or KanbanStageKeys.Inspecting)
            .GroupBy(x => x.ProductionBatchId)
            .Select(g => g.First())
            .Sum(x => x.ProductionWeight ?? 0m);

        var row8BucketTons = buckets.Select(_ => 0m).ToList();
        rows.Add(new OverviewRowDto
        {
            Seq = nextSeq + 1,
            Category = "投料-成检",
            Section = "",
            CategoryNo = 3,
            RowNo = 1,
            InProcurementTons = null,
            TotalRemainingTons = ConvertToTons(fiPendingWeight),
            EstDays = null,
            EstDeadline = null,
            DateBucketTons = row8BucketTons
        });

        // ========== 行 12: 成检汇总（成检仅 1 行，汇总=自身） ==========
        rows.Add(new OverviewRowDto
        {
            Seq = nextSeq + 2,
            Category = "投料-成检",
            Section = "汇总",
            InProcurementTons = null,
            TotalRemainingTons = ConvertToTons(fiPendingWeight),
            EstDays = null,
            EstDeadline = null,
            DateBucketTons = row8BucketTons.ToList(),
            IsSummary = true
        });

        // ========== 行 13: 整体完工预计 ==========
        // 预计天数 = Max(生产工段天数) + 原料待产量(吨) / 20,30轧机日产(吨/天) + 成品检验 2 天（2026-08-19 由「总估算」改名，明确表达全部负荷消化完的总体预估；成检环节硬编码 2 天补齐）
        var rawPendingTons = row1Remaining > 0
            ? row1Remaining / 1000m
            : 0m;
        // 原料待产量 ÷ 2030 机台组日产能（2026-08-30 起产能档案键=机台组 GroupKey；无运行时兜底，未配置→0）
        var daily2030 = capacityMap.GetValueOrDefault("2030", 0m);
        var extraDays = daily2030 > 0
            ? (int)Math.Ceiling(rawPendingTons / daily2030)
            : 0;
        var totalEstDays = maxProdEstDays + extraDays + 2;

        rows.Add(new OverviewRowDto
        {
            Seq = nextSeq + 3,
            Category = "整体完工预计",
            Section = "",
            InProcurementTons = null,
            TotalRemainingTons = null,
            EstDays = totalEstDays > 0 ? totalEstDays : null,
            EstDeadline = totalEstDays > 0 ? now.AddDays(totalEstDays) : null,
            DateBucketTons = buckets.Select(_ => 0m).ToList()
        });

        // ========== 行 15: 订单延期-原料 ==========
        // 主值：延期量条件 + 主号关注=原料锁定(2) 的工单成品重量（TotalWeight）；
        // 副值（料）：同批工单的投料缺少量（复用 CalcPending 待投料口径，原料重转吨）。
        var stage2Delays = summaries.Where(s => s.ScheduleStage == 2).ToList();
        var rawMainBucketTons = buckets
            .Select(bucket => ConvertToTons(stage2Delays
                .Where(s => IsInBucket(s.DeliveryDate, bucket)
                            && s.EstimatedProcessCompletionDate.HasValue
                            && s.EstimatedProcessCompletionDate.Value > s.DeliveryDate)
                .Sum(s => s.TotalWeight)))
            .ToList();
        var rawSubBucketTons = buckets
            .Select(bucket => ConvertToTons(stage2Delays
                .Where(s => IsInBucket(s.DeliveryDate, bucket)
                            && s.EstimatedProcessCompletionDate.HasValue
                            && s.EstimatedProcessCompletionDate.Value > s.DeliveryDate)
                .Sum(s => ProductionSummaryHelper.CalcPending(s.TotalWeight, s.FinishPlanWeight, s.FinishInWeight, s.InputWeight,
                    s.FlowOutputRatio, s.RawMaterialLockRemark, rawRatio))))
            .ToList();

        rows.Add(new OverviewRowDto
        {
            Seq = nextSeq + 4,
            Category = "订单交期负荷",
            Section = "订单延期-原料",
            InProcurementTons = null,
            TotalRemainingTons = null,
            EstDays = null,
            EstDeadline = null,
            DateBucketTons = rawMainBucketTons,
            DateBucketSubTons = rawSubBucketTons.Select(x => (decimal?)x).ToList(),
            SubValuePrefix = "待料",
            DateBucketSubOnly = true
        });

        // ========== 行 16: 订单延期-在产 ==========
        // 主值：延期量条件 + 主号关注=生产执行(3) 的工单成品重量；
        // 副值（在产）：同批工单（按工单号匹配）关联批次中状态=在产/未产 的理论成品重量和。
        var stage3Delays = summaries.Where(s => s.ScheduleStage == 3).ToList();
        var prodMainBucketTons = buckets
            .Select(bucket => ConvertToTons(stage3Delays
                .Where(s => IsInBucket(s.DeliveryDate, bucket)
                            && s.EstimatedProcessCompletionDate.HasValue
                            && s.EstimatedProcessCompletionDate.Value > s.DeliveryDate)
                .Sum(s => s.TotalWeight)))
            .ToList();
        var prodSubBucketTons = buckets
            .Select(bucket =>
            {
                var woNos = stage3Delays
                    .Where(s => IsInBucket(s.DeliveryDate, bucket)
                                && s.EstimatedProcessCompletionDate.HasValue
                                && s.EstimatedProcessCompletionDate.Value > s.DeliveryDate)
                    .Select(s => s.WorkOrderNo)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var weight = delayBatches
                    .Where(b => woNos.Contains(b.WorkOrderNo)
                                && (b.Status == BatchStatus.InProgress || b.Status == BatchStatus.None))
                    .Sum(b => b.TheoreticalOutputWeight ?? 0);
                return ConvertToTons(weight);
            })
            .ToList();

        rows.Add(new OverviewRowDto
        {
            Seq = nextSeq + 5,
            Category = "订单交期负荷",
            Section = "订单延期-在产",
            InProcurementTons = null,
            TotalRemainingTons = null,
            EstDays = null,
            EstDeadline = null,
            DateBucketTons = prodMainBucketTons,
            DateBucketSubTons = prodSubBucketTons.Select(x => (decimal?)x).ToList(),
            SubValuePrefix = "在产",
            DateBucketSubOnly = true
        });

        // ========== 行 17: 订单延期-成检 ==========
        // 主值：延期量条件 + 主号关注=成品检验(4) 的工单成品重量；
        // 副值（在检）：同批工单（按工单号匹配）关联批次中状态=成检 的理论成品重量和。
        var stage4Delays = summaries.Where(s => s.ScheduleStage == 4).ToList();
        var fiMainBucketTons = buckets
            .Select(bucket => ConvertToTons(stage4Delays
                .Where(s => IsInBucket(s.DeliveryDate, bucket)
                            && s.EstimatedProcessCompletionDate.HasValue
                            && s.EstimatedProcessCompletionDate.Value > s.DeliveryDate)
                .Sum(s => s.TotalWeight)))
            .ToList();
        var fiSubBucketTons = buckets
            .Select(bucket =>
            {
                var woNos = stage4Delays
                    .Where(s => IsInBucket(s.DeliveryDate, bucket)
                                && s.EstimatedProcessCompletionDate.HasValue
                                && s.EstimatedProcessCompletionDate.Value > s.DeliveryDate)
                    .Select(s => s.WorkOrderNo)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var weight = delayBatches
                    .Where(b => woNos.Contains(b.WorkOrderNo) && b.Status == BatchStatus.InFinalInspection)
                    .Sum(b => b.TheoreticalOutputWeight ?? 0);
                return ConvertToTons(weight);
            })
            .ToList();

        rows.Add(new OverviewRowDto
        {
            Seq = nextSeq + 6,
            Category = "订单交期负荷",
            Section = "订单延期-成检",
            InProcurementTons = null,
            TotalRemainingTons = null,
            EstDays = null,
            EstDeadline = null,
            DateBucketTons = fiMainBucketTons,
            DateBucketSubTons = fiSubBucketTons.Select(x => (decimal?)x).ToList(),
            SubValuePrefix = "在检",
            DateBucketSubOnly = true
        });

        // ========== 行序重排（2026-08-23 用户决策）：订单交期负荷 3 行置顶（延期-原料/在产/成检），原料→生产→成检随后，整体完工预计最后 ==========
        // 生产工段行数随机台组配置动态变化（2026-08-30 起完全遍历机台组），故用分类排序替代固定索引数组。
        // （2026-08-23 删除订单延期量/订单延期量[预计完结]/订单非延期 3 行；前 3 行日期桶格仅显示副值）
        rows = rows
            .OrderBy(r => r.Category == "订单交期负荷" ? 0 : 1)
            .ThenBy(r => r.Category == "整体完工预计" ? 1 : 0)
            .ThenBy(r => r.Seq)
            .ToList();
        for (int i = 0; i < rows.Count; i++) rows[i].Seq = i + 1;

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
        int? ColdRollDraw, int? OilPipeCut, int? Degrease, int? EmulsionWash, int? UltrasonicWash, int? ClothPolish,
        int? BrightAnnealing, int? Solution, int? Straighten, int? Cut, int? ThicknessMeasure, int? Pickle,
        int? OuterPolish, int? InnerPolish, int? InnerGrinding, int? OuterSpotGrinding, int? SandBlasting,
        int? ShotBlasting, int? Inspection, int? WeldingHead, int? Welding, int? Lubrication, int? Packing,
        int? Warehouse, int? Extra1, int? Extra2)
    {
        /// <summary>获取指定工段名称在此工序组中的执行序号，null 表示此工序组不含该工段</summary>
        public int? GetSectionSequence(string? sectionName)
        {
            var key = SectionKeys.ToKey(sectionName);
            if (key == null) return null;
            return key switch
            {
                SectionKeys.ColdRollDraw => ColdRollDraw,
                SectionKeys.OilPipeCut => OilPipeCut,
                SectionKeys.Degrease => Degrease,
                SectionKeys.EmulsionWash => EmulsionWash,
                SectionKeys.UltrasonicWash => UltrasonicWash,
                SectionKeys.ClothPolish => ClothPolish,
                SectionKeys.BrightAnnealing => BrightAnnealing,
                SectionKeys.Solution => Solution,
                SectionKeys.Straighten => Straighten,
                SectionKeys.Cut => Cut,
                SectionKeys.ThicknessMeasure => ThicknessMeasure,
                SectionKeys.Pickle => Pickle,
                SectionKeys.OuterPolish => OuterPolish,
                SectionKeys.InnerPolish => InnerPolish,
                SectionKeys.InnerGrinding => InnerGrinding,
                SectionKeys.OuterSpotGrinding => OuterSpotGrinding,
                SectionKeys.SandBlasting => SandBlasting,
                SectionKeys.ShotBlasting => ShotBlasting,
                SectionKeys.Inspection => Inspection,
                SectionKeys.WeldingHead => WeldingHead,
                SectionKeys.Welding => Welding,
                SectionKeys.Lubrication => Lubrication,
                SectionKeys.Packing => Packing,
                SectionKeys.Warehouse => Warehouse,
                SectionKeys.Extra1 => Extra1,
                SectionKeys.Extra2 => Extra2,
                _ => null
            };
        }
    }

    private static bool IsInBucket(DateTime date, (DateTime Start, DateTime End, string Label) bucket)
    {
        return date >= bucket.Start && date <= bucket.End;
    }

    /// <summary>
    /// 解析机台组 ProcessKeys 逗号串为 Key 数组（Trim + 去空）。
    /// </summary>
    private static string[] SplitProcessKeys(string? processKeys)
    {
        if (string.IsNullOrWhiteSpace(processKeys)) return Array.Empty<string>();
        return processKeys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// 判定工序组是否属于指定生产总览行。
    /// 荒管抛光：工序名称为"荒管处理"且带有抛光工段（OuterPolish 有值）；
    /// 冷轧/冷拔机台组：组内工序 ProcessKeys 包含该工序（配置表驱动，2026-08-30 起无硬编码工序 Key，
    /// 工序全局唯一归属一组，服务层已校验跨组不重叠）。
    /// </summary>
    private static bool ClassifySection(ProcessGroupInfo pg, string sectionKey, string[] groupProcessKeys)
    {
        // 荒管抛光
        if (sectionKey == ProductionOverviewRowKeys.Polish)
            return pg.ProcessName == ProcessKeys.RoughTubeProcessing && pg.OuterPolish.HasValue;

        // 机台组：按组内工序集合匹配（含冷轧与冷拔）
        var key = ProcessKeys.ToKey(pg.ProcessName) ?? pg.ProcessName;
        return groupProcessKeys.Length > 0 && groupProcessKeys.Contains(key, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判定待执行工序组将产出的产类（在制/成品），参考生产记录「产类」逻辑。
    /// 对冷轧/冷拔工序：成品判定 = 该工序组制造规格 == 批次成品规格 且 批次制造物品属成品类（OrderFinished/Finished/CriticalFinished/SpecialDeliveryStatus），
    /// 与 <see cref="ProductStatusHelper.Calculate"/> 对冷轧/冷拔工序完全等价（荒管分支在冷轧/冷拔工序不触发）。
    /// </summary>
    private static string ClassifyPendingProductStatus(ProcessGroupInfo pg, string? manufacturingItem, string? finishedSpec)
    {
        if (finishedSpec != null
            && string.Equals(pg.ManufacturingSpec, finishedSpec, StringComparison.OrdinalIgnoreCase)
            && ProductStatusHelper.IsFinishedManufacturingItem(manufacturingItem))
            return ProductStatuses.Finished;
        return ProductStatuses.InProgress;
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
            // 冷轧/冷拔类工序组以「冷轧拔」为机台加工点（冷轧拔是本组第一道工段）。
            // 当前工段已越过冷轧拔（如脱脂/酸洗/检验等后工段）→ 该机台已轧完、不再占用机台产能，不应计入待产。
            // 2026-08-20 修复：实证 12 批次 ColdRoll50 组内冷轧拔(序号6)已完成、正处脱脂(序号8)未完工，原逻辑误计 5060 行待产。
            if (targetPg.ColdRollDraw.HasValue && currentSectionName != null)
            {
                var currentSeq = targetPg.GetSectionSequence(currentSectionName);
                if (currentSeq.HasValue && currentSeq.Value > targetPg.ColdRollDraw.Value)
                    return false;
            }

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
