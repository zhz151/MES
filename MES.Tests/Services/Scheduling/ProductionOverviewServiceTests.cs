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
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        var capacityMock = new Mock<IDailyProductionCapacityService>();
        capacityMock.Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<DailyProductionCapacityDto>());
        var fiMock = new Mock<IFinalInspectionPlanService>();
        fiMock.Setup(x => x.GetKanbanAsync())
            .ReturnsAsync(kanban ?? new List<FinalInspectionPlanDto>());
        return new ProductionOverviewService(ctx, configMock.Object, capacityMock.Object,
            CreateProcessDefinitionServiceMock(), fiMock.Object);
    }

    private static WorkOrderExecutionSummary SeedSummary(AppDbContext ctx, string workOrderNo,
        int scheduleStage, decimal totalWeight = 0m,
        decimal finishPlanWeight = 0m, decimal finishInWeight = 0m,
        decimal inputWeight = 0m, decimal flowOutputRatio = 0m,
        decimal pendingRoughTubeWeight = 0m, string? rawMaterialLockRemark = null,
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
            PendingRoughTubeWeight = pendingRoughTubeWeight,
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

    private static void SeedProcessGroup(AppDbContext ctx, ProductionBatch batch, int seq, string processName, int? outerPolish = null, string? manufacturingSpec = null)
    {
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            SequenceNumber = seq,
            ProcessName = processName,
            OuterPolish = outerPolish,
            ManufacturingSpec = manufacturingSpec
        });
    }

    [Fact]
    public async Task GetOverviewAsync_行重排_完善计划执行计划外购成品原料汇总_生产工段生产汇总成检成检汇总整体完工预计()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO-D1", 2, totalWeight: 10000m, finishPlanWeight: 2000m, finishInWeight: 0m,
            inputWeight: 1000m, pendingRoughTubeWeight: 1000m, rawMaterialLockRemark: RawMaterialLockRemarkKeys.ImprovePlan);
        SeedSummary(ctx, "WO-D2", 2, totalWeight: 5000m, finishPlanWeight: 0m, finishInWeight: 0m,
            inputWeight: 500m, pendingRoughTubeWeight: 2000m, rawMaterialLockRemark: RawMaterialLockRemarkKeys.ImprovePlan);
        SeedSummary(ctx, "WO-A", 2, totalWeight: 8000m, finishPlanWeight: 0m, finishInWeight: 0m,
            inputWeight: 0m, flowOutputRatio: 50m, rawMaterialLockRemark: RawMaterialLockRemarkKeys.QualityReplenish);
        SeedSummary(ctx, "WO-X", 3, totalWeight: 2000m);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        result.Rows.Should().HaveCount(19);
        // 行 1-3: 订单延期-原料/在产/成检（订单交期负荷，序号留空）
        result.Rows[0].Seq.Should().Be(1);
        result.Rows[0].Category.Should().Be("订单交期负荷");
        result.Rows[0].Section.Should().Be("订单延期-原料");
        result.Rows[0].SubValuePrefix.Should().Be("待料");
        result.Rows[1].Seq.Should().Be(2);
        result.Rows[1].Category.Should().Be("订单交期负荷");
        result.Rows[1].Section.Should().Be("订单延期-在产");
        result.Rows[1].SubValuePrefix.Should().Be("在产");
        result.Rows[2].Seq.Should().Be(3);
        result.Rows[2].Category.Should().Be("订单交期负荷");
        result.Rows[2].Section.Should().Be("订单延期-成检");
        result.Rows[2].SubValuePrefix.Should().Be("在检");
        // 行 4-6: 订单延期量/订单延期量[预计完结]/订单非延期（序号留空）
        result.Rows[3].Seq.Should().Be(4);
        result.Rows[3].Category.Should().Be("订单交期负荷");
        result.Rows[3].Section.Should().Be("订单延期量");
        result.Rows[3].CategoryNo.Should().Be(0);
        result.Rows[4].Seq.Should().Be(5);
        result.Rows[4].Category.Should().Be("订单交期负荷");
        result.Rows[4].Section.Should().Be("订单延期量[预计完结]");
        result.Rows[4].CategoryNo.Should().Be(0);
        result.Rows[5].Seq.Should().Be(6);
        result.Rows[5].Category.Should().Be("订单交期负荷");
        result.Rows[5].Section.Should().Be("订单非延期");
        result.Rows[5].CategoryNo.Should().Be(0);
        // 行 7: 整体完工预计（序号留空）
        result.Rows[6].Seq.Should().Be(7);
        result.Rows[6].Category.Should().Be("整体完工预计");
        result.Rows[6].CategoryNo.Should().Be(0);
        // 行 8: 完善计划（序号 1-1）
        result.Rows[7].Seq.Should().Be(8);
        result.Rows[7].Category.Should().Be("原料");
        result.Rows[7].Section.Should().Be("完善计划");
        result.Rows[7].CategoryNo.Should().Be(1);
        result.Rows[7].RowNo.Should().Be(1);
        // 行 9: 执行计划（序号 1-2）
        result.Rows[8].Seq.Should().Be(9);
        result.Rows[8].Category.Should().Be("原料");
        result.Rows[8].Section.Should().Be("执行计划");
        result.Rows[8].CategoryNo.Should().Be(1);
        result.Rows[8].RowNo.Should().Be(2);
        // 行 10: 外购成品（序号 1-3）
        result.Rows[9].Seq.Should().Be(10);
        result.Rows[9].Category.Should().Be("原料");
        result.Rows[9].Section.Should().Be("外购成品");
        result.Rows[9].CategoryNo.Should().Be(1);
        result.Rows[9].RowNo.Should().Be(3);
        // 行 11: 原料汇总（序号留空）
        result.Rows[10].Seq.Should().Be(11);
        result.Rows[10].Category.Should().Be("原料");
        result.Rows[10].Section.Should().Be("汇总");
        result.Rows[10].IsSummary.Should().BeTrue();
        // 行 12-16: 生产工段（序号 2-1~2-5）
        result.Rows[11].Seq.Should().Be(12);
        result.Rows[11].Category.Should().Be("投料-在产");
        result.Rows[11].CategoryNo.Should().Be(2);
        result.Rows[11].RowNo.Should().Be(1);
        result.Rows[12].Seq.Should().Be(13);
        result.Rows[12].CategoryNo.Should().Be(2);
        result.Rows[12].RowNo.Should().Be(2);
        result.Rows[13].Seq.Should().Be(14);
        result.Rows[13].RowNo.Should().Be(3);
        result.Rows[14].Seq.Should().Be(15);
        result.Rows[14].RowNo.Should().Be(4);
        result.Rows[15].Seq.Should().Be(16);
        result.Rows[15].RowNo.Should().Be(5);
        // 行 17: 生产汇总
        result.Rows[16].Seq.Should().Be(17);
        result.Rows[16].Category.Should().Be("投料-在产");
        result.Rows[16].Section.Should().Be("汇总");
        result.Rows[16].IsSummary.Should().BeTrue();
        // 行 18: 成检（序号 3-1）/ 行 19: 成检汇总（序号留空）
        result.Rows[17].Seq.Should().Be(18);
        result.Rows[17].Category.Should().Be("投料-成检");
        result.Rows[17].CategoryNo.Should().Be(3);
        result.Rows[17].RowNo.Should().Be(1);
        result.Rows[18].Seq.Should().Be(19);
        result.Rows[18].Category.Should().Be("投料-成检");
        result.Rows[18].Section.Should().Be("汇总");
        result.Rows[18].IsSummary.Should().BeTrue();
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

        var row = result.Rows[7];
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
        result.Rows[8].PendingPlanTons.Should().Be(10m);
        // 完善计划行 = D完善计划待投料 = 7800kg → 8 吨（与执行计划互不串行）
        result.Rows[7].PendingPlanTons.Should().Be(8m);
        // 外购成品行 = ΣMax(0, 成品计划量-已到货)（仅 stage2）= 2000kg → 2 吨
        result.Rows[9].InProcurementTons.Should().Be(2m);
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
        var row = result.Rows[7];

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
        var raw = result.Rows[10];

        raw.IsSummary.Should().BeTrue();
        // 待计划量 = 完善计划 + 执行计划 = 13 + 6 = 19 吨
        raw.PendingPlanTons.Should().Be(19m);
        raw.PendingPlanTons.Should().Be((result.Rows[7].PendingPlanTons ?? 0) + (result.Rows[8].PendingPlanTons ?? 0));
        // 在购量 = 外购成品（成购缺口）= WO-D1 的 2000kg → 2 吨
        raw.InProcurementTons.Should().Be(2m);
        raw.InProcurementTons.Should().Be(result.Rows[9].InProcurementTons);
        // 待产量列不参与原料汇总
        raw.TotalRemainingTons.Should().BeNull();
        raw.EstDays.Should().BeNull();
        raw.EstDeadline.Should().BeNull();
        // 日期桶 = 完善计划 + 执行计划 + 外购成品 对应桶求和
        for (int i = 0; i < result.DateBuckets.Count; i++)
        {
            var expected = result.Rows[7].DateBucketTons[i]
                + result.Rows[8].DateBucketTons[i]
                + result.Rows[9].DateBucketTons[i];
            raw.DateBucketTons[i].Should().Be(expected);
        }
        // 桶内合计校验：桶1（交期截止-今日）= 完善 8 + 外购 2 = 10；桶4（今日+16~+30）= 完善 5 + 执行 6 = 11
        raw.DateBucketTons[0].Should().Be(10m);
        raw.DateBucketTons[3].Should().Be(11m);
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

        // 荒管抛光（行12）与冷轧5060（行13）各计入 6000kg → 6 吨
        result.Rows[11].TotalRemainingTons.Should().Be(6m);
        result.Rows[12].TotalRemainingTons.Should().Be(6m);
        // 生产汇总（行17）= 批次去重 6000kg → 6 吨（非两节点之和 12）
        var prod = result.Rows[16];
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

        // 冷轧2030 行（序号 2-3，Rows[13]）：总量 10 吨，在制 5 吨，成品 5 吨
        var cr20_30 = result.Rows[13];
        cr20_30.TotalRemainingTons.Should().Be(10m);
        cr20_30.PendingInProgressTons.Should().Be(5m);
        cr20_30.PendingFinishedTons.Should().Be(5m);
        // 冷轧5060 行（序号 2-2，Rows[11]）：无匹配批次 → 总量 0，附加量 0（前端不显示）
        var cr50_60 = result.Rows[12];
        cr50_60.TotalRemainingTons.Should().Be(0m);
        cr50_60.PendingInProgressTons.Should().Be(0m);
        cr50_60.PendingFinishedTons.Should().Be(0m);
        // 荒管抛光行（序号 2-1）不拆分产类 → 附加量恒 null（区别于冷轧行的 0）
        result.Rows[11].PendingInProgressTons.Should().BeNull();
        result.Rows[11].PendingFinishedTons.Should().BeNull();
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

        // 冷轧5060 行（序号 2-2，Rows[12]）：两个批次均判在制 → 在制 10 吨、成品 0
        var cr50_60 = result.Rows[12];
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
        var prod = result.Rows[16];

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
        var prod = result.Rows[16];

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

        // 成检行（行18）与成检汇总（行19）均为 (3000+2000)/1000 = 5 吨
        var fi = result.Rows[17];
        fi.TotalRemainingTons.Should().Be(5m);
        var fiSum = result.Rows[18];
        fiSum.IsSummary.Should().BeTrue();
        fiSum.TotalRemainingTons.Should().Be(5m);
    }

    [Fact]
    public async Task GetOverviewAsync_订单延期量_按交货日期所在桶区间统计不累加()
    {
        using var ctx = CreateDbContext();
        // WO-1：交期今天、预计完成 +10 天 → 交货在桶1区间 [MinValue,今日]，预计完成>今日 → 仅桶1计入；桶2（今日+7）交期不在区间不计
        SeedSummary(ctx, "WO-1", 3, totalWeight: 2000m, deliveryDate: DateTime.Today,
            estimatedProcessCompletionDate: DateTime.Today.AddDays(10));
        // WO-2：交期 +5 天、预计完成 +20 天 → 交货在桶2区间 [今日+1,今日+7]，预计完成>今日+7 → 仅桶2计入；桶3（今日+15）交期不在区间不计
        SeedSummary(ctx, "WO-2", 3, totalWeight: 3000m, deliveryDate: DateTime.Today.AddDays(5),
            estimatedProcessCompletionDate: DateTime.Today.AddDays(20));
        // WO-3：交期今天、预计完成今天（已完成）→ 预计完成>今日 不成立，不计
        SeedSummary(ctx, "WO-3", 3, totalWeight: 5000m, deliveryDate: DateTime.Today,
            estimatedProcessCompletionDate: DateTime.Today);
        // WO-4：预计完成日 null → 不计
        SeedSummary(ctx, "WO-4", 3, totalWeight: 9000m, deliveryDate: DateTime.Today);
        // WO-5：主号完成(1)，页面口径排除 → 不计
        SeedSummary(ctx, "WO-5", 1, totalWeight: 8000m, deliveryDate: DateTime.Today,
            estimatedProcessCompletionDate: DateTime.Today.AddDays(10));
        // WO-6：交期今天、预计完成 +5 天 → 延期 5 天≤7，主值桶1含 2000kg，副值（超1周）不含
        SeedSummary(ctx, "WO-6", 3, totalWeight: 2000m, deliveryDate: DateTime.Today,
            estimatedProcessCompletionDate: DateTime.Today.AddDays(5));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        var delay = result.Rows[3];
        delay.Seq.Should().Be(4);
        delay.Category.Should().Be("订单交期负荷");
        delay.Section.Should().Be("订单延期量");
        delay.CategoryNo.Should().Be(0);
        delay.SubValuePrefix.Should().Be("超1周");
        delay.SubValueParenFormat.Should().BeTrue();
        delay.DateBucketTons.Count.Should().Be(7);
        delay.DateBucketSubTons.Count.Should().Be(7);
        // 桶1（交期截止-今日）：WO-1（延期10天）+ WO-6（延期5天） = 4000kg → 4 吨
        delay.DateBucketTons[0].Should().Be(4m);
        // 桶1 副值（超1周）：仅 WO-1（延期 10 天>7）= 2000kg → 2 吨（WO-6 延期 5 天≤7 不计）
        delay.DateBucketSubTons[0].Should().Be(2m);
        // 桶2（今日+7）：仅 WO-2 = 3000kg → 3 吨（WO-1 交期今日不在 [今日+1,今日+7]，不累加）
        delay.DateBucketTons[1].Should().Be(3m);
        // 桶2 副值（超1周）：WO-2（延期 15 天>7）= 3000kg → 3 吨
        delay.DateBucketSubTons[1].Should().Be(3m);
        // 桶3（今日+15）起均 0
        delay.DateBucketTons[2].Should().Be(0m);
        delay.DateBucketSubTons[2].Should().Be(0m);
        delay.DateBucketTons[3].Should().Be(0m);
        delay.DateBucketTons[4].Should().Be(0m);
        delay.DateBucketTons[5].Should().Be(0m);
        // 桶7（远日量）截止无穷大 → 恒 0
        delay.DateBucketTons[6].Should().Be(0m);
        delay.DateBucketSubTons[6].Should().Be(0m);
    }

    [Fact]
    public async Task GetOverviewAsync_订单延期量预计完结_按预计完成日期所在桶统计()
    {
        using var ctx = CreateDbContext();
        // WO-1：交期今天、预计完成 +10 天（延期）→ 预计完结桶2（今日+8~+15）
        SeedSummary(ctx, "WO-1", 3, totalWeight: 2000m, deliveryDate: DateTime.Today,
            estimatedProcessCompletionDate: DateTime.Today.AddDays(10));
        // WO-2：交期 +5 天、预计完成 +20 天（延期）→ 预计完结桶3（今日+16~+30）
        SeedSummary(ctx, "WO-2", 3, totalWeight: 3000m, deliveryDate: DateTime.Today.AddDays(5),
            estimatedProcessCompletionDate: DateTime.Today.AddDays(20));
        // WO-3：交期今天、预计完成今天（非延期）→ 不计
        SeedSummary(ctx, "WO-3", 3, totalWeight: 5000m, deliveryDate: DateTime.Today,
            estimatedProcessCompletionDate: DateTime.Today);
        // WO-4：预计完成日 null → 不计
        SeedSummary(ctx, "WO-4", 3, totalWeight: 9000m, deliveryDate: DateTime.Today);
        // WO-5：主号完成(1)，页面口径排除 → 不计
        SeedSummary(ctx, "WO-5", 1, totalWeight: 8000m, deliveryDate: DateTime.Today,
            estimatedProcessCompletionDate: DateTime.Today.AddDays(10));
        // WO-6：交期今天、预计完成 +5 天（延期）→ 预计完结桶1（今日+1~+7）
        SeedSummary(ctx, "WO-6", 3, totalWeight: 2000m, deliveryDate: DateTime.Today,
            estimatedProcessCompletionDate: DateTime.Today.AddDays(5));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        var row = result.Rows[4];
        row.Seq.Should().Be(5);
        row.Category.Should().Be("订单交期负荷");
        row.Section.Should().Be("订单延期量[预计完结]");
        row.CategoryNo.Should().Be(0);
        row.DateBucketTons.Count.Should().Be(7);
        // 桶1（交期截止-今日）：无预计完结日落在今日 → 0
        row.DateBucketTons[0].Should().Be(0m);
        // 桶2（今日+1~+7）：WO-6（+5 天）→ 2 吨
        row.DateBucketTons[1].Should().Be(2m);
        // 桶3（今日+8~+15）：WO-1（+10 天）→ 2 吨
        row.DateBucketTons[2].Should().Be(2m);
        // 桶4（今日+16~+30）：WO-2（+20 天）→ 3 吨
        row.DateBucketTons[3].Should().Be(3m);
        // 其余桶 0
        row.DateBucketTons[4].Should().Be(0m);
        row.DateBucketTons[5].Should().Be(0m);
        row.DateBucketTons[6].Should().Be(0m);
    }

    [Fact]
    public async Task GetOverviewAsync_延期量与预计完结_总量一致按不同维度分桶()
    {
        // 2026-08-19 统一延期判定为「预计完成日 > 交货日期」后，延期量行（按交货日期分桶）与预计完结行（按预计完成日分桶）
        // 统计的是同一批延期工单，逐桶分布不同但总量必须一致。
        // 旧口径「> 桶截止日」会漏计：①交期在桶中段、完成略晚未超桶截止的工单（WO-1）；②交期在远日量桶的工单（WO-3，EstComp>MaxValue 恒 false）。
        using var ctx = CreateDbContext();
        // WO-1：交期今天、预计完成 +3 天（延期，未超 1 周）→ 延期量桶1（今日）；预计完结桶2（今日+1~+7）
        SeedSummary(ctx, "WO-1", 3, totalWeight: 2000m, deliveryDate: DateTime.Today,
            estimatedProcessCompletionDate: DateTime.Today.AddDays(3));
        // WO-2：交期 +5 天、预计完成 +9 天（延期，未超 1 周）→ 延期量桶2（今日+1~+7）；预计完结桶3（今日+8~+15）
        SeedSummary(ctx, "WO-2", 3, totalWeight: 3000m, deliveryDate: DateTime.Today.AddDays(5),
            estimatedProcessCompletionDate: DateTime.Today.AddDays(9));
        // WO-4：交期 +10 天、预计完成 +25 天（延期，超 1 周）→ 延期量桶3（今日+8~+15）主值+副值；预计完结桶4（今日+16~+30）
        SeedSummary(ctx, "WO-4", 3, totalWeight: 4000m, deliveryDate: DateTime.Today.AddDays(10),
            estimatedProcessCompletionDate: DateTime.Today.AddDays(25));
        // WO-3：交期 +70 天（远日量桶）、预计完成 +75 天（延期）→ 延期量桶7（远日量，旧口径漏计）；预计完结桶7
        SeedSummary(ctx, "WO-3", 3, totalWeight: 5000m, deliveryDate: DateTime.Today.AddDays(70),
            estimatedProcessCompletionDate: DateTime.Today.AddDays(75));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        var delay = result.Rows[3];
        var estComplete = result.Rows[4];
        // 延期量行按交货日期所在桶（桶0=交期截止-今日/桶6=远日量）：桶0 WO-1=2、桶1 WO-2=3、桶2 WO-4=4、桶6 WO-3=5
        delay.DateBucketTons[0].Should().Be(2m);
        delay.DateBucketTons[1].Should().Be(3m);
        delay.DateBucketTons[2].Should().Be(4m);
        delay.DateBucketTons[3].Should().Be(0m);
        delay.DateBucketTons[4].Should().Be(0m);
        delay.DateBucketTons[5].Should().Be(0m);
        delay.DateBucketTons[6].Should().Be(5m);   // 远日量桶：WO-3 新口径计入（旧口径漏计）
        // 延期量副值（超 1 周）：仅 WO-4（延期 15 天）→ 桶2 = 4
        delay.DateBucketSubTons[0].Should().Be(0m);
        delay.DateBucketSubTons[1].Should().Be(0m);
        delay.DateBucketSubTons[2].Should().Be(4m);
        delay.DateBucketSubTons[3].Should().Be(0m);
        delay.DateBucketSubTons[4].Should().Be(0m);
        delay.DateBucketSubTons[5].Should().Be(0m);
        delay.DateBucketSubTons[6].Should().Be(0m);
        // 预计完结行按预计完成日所在桶：桶1 WO-1=2、桶2 WO-2=3、桶3 WO-4=4、桶6 WO-3=5
        estComplete.DateBucketTons[0].Should().Be(0m);
        estComplete.DateBucketTons[1].Should().Be(2m);
        estComplete.DateBucketTons[2].Should().Be(3m);
        estComplete.DateBucketTons[3].Should().Be(4m);
        estComplete.DateBucketTons[4].Should().Be(0m);
        estComplete.DateBucketTons[5].Should().Be(0m);
        estComplete.DateBucketTons[6].Should().Be(5m);
        // 总量一致（同一批延期工单）：2+3+4+5 = 14
        delay.DateBucketTons.Sum().Should().Be(estComplete.DateBucketTons.Sum());
        delay.DateBucketTons.Sum().Should().Be(14m);
    }

    [Fact]
    public async Task GetOverviewAsync_订单非延期_按桶区间预计完成日未超截止统计()
    {
        using var ctx = CreateDbContext();
        // WO-A：交期今天、预计完成今天 → 非延期桶1 = 5 吨
        SeedSummary(ctx, "WO-A", 3, totalWeight: 5000m, deliveryDate: DateTime.Today,
            estimatedProcessCompletionDate: DateTime.Today);
        // WO-B：交期 +5 天、预计完成 +3 天（提前）→ 非延期桶2（今日+7）= 3 吨
        SeedSummary(ctx, "WO-B", 3, totalWeight: 3000m, deliveryDate: DateTime.Today.AddDays(5),
            estimatedProcessCompletionDate: DateTime.Today.AddDays(3));
        // WO-C：交期 +20 天、预计完成 +18 天（提前完成）→ 交期在桶4区间 [今日+16,今日+30]，预计完成≤交货日期 → 非延期桶4 = 8 吨
        //（2026-08-19 统一延期判定为「预计完成日≤交货日期」非延期，与延期量行「>交货日期」严格互补；原「预计完成≤桶截止」口径下 WO-C 预计+25 也算非延期，新口径算延期）
        SeedSummary(ctx, "WO-C", 3, totalWeight: 8000m, deliveryDate: DateTime.Today.AddDays(20),
            estimatedProcessCompletionDate: DateTime.Today.AddDays(18));
        // WO-D：预计完成日 null → 不计
        SeedSummary(ctx, "WO-D", 3, totalWeight: 9000m, deliveryDate: DateTime.Today.AddDays(5));
        // WO-E：主号完成(1)，页面口径排除 → 不计
        SeedSummary(ctx, "WO-E", 1, totalWeight: 7000m, deliveryDate: DateTime.Today,
            estimatedProcessCompletionDate: DateTime.Today);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetOverviewAsync();

        var onTime = result.Rows[5];
        onTime.Seq.Should().Be(6);
        onTime.Category.Should().Be("订单交期负荷");
        onTime.Section.Should().Be("订单非延期");
        onTime.CategoryNo.Should().Be(0);
        onTime.DateBucketTons.Count.Should().Be(7);
        onTime.DateBucketTons[0].Should().Be(5m);
        onTime.DateBucketTons[1].Should().Be(3m);
        onTime.DateBucketTons[2].Should().Be(0m);
        onTime.DateBucketTons[3].Should().Be(8m);   // 桶4 今日+16~+30：WO-C
        onTime.DateBucketTons[4].Should().Be(0m);
        onTime.DateBucketTons[5].Should().Be(0m);
        onTime.DateBucketTons[6].Should().Be(0m);
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
        row.DateBucketTons[0].Should().Be(6m);
        row.DateBucketSubTons[0].Should().Be(2m);
        row.DateBucketTons[1].Should().Be(0m);
        row.DateBucketSubTons[1].Should().Be(0m);
    }
}
