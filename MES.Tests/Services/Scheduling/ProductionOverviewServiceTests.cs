using FluentAssertions;
using MES.Core.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Scheduling;
using MES.Core.Enums;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.Scheduling;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.WorkOrder;
using MES.Services.Scheduling;
using MES.Tests.Tests;
using Moq;

namespace MES.Tests.Services.Scheduling;

/// <summary>
/// 订单负荷总量（原「负载总览」）测试：行重排（完善计划/执行计划/外购成品/原料汇总/生产工段/生产汇总/成检/成检汇总/整体完工预计）、
/// 完善计划与执行计划 = 原锁计划「待投料量汇总」中 D完善计划（ImprovePlan）/ C执行计划（ExecutePlan）行的合计待投料重量、
/// 外购成品 = 成购缺口、待产量列删除、
/// 类别汇总行（原料/生产/成检小计）：生产汇总按批次去重（未产+在产），区别于工段行按节点匹配的重复统计。
/// </summary>
public class ProductionOverviewServiceTests : TestBase
{
    private ProductionOverviewService CreateService(AppDbContext ctx, List<FinalInspectionPlanDto>? kanban = null)
    {
        // 冷轧/冷拔生产工段行由机台组配置表动态驱动（2026-08-30 起），内存库必须预置组种子；
        // 4 组（5060/2030/三辊/拉机，DisplayOrder 1-4）行序与既有断言索引保持一致（Rows[7]=Polish、[8]=5060、[9]=2030、[10]=三辊、[11]=拉机）。
        SeedMachineGroupConfigs(ctx);
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        var capacityMock = new Mock<IDailyProductionCapacityService>();
        capacityMock.Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<DailyProductionCapacityDto>());
        var fiMock = new Mock<IFinalInspectionPlanService>();
        fiMock.Setup(x => x.GetKanbanAsync())
            .ReturnsAsync(kanban ?? new List<FinalInspectionPlanDto>());
        return new ProductionOverviewService(ctx, configMock.Object, capacityMock.Object, fiMock.Object);
    }

    private static void SeedMachineGroupConfigs(AppDbContext ctx)
    {
        if (ctx.ColdRollMachineGroupConfigs.Any()) return;
        ctx.ColdRollMachineGroupConfigs.AddRange(
            new ColdRollMachineGroupConfig { GroupKey = "5060", DisplayName = "5060组", ProcessKeys = "ColdRoll50,ColdRoll60", DisplayOrder = 1 },
            new ColdRollMachineGroupConfig { GroupKey = "2030", DisplayName = "2030组", ProcessKeys = "ColdRoll20,ColdRoll30", DisplayOrder = 2 },
            new ColdRollMachineGroupConfig { GroupKey = "ThreeRoll", DisplayName = "三辊组", ProcessKeys = "ThreeRollColdRoll", DisplayOrder = 3 },
            new ColdRollMachineGroupConfig { GroupKey = "Draw", DisplayName = "拉机组", ProcessKeys = "ColdDraw", DisplayOrder = 4 });
        ctx.SaveChanges();
    }

    private static WorkOrderExecutionSummary SeedSummary(AppDbContext ctx, string workOrderNo,
        int scheduleStage, decimal totalWeight = 0m,
        decimal finishPlanWeight = 0m, decimal finishInWeight = 0m,
        decimal inputWeight = 0m, decimal flowOutputRatio = 0m,
        string? rawMaterialLockRemark = null,
        DateTime? deliveryDate = null, DateTime? estimatedProcessCompletionDate = null)
    {
        var s = new WorkOrderExecutionSummary
        {
            WorkOrderNo = workOrderNo,
            Salesman = "业务员",
            CustomerName = "客户",
            SettlementMethod = "Theoretical",
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            MaterialName = "无缝管",
            DeliveryState = "SolutionAnnealedAndPickled",
            PlantGrade = "304",
            Specification = "219*8",
            LengthStatus = "Fixed",
            DeliveryDate = deliveryDate ?? DateTime.Today,
            EstimatedProcessCompletionDate = estimatedProcessCompletionDate,
            ScheduleStage = scheduleStage,
            TotalWeight = totalWeight,
            FinishPlanWeight = finishPlanWeight,
            FinishInWeight = finishInWeight,
            InputWeight = inputWeight,
            FlowOutputRatio = flowOutputRatio,
            RawMaterialLockRemark = rawMaterialLockRemark,
        };
        ctx.Set<WorkOrderExecutionSummary>().Add(s);
        return s;
    }

    private static ProductionBatch SeedBatch(AppDbContext ctx, string batchNo,
        BatchStatus status, int currentValidWeight, DateTime? deliveryDate = null,
        string? workOrderNo = null, int? theoreticalOutputWeight = null,
        string? manufacturingItem = null, string? specification = null)
    {
        var batch = new ProductionBatch
        {
            BatchNo = batchNo,
            MaterialName = "不锈钢管",
            PlantGrade = "304",
            Specification = specification ?? "219*8",
            Status = status,
            ProductionType = "Internal",
            ManufacturingItem = manufacturingItem ?? "OrderFinished",
            WorkOrderNo = workOrderNo ?? "WO-B1",
            SalesOrderNo = "SO-B1",
            ProductionMainNo = "M-B1",
            OrderItemIds = "1",
            Salesman = "张三",
            SettlementMethod = "Weighing",
            StandardCode = "GB/T 14976",
            DeliveryState = "Hard",
            LengthStatus = "NonFixed",
            TechnicalRequirements = "无",
            SignDate = DateTime.Today,
            DeliveryDate = deliveryDate ?? DateTime.Today.AddMonths(1),
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.3m,
            WallThicknessPositive = 0.3m,
            TotalQuantity = 100,
            TotalMeters = 1000m,
            TotalWeight = 5000m,
            TotalItemCount = 1,
            CurrentValidWeight = currentValidWeight,
            TheoreticalOutputWeight = theoreticalOutputWeight
        };
        ctx.ProductionBatches.Add(batch);
        return batch;
    }

    private static void SeedProcessGroup(AppDbContext ctx, ProductionBatch batch, int seq, string processName, int? outerPolish = null, string? manufacturingSpec = null, int? coldRollDraw = null)
    {
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            SequenceNumber = seq,
            ProcessName = processName,
            OuterPolish = outerPolish,
            ManufacturingSpec = manufacturingSpec,
            ColdRollDraw = coldRollDraw
        });
    }

    [Fact]
    public async Task GetOverviewAsync_行重排_延期分类前3行_原料生产成检随后_整体完工预计最后()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO-D1", 2, totalWeight: 10000m, finishPlanWeight: 2000m, finishInWeight: 0m,
            inputWeight: 1000m, rawMaterialLockRemark: RawMaterialLockRemarkKeys.ImprovePlan);
        SeedSummary(ctx, "WO-D2", 2, totalWeight: 5000m, finishPlanWeight: 0m, finishInWeight: 0m,
            inputWeight: 500m, rawMaterialLockRemark: RawMaterialLockRemarkKeys.ImprovePlan);
        SeedSummary(ctx, "WO-A", 2, totalWeight: 8000m, finishPlanWeight: 0m, finishInWeight: 0m,
            inputWeight: 0m, flowOutputRatio: 50m, rawMaterialLockRemark: RawMaterialLockRemarkKeys.QualityReplenish);
        SeedSummary(ctx, "WO-X", 3, totalWeight: 2000m);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        result.Rows.Should().HaveCount(16);
        // 行 1-3: 订单延期-原料/在产/成检（订单交期负荷，序号留空，日期桶格仅显示副值）
        result.Rows[0].Seq.Should().Be(1);
        result.Rows[0].Category.Should().Be("订单交期负荷");
        result.Rows[0].Section.Should().Be("订单延期-原料");
        result.Rows[0].SubValuePrefix.Should().Be("待料");
        result.Rows[0].DateBucketSubOnly.Should().BeTrue();
        result.Rows[1].Seq.Should().Be(2);
        result.Rows[1].Category.Should().Be("订单交期负荷");
        result.Rows[1].Section.Should().Be("订单延期-在产");
        result.Rows[1].SubValuePrefix.Should().Be("在产");
        result.Rows[1].DateBucketSubOnly.Should().BeTrue();
        result.Rows[2].Seq.Should().Be(3);
        result.Rows[2].Category.Should().Be("订单交期负荷");
        result.Rows[2].Section.Should().Be("订单延期-成检");
        result.Rows[2].SubValuePrefix.Should().Be("在检");
        result.Rows[2].DateBucketSubOnly.Should().BeTrue();
        // 行 4: 完善计划（序号 1-1）
        result.Rows[3].Seq.Should().Be(4);
        result.Rows[3].Category.Should().Be("原料");
        result.Rows[3].Section.Should().Be("完善计划");
        result.Rows[3].CategoryNo.Should().Be(1);
        result.Rows[3].RowNo.Should().Be(1);
        // 行 5: 执行计划（序号 1-2）
        result.Rows[4].Seq.Should().Be(5);
        result.Rows[4].Category.Should().Be("原料");
        result.Rows[4].Section.Should().Be("执行计划");
        result.Rows[4].CategoryNo.Should().Be(1);
        result.Rows[4].RowNo.Should().Be(2);
        // 行 6: 外购成品（序号 1-3）
        result.Rows[5].Seq.Should().Be(6);
        result.Rows[5].Category.Should().Be("原料");
        result.Rows[5].Section.Should().Be("外购成品");
        result.Rows[5].CategoryNo.Should().Be(1);
        result.Rows[5].RowNo.Should().Be(3);
        // 行 7: 原料汇总（序号留空）
        result.Rows[6].Seq.Should().Be(7);
        result.Rows[6].Category.Should().Be("原料");
        result.Rows[6].Section.Should().Be("汇总");
        result.Rows[6].IsSummary.Should().BeTrue();
        // 行 8-12: 生产工段（序号 2-1~2-5）
        result.Rows[7].Seq.Should().Be(8);
        result.Rows[7].Category.Should().Be("投料-在产");
        result.Rows[7].CategoryNo.Should().Be(2);
        result.Rows[7].RowNo.Should().Be(1);
        result.Rows[8].Seq.Should().Be(9);
        result.Rows[8].CategoryNo.Should().Be(2);
        result.Rows[8].RowNo.Should().Be(2);
        result.Rows[9].Seq.Should().Be(10);
        result.Rows[9].RowNo.Should().Be(3);
        result.Rows[10].Seq.Should().Be(11);
        result.Rows[10].RowNo.Should().Be(4);
        result.Rows[11].Seq.Should().Be(12);
        result.Rows[11].RowNo.Should().Be(5);
        // 行 13: 生产汇总
        result.Rows[12].Seq.Should().Be(13);
        result.Rows[12].Category.Should().Be("投料-在产");
        result.Rows[12].Section.Should().Be("汇总");
        result.Rows[12].IsSummary.Should().BeTrue();
        // 行 14: 成检（序号 3-1）/ 行 15: 成检汇总（序号留空）
        result.Rows[13].Seq.Should().Be(14);
        result.Rows[13].Category.Should().Be("投料-成检");
        result.Rows[13].CategoryNo.Should().Be(3);
        result.Rows[13].RowNo.Should().Be(1);
        result.Rows[14].Seq.Should().Be(15);
        result.Rows[14].Category.Should().Be("投料-成检");
        result.Rows[14].Section.Should().Be("汇总");
        result.Rows[14].IsSummary.Should().BeTrue();
        // 行 16: 整体完工预计（序号留空，最后一行）
        result.Rows[15].Seq.Should().Be(16);
        result.Rows[15].Category.Should().Be("整体完工预计");
        result.Rows[15].CategoryNo.Should().Be(0);
    }

    [Fact]
    public async Task GetOverviewAsync_完善计划量_仅D完善计划合计_且待产量列删除()
    {
        using var ctx = CreateDbContext();
        // D完善计划两单：待投料 = 7800 + 5000 = 12800kg → 13 吨
        SeedSummary(ctx, "WO-D1", 2, totalWeight: 10000m, finishPlanWeight: 2000m, finishInWeight: 0m,
            inputWeight: 1000m, rawMaterialLockRemark: RawMaterialLockRemarkKeys.ImprovePlan);
        SeedSummary(ctx, "WO-D2", 2, totalWeight: 5000m, finishPlanWeight: 0m, finishInWeight: 0m,
            inputWeight: 500m, rawMaterialLockRemark: RawMaterialLockRemarkKeys.ImprovePlan);
        // A质量补料不计入待计划量
        SeedSummary(ctx, "WO-A", 2, totalWeight: 8000m, finishPlanWeight: 0m, finishInWeight: 0m,
            inputWeight: 0m, flowOutputRatio: 50m, rawMaterialLockRemark: RawMaterialLockRemarkKeys.QualityReplenish);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        var row = result.Rows[3];
        row.PendingPlanTons.Should().Be(13m);          // (7800+5000)/1000=12.8 → 13
        row.TotalRemainingTons.Should().BeNull();       // 待产量（待投料量）已删除
        row.InProcurementTons.Should().BeNull();
    }

    [Fact]
    public async Task GetOverviewAsync_执行计划与外购成品行_数值正确()
    {
        using var ctx = CreateDbContext();
        // 执行计划 = ΣC执行计划（ExecutePlan）待投料 = (6000×1.1−1000) + (4000×1.1−500) = 5600+3900 = 9500kg → 10 吨
        SeedSummary(ctx, "WO-C1", 2, totalWeight: 6000m, finishPlanWeight: 0m, finishInWeight: 0m,
            inputWeight: 1000m, rawMaterialLockRemark: RawMaterialLockRemarkKeys.ExecutePlan);
        SeedSummary(ctx, "WO-C2", 2, totalWeight: 4000m, finishPlanWeight: 0m, finishInWeight: 0m,
            inputWeight: 500m, rawMaterialLockRemark: RawMaterialLockRemarkKeys.ExecutePlan);
        // 完善计划（WO-D1）不计入执行计划行
        SeedSummary(ctx, "WO-D1", 2, totalWeight: 10000m, finishPlanWeight: 2000m, finishInWeight: 0m,
            inputWeight: 1000m, rawMaterialLockRemark: RawMaterialLockRemarkKeys.ImprovePlan);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        // 执行计划行 = C执行计划待投料 10 吨
        result.Rows[4].PendingPlanTons.Should().Be(10m);
        // 完善计划行 = D完善计划待投料 = 7800kg → 8 吨（与执行计划互不串行）
        result.Rows[3].PendingPlanTons.Should().Be(8m);
        // 外购成品行 = ΣMax(0, 成品计划量-已到货)（仅 stage2）= 2000kg → 2 吨
        result.Rows[5].InProcurementTons.Should().Be(2m);
    }

    [Fact]
    public async Task GetOverviewAsync_完善计划行日期桶_按D完善计划工单交货日期分布()
    {
        using var ctx = CreateDbContext();
        // WO-D1：D完善计划，交期今天 → 桶1（7800kg → 8 吨）
        SeedSummary(ctx, "WO-D1", 2, deliveryDate: DateTime.Today,
            totalWeight: 10000m, finishPlanWeight: 2000m, finishInWeight: 0m, inputWeight: 1000m,
            rawMaterialLockRemark: RawMaterialLockRemarkKeys.ImprovePlan);
        // WO-D2：D完善计划，交期 20 天后 → 桶3（16~30 天，5000kg → 5 吨）
        SeedSummary(ctx, "WO-D2", 2, deliveryDate: DateTime.Today.AddDays(20),
            totalWeight: 5000m, finishPlanWeight: 0m, finishInWeight: 0m, inputWeight: 500m,
            rawMaterialLockRemark: RawMaterialLockRemarkKeys.ImprovePlan);
        // WO-A：A质量补料，交期今天，非 D完善计划不计入待计划行日期桶
        SeedSummary(ctx, "WO-A", 2, deliveryDate: DateTime.Today,
            totalWeight: 8000m, finishPlanWeight: 0m, finishInWeight: 0m, inputWeight: 0m,
            flowOutputRatio: 50m, rawMaterialLockRemark: RawMaterialLockRemarkKeys.QualityReplenish);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();
        var row = result.Rows[3];

        row.DateBucketTons.Count.Should().Be(7);
        row.DateBucketTons[0].Should().Be(8m);   // 交期截止-今日桶（≤今日）：仅 D 工单 WO-D1，A 质量补料不计入
        row.DateBucketTons[3].Should().Be(5m);   // 今日+16~+30 桶：WO-D2
        row.DateBucketTons.Where((_, i) => i is not 0 and not 3).Should().OnlyContain(v => v == 0m);
    }

    // ========== 类别汇总行 ==========

    [Fact]
    public async Task GetOverviewAsync_原料汇总_完善执行计划与在购量及日期桶_等于三明细行合计()
    {
        using var ctx = CreateDbContext();
        // D完善计划：WO-D1 今日（7800kg → 8 吨）、WO-D2 20 天后（5000kg → 5 吨），合计 13 吨
        SeedSummary(ctx, "WO-D1", 2, deliveryDate: DateTime.Today,
            totalWeight: 10000m, finishPlanWeight: 2000m, finishInWeight: 0m, inputWeight: 1000m,
            rawMaterialLockRemark: RawMaterialLockRemarkKeys.ImprovePlan);
        SeedSummary(ctx, "WO-D2", 2, deliveryDate: DateTime.Today.AddDays(20),
            totalWeight: 5000m, finishPlanWeight: 0m, finishInWeight: 0m, inputWeight: 500m,
            rawMaterialLockRemark: RawMaterialLockRemarkKeys.ImprovePlan);
        // C执行计划：WO-C1 20 天后（(6000×1.1−1000)=5600kg → 6 吨）
        SeedSummary(ctx, "WO-C1", 2, deliveryDate: DateTime.Today.AddDays(20),
            totalWeight: 6000m, finishPlanWeight: 0m, finishInWeight: 0m, inputWeight: 1000m,
            rawMaterialLockRemark: RawMaterialLockRemarkKeys.ExecutePlan);
        // A质量补料不计入完善/执行/外购任何明细行
        SeedSummary(ctx, "WO-A", 2, deliveryDate: DateTime.Today,
            totalWeight: 8000m, finishPlanWeight: 0m, finishInWeight: 0m, inputWeight: 0m,
            flowOutputRatio: 50m, rawMaterialLockRemark: RawMaterialLockRemarkKeys.QualityReplenish);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();
        var raw = result.Rows[6];

        raw.IsSummary.Should().BeTrue();
        // 待计划量 = 完善计划 + 执行计划 = 13 + 6 = 19 吨
        raw.PendingPlanTons.Should().Be(19m);
        raw.PendingPlanTons.Should().Be((result.Rows[3].PendingPlanTons ?? 0) + (result.Rows[4].PendingPlanTons ?? 0));
        // 在购量 = 外购成品（成购缺口）= WO-D1 的 2000kg → 2 吨
        raw.InProcurementTons.Should().Be(2m);
        raw.InProcurementTons.Should().Be(result.Rows[5].InProcurementTons);
        // 待产量列不参与原料汇总
        raw.TotalRemainingTons.Should().BeNull();
        raw.EstDays.Should().BeNull();
        raw.EstDeadline.Should().BeNull();
        // 日期桶 = 完善计划 + 执行计划 + 外购成品 对应桶求和
        for (int i = 0; i < result.DateBuckets.Count; i++)
        {
            var expected = result.Rows[3].DateBucketTons[i]
                + result.Rows[4].DateBucketTons[i]
                + result.Rows[5].DateBucketTons[i];
            raw.DateBucketTons[i].Should().Be(expected);
        }
        // 桶内合计校验：桶1（交期截止-今日）= 完善 8 + 外购 2 = 10；桶4（今日+16~+30）= 完善 5 + 执行 6 = 11
        raw.DateBucketTons[0].Should().Be(10m);
        raw.DateBucketTons[3].Should().Be(11m);
    }

    [Fact]
    public async Task GetOverviewAsync_冷轧5060_同批次50与60两道次未到达_各计一次构成合重量()
    {
        using var ctx = CreateDbContext();
        // 批次流转 1000kg：工序 荒管处理(1)→冷轧60(2)→冷轧50(3)，未开始生产（CurrentGroupName 空）：
        // 「冷轧5060」= 50+60 合重量，两道次均未到达 → 各计入 1000kg → 2000kg（非只计第一道次的 1000kg）
        var batch = SeedBatch(ctx, "B1", BatchStatus.InProgress, 1000, DateTime.Today);
        await ctx.SaveChangesAsync();
        SeedProcessGroup(ctx, batch, 1, ProcessKeys.RoughTubeProcessing, outerPolish: 1);
        SeedProcessGroup(ctx, batch, 2, ProcessKeys.ColdRoll60, manufacturingSpec: "180*6");
        SeedProcessGroup(ctx, batch, 3, ProcessKeys.ColdRoll50, manufacturingSpec: "219*8");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        // 冷轧5060 行（Rows[8]）：60+50 两道次各 1 吨 → 合重量 2 吨
        var cr50_60 = result.Rows[8];
        cr50_60.TotalRemainingTons.Should().Be(2m);
        // 冷轧2030/三辊/冷拔行无匹配工序组 → 0
        result.Rows[9].TotalRemainingTons.Should().Be(0m);
        result.Rows[10].TotalRemainingTons.Should().Be(0m);
        result.Rows[11].TotalRemainingTons.Should().Be(0m);
    }

    [Fact]
    public async Task GetOverviewAsync_冷轧5060_同批次多次60多次50各道次各计待产量()
    {
        using var ctx = CreateDbContext();
        // 批次流转 1000kg：工序 荒管(1)→60(2,180*6)→50(3,159*6)→60(4,114*4)→50(5,89*3)，未开始生产：
        // 多次60、多次50 与多次冷拔同语义：每道未到达的 60/50 各计一次 → 冷轧5060 = 4×1000 = 4000kg
        var batch = SeedBatch(ctx, "B1", BatchStatus.InProgress, 1000, DateTime.Today);
        await ctx.SaveChangesAsync();
        SeedProcessGroup(ctx, batch, 1, ProcessKeys.RoughTubeProcessing, outerPolish: 1);
        SeedProcessGroup(ctx, batch, 2, ProcessKeys.ColdRoll60, manufacturingSpec: "180*6");
        SeedProcessGroup(ctx, batch, 3, ProcessKeys.ColdRoll50, manufacturingSpec: "159*6");
        SeedProcessGroup(ctx, batch, 4, ProcessKeys.ColdRoll60, manufacturingSpec: "114*4");
        SeedProcessGroup(ctx, batch, 5, ProcessKeys.ColdRoll50, manufacturingSpec: "89*3");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        // 冷轧5060 行（Rows[8]）：4 道次各 1 吨 → 4 吨
        result.Rows[8].TotalRemainingTons.Should().Be(4m);
    }

    [Fact]
    public async Task GetOverviewAsync_拉机行_同批次多次冷拔规格不同_各计一次待产量()
    {
        using var ctx = CreateDbContext();
        // 批次流转 1000kg：工序 荒管(1)→冷拔(2,规格180*6)→冷轧30(3)→冷拔(4,规格219*8)，未开始生产：
        // 多次冷拔每次规格不同，属独立加工道次 → 拉机行两道次各计入 1000kg → 2000kg
        var batch = SeedBatch(ctx, "B1", BatchStatus.InProgress, 1000, DateTime.Today);
        await ctx.SaveChangesAsync();
        SeedProcessGroup(ctx, batch, 1, ProcessKeys.RoughTubeProcessing, outerPolish: 1);
        SeedProcessGroup(ctx, batch, 2, ProcessKeys.ColdDraw, coldRollDraw: 1, manufacturingSpec: "180*6");
        SeedProcessGroup(ctx, batch, 3, ProcessKeys.ColdRoll30, manufacturingSpec: "159*6");
        SeedProcessGroup(ctx, batch, 4, ProcessKeys.ColdDraw, coldRollDraw: 1, manufacturingSpec: "219*8");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        // 拉机行（Rows[11]）：两次冷拔各计 1 吨 → 2 吨
        result.Rows[11].TotalRemainingTons.Should().Be(2m);
    }

    [Fact]
    public async Task GetOverviewAsync_冷轧5060_当前工序组内已过冷轧拔做脱脂未完工_不计入待产()
    {
        using var ctx = CreateDbContext();
        // 批次流转 1000kg，工序组 冷轧50(2)：冷轧拔=序号6、脱脂=序号8；当前在 ColdRoll50 组内脱脂工段未完工。
        // 冷轧拔(第一道工段)已完成 → 已轧完、不再占用 50/60 机台 → 不计入 5060 行待产
        var batch = SeedBatch(ctx, "B1", BatchStatus.InProgress, 1000, DateTime.Today);
        await ctx.SaveChangesAsync();
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            SequenceNumber = 2,
            ProcessName = ProcessKeys.ColdRoll50,
            ColdRollDraw = 6,
            Degrease = 8
        });
        batch.CurrentGroupName = ProcessKeys.ColdRoll50;
        batch.CurrentSectionName = SectionKeys.Degrease;
        batch.CurrentSectionCompleted = false;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        // 冷轧5060 行（Rows[8]）：已轧完 → 不计入
        result.Rows[8].TotalRemainingTons.Should().Be(0m);
    }

    [Fact]
    public async Task GetOverviewAsync_冷轧5060_当前工序组内冷轧拔生产中_计入待产()
    {
        using var ctx = CreateDbContext();
        // 批次流转 1000kg，当前在 ColdRoll50 组内冷轧拔(序号6)工段未完工（正在轧制）→ 计入 5060 行待产
        var batch = SeedBatch(ctx, "B1", BatchStatus.InProgress, 1000, DateTime.Today);
        await ctx.SaveChangesAsync();
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            SequenceNumber = 2,
            ProcessName = ProcessKeys.ColdRoll50,
            ColdRollDraw = 6,
            Degrease = 8
        });
        batch.CurrentGroupName = ProcessKeys.ColdRoll50;
        batch.CurrentSectionName = SectionKeys.ColdRollDraw;
        batch.CurrentSectionCompleted = false;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        // 冷轧5060 行（Rows[8]）：冷轧拔生产中 → 计入 1 吨
        result.Rows[8].TotalRemainingTons.Should().Be(1m);
    }

    [Fact]
    public async Task GetOverviewAsync_生产汇总_按批次去重_区别于工段行重复统计()
    {
        using var ctx = CreateDbContext();
        // 单批次含「荒管处理(带抛光) + 50冷轧」两个工序组，且未开始生产（CurrentGroupName 空）：
        // 荒管抛光行与冷轧5060行均计入该批次 6000kg（节点重复统计），生产汇总行按批次只计一次
        var batch = SeedBatch(ctx, "B1", BatchStatus.InProgress, 6000, DateTime.Today);
        await ctx.SaveChangesAsync();
        SeedProcessGroup(ctx, batch, 1, ProcessKeys.RoughTubeProcessing, outerPolish: 1);
        SeedProcessGroup(ctx, batch, 2, ProcessKeys.ColdRoll50);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        // 荒管抛光（行7）与冷轧5060（行8）各计入 6000kg → 6 吨
        result.Rows[7].TotalRemainingTons.Should().Be(6m);
        result.Rows[8].TotalRemainingTons.Should().Be(6m);
        // 生产汇总（行12）= 批次去重 6000kg → 6 吨（非两节点之和 12）
        var prod = result.Rows[12];
        prod.IsSummary.Should().BeTrue();
        prod.TotalRemainingTons.Should().Be(6m);
        // 生产汇总行预计天数/完成日留空（用户决策），防与工段行口径混淆
        prod.EstDays.Should().BeNull();
        prod.EstDeadline.Should().BeNull();
    }

    // ========== 冷轧工段待产量按待生产产类拆分（2026-08-19 任务 E） ==========

    [Fact]
    public async Task GetOverviewAsync_冷轧工段待产量_按待生产产类拆分在制成品附加量()
    {
        using var ctx = CreateDbContext();
        // B1：冷轧20 制造规格 == 成品规格 219*8 且 制造物品 OrderFinished（成品类）→ 成品
        var b1 = SeedBatch(ctx, "B1", BatchStatus.InProgress, 5000, DateTime.Today);
        // B2：冷轧20 制造规格 ≠ 成品规格 → 在制
        var b2 = SeedBatch(ctx, "B2", BatchStatus.InProgress, 5000, DateTime.Today);
        await ctx.SaveChangesAsync();
        SeedProcessGroup(ctx, b1, 1, ProcessKeys.ColdRoll20, manufacturingSpec: "219*8");
        SeedProcessGroup(ctx, b2, 1, ProcessKeys.ColdRoll20, manufacturingSpec: "180*6");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        // 冷轧2030 行（序号 2-3，Rows[9]）：总量 10 吨，在制 5 吨，成品 5 吨
        var cr20_30 = result.Rows[9];
        cr20_30.TotalRemainingTons.Should().Be(10m);
        cr20_30.PendingInProgressTons.Should().Be(5m);
        cr20_30.PendingFinishedTons.Should().Be(5m);
        // 冷轧5060 行（序号 2-2，Rows[8]）：无匹配批次 → 总量 0，附加量 0（前端不显示）
        var cr50_60 = result.Rows[8];
        cr50_60.TotalRemainingTons.Should().Be(0m);
        cr50_60.PendingInProgressTons.Should().Be(0m);
        cr50_60.PendingFinishedTons.Should().Be(0m);
        // 荒管抛光行（序号 2-1）不拆分产类 → 附加量恒 null（区别于冷轧行的 0）
        result.Rows[7].PendingInProgressTons.Should().BeNull();
        result.Rows[7].PendingFinishedTons.Should().BeNull();
    }

    [Fact]
    public async Task GetOverviewAsync_冷轧工段待产量_制造规格不匹配或非成品物品_判在制()
    {
        using var ctx = CreateDbContext();
        // B1：制造规格 == 成品规格，但制造物品非成品类（MaterialType 不含成品含义）→ 在制
        var b1 = SeedBatch(ctx, "B1", BatchStatus.InProgress, 5000, DateTime.Today,
            manufacturingItem: "SolutionAnnealed");
        // B2：制造物品是成品类，但制造规格 ≠ 成品规格 → 在制
        var b2 = SeedBatch(ctx, "B2", BatchStatus.InProgress, 5000, DateTime.Today);
        await ctx.SaveChangesAsync();
        SeedProcessGroup(ctx, b1, 1, ProcessKeys.ColdRoll50, manufacturingSpec: "219*8");
        SeedProcessGroup(ctx, b2, 1, ProcessKeys.ColdRoll50, manufacturingSpec: "180*6");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        // 冷轧5060 行（序号 2-2，Rows[8]）：两个批次均判在制 → 在制 10 吨、成品 0
        var cr50_60 = result.Rows[8];
        cr50_60.TotalRemainingTons.Should().Be(10m);
        cr50_60.PendingInProgressTons.Should().Be(10m);
        cr50_60.PendingFinishedTons.Should().Be(0m);
    }

    [Fact]
    public async Task GetOverviewAsync_生产汇总_未产与在产计入_完成不计入()
    {
        using var ctx = CreateDbContext();
        // 在产 6000kg + 未产 4000kg 计入；完成批次不计入
        SeedBatch(ctx, "B1", BatchStatus.InProgress, 6000, DateTime.Today);
        SeedBatch(ctx, "B2", BatchStatus.None, 4000, DateTime.Today);
        SeedBatch(ctx, "B3", BatchStatus.Completed, 99999, DateTime.Today);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();
        var prod = result.Rows[12];

        prod.TotalRemainingTons.Should().Be(10m); // (6000+4000)/1000 = 10
    }

    [Fact]
    public async Task GetOverviewAsync_生产汇总日期桶_按批次交货日期分布()
    {
        using var ctx = CreateDbContext();
        // 在产 6000kg 交期今天 → 桶1；未产 4000kg 交期 20 天后 → 桶3（16~30 天）
        SeedBatch(ctx, "B1", BatchStatus.InProgress, 6000, DateTime.Today);
        SeedBatch(ctx, "B2", BatchStatus.None, 4000, DateTime.Today.AddDays(20));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();
        var prod = result.Rows[12];

        prod.DateBucketTons.Count.Should().Be(7);
        prod.DateBucketTons[0].Should().Be(6m);
        prod.DateBucketTons[3].Should().Be(4m);   // 今日+16~+30 桶：B2（20 天后）
        prod.DateBucketTons.Where((_, i) => i is not 0 and not 3).Should().OnlyContain(v => v == 0m);
    }

    [Fact]
    public async Task GetOverviewAsync_成检汇总_等于成检行自身()
    {
        using var ctx = CreateDbContext();
        var kanban = new List<FinalInspectionPlanDto>
        {
            new() { ProductionBatchId = 1, KanbanStage = "待检验", ProductionWeight = 3000m },
            new() { ProductionBatchId = 2, KanbanStage = "检验中", ProductionWeight = 2000m },
            new() { ProductionBatchId = 3, KanbanStage = "待到料", ProductionWeight = 9999m },   // 非待检验/检验中不计
            new() { ProductionBatchId = 1, KanbanStage = "检验中", ProductionWeight = 9999m },   // 同批次去重
        };
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, kanban);
        var result = await svc.GetOverviewAsync();

        // 成检行（行13）与成检汇总（行14）均为 (3000+2000)/1000 = 5 吨
        var fi = result.Rows[13];
        fi.TotalRemainingTons.Should().Be(5m);
        var fiSum = result.Rows[14];
        fiSum.IsSummary.Should().BeTrue();
        fiSum.TotalRemainingTons.Should().Be(5m);
    }

    [Fact]
    public async Task GetOverviewAsync_订单延期原料_主值工单重量_副值投料缺少量()
    {
        using var ctx = CreateDbContext();
        // WO-R1：原料锁定(2)、延期（交期今天、预计+10）→ 桶1
        // 主值 10000kg → 10 吨；副值 CalcPending=(10000−0)×1.1−2000=9000kg → 9 吨
        SeedSummary(ctx, "WO-R1", 2, totalWeight: 10000m, finishPlanWeight: 0m, finishInWeight: 0m,
            inputWeight: 2000m, deliveryDate: DateTime.Today,
            estimatedProcessCompletionDate: DateTime.Today.AddDays(10));
        // WO-R2：原料锁定(2)、非延期（预计完成今天）→ 不计
        SeedSummary(ctx, "WO-R2", 2, totalWeight: 3000m, finishPlanWeight: 0m, finishInWeight: 0m,
            inputWeight: 0m, deliveryDate: DateTime.Today,
            estimatedProcessCompletionDate: DateTime.Today);
        // WO-P：生产执行(3)、延期 → 不计入原料行
        SeedSummary(ctx, "WO-P", 3, totalWeight: 5000m, deliveryDate: DateTime.Today.AddDays(5),
            estimatedProcessCompletionDate: DateTime.Today.AddDays(20));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        var row = result.Rows[0];
        row.Seq.Should().Be(1);
        row.Category.Should().Be("订单交期负荷");
        row.Section.Should().Be("订单延期-原料");
        row.SubValuePrefix.Should().Be("待料");
        row.DateBucketSubOnly.Should().BeTrue();
        row.DateBucketTons.Count.Should().Be(7);
        row.DateBucketTons[0].Should().Be(10m);
        row.DateBucketSubTons[0].Should().Be(9m);
        row.DateBucketTons[1].Should().Be(0m);
        row.DateBucketSubTons[1].Should().Be(0m);
    }

    [Fact]
    public async Task GetOverviewAsync_订单延期生产_副值在产未产批次理论成品重量()
    {
        using var ctx = CreateDbContext();
        // WO-P1：生产执行(3)、延期（交期+5、预计+20）→ 桶2
        SeedSummary(ctx, "WO-P1", 3, totalWeight: 5000m, deliveryDate: DateTime.Today.AddDays(5),
            estimatedProcessCompletionDate: DateTime.Today.AddDays(20));
        // 在产 4000kg + 未产 2000kg 理论成品重量 → 副值 6 吨；完成批次（查询已排除）不计
        SeedBatch(ctx, "B-P1", BatchStatus.InProgress, 6000, workOrderNo: "WO-P1", theoreticalOutputWeight: 4000);
        SeedBatch(ctx, "B-P2", BatchStatus.None, 3000, workOrderNo: "WO-P1", theoreticalOutputWeight: 2000);
        SeedBatch(ctx, "B-P3", BatchStatus.Completed, 99999, workOrderNo: "WO-P1", theoreticalOutputWeight: 9999);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        var row = result.Rows[1];
        row.Seq.Should().Be(2);
        row.Section.Should().Be("订单延期-在产");
        row.SubValuePrefix.Should().Be("在产");
        row.DateBucketSubOnly.Should().BeTrue();
        row.DateBucketTons[1].Should().Be(5m);    // 主值 5000kg → 5 吨
        row.DateBucketSubTons[1].Should().Be(6m); // 4000+2000 → 6 吨
        row.DateBucketTons[0].Should().Be(0m);    // WO-P1 交期+5 不在桶1区间
        row.DateBucketSubTons[0].Should().Be(0m);
    }

    [Fact]
    public async Task GetOverviewAsync_订单延期成检_副值成检批次理论成品重量()
    {
        using var ctx = CreateDbContext();
        // WO-F1：成品检验(4)、延期（交期今天、预计+10）→ 桶1
        SeedSummary(ctx, "WO-F1", 4, totalWeight: 6000m, deliveryDate: DateTime.Today,
            estimatedProcessCompletionDate: DateTime.Today.AddDays(10));
        // 成检批次 2000kg 理论成品重量 → 副值 2 吨；在产批次不计入成检行
        SeedBatch(ctx, "B-F1", BatchStatus.InFinalInspection, 5000, workOrderNo: "WO-F1", theoreticalOutputWeight: 2000);
        SeedBatch(ctx, "B-F2", BatchStatus.InProgress, 99999, workOrderNo: "WO-F1", theoreticalOutputWeight: 9999);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        var row = result.Rows[2];
        row.Seq.Should().Be(3);
        row.Section.Should().Be("订单延期-成检");
        row.SubValuePrefix.Should().Be("在检");
        row.DateBucketSubOnly.Should().BeTrue();
        row.DateBucketTons[0].Should().Be(6m);
        row.DateBucketSubTons[0].Should().Be(2m);
        row.DateBucketTons[1].Should().Be(0m);
        row.DateBucketSubTons[1].Should().Be(0m);
    }

    [Fact]
    public async Task GetOverviewAsync_含110机台组_动态出行且按DisplayOrder排最前()
    {
        using var ctx = CreateDbContext();
        // 批次 1000kg：工序 荒管(1)→冷轧110(2)，未开始生产 → 110 组行应计入 1 吨
        var batch = SeedBatch(ctx, "B1", BatchStatus.InProgress, 1000, DateTime.Today);
        await ctx.SaveChangesAsync();
        SeedProcessGroup(ctx, batch, 1, ProcessKeys.RoughTubeProcessing, outerPolish: 1);
        SeedProcessGroup(ctx, batch, 2, "ColdRoll110", manufacturingSpec: "110*6");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx); // 先种 4 组（5060/2030/三辊/拉机，DisplayOrder 1-4）
        // 追加 110 组（DisplayOrder=0 确保排最前）：完全遍历含 110，生产工段行 = 荒管抛光 + 5 组共 6 行
        ctx.ColdRollMachineGroupConfigs.Add(new ColdRollMachineGroupConfig { GroupKey = "110", DisplayName = "110组", ProcessKeys = "ColdRoll110", DisplayOrder = 0 });
        await ctx.SaveChangesAsync();

        var result = await svc.GetOverviewAsync();

        // 行序：延期3 + 原料4 + [荒管抛光,110,5060,2030,三辊,拉机] + 生产汇总 + 成检 + 成检汇总 + 整体完工 = 17 行
        result.Rows.Should().HaveCount(17);
        result.Rows[8].Section.Should().Be("[累]110组");
        result.Rows[8].TotalRemainingTons.Should().Be(1m);
        result.Rows[9].Section.Should().Be("[累]5060组");
        result.Rows[9].TotalRemainingTons.Should().Be(0m);
        result.Rows[12].Section.Should().Be("[累]拉机组");
        result.Rows[12].TotalRemainingTons.Should().Be(0m);
        // 110 组无产能档案 → 运行时无兜底（产能=0）→ 预计天数空（2026-08-30 去运行时兜底）
        result.Rows[8].EstDays.Should().BeNull();
    }
}
