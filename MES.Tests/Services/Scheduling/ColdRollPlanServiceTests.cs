using FluentAssertions;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Scheduling;
using MES.Tests.Tests;
using MES.Data.Entities.Batch;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Scheduling;

namespace MES.Tests.Services.Scheduling;

/// <summary>
/// 冷轧计划看板服务测试：按规格维度的生产批次时间桶重量分布聚合
/// </summary>
public class ColdRollPlanServiceTests : TestBase
{
    private ColdRollPlanService CreateService(AppDbContext ctx) => new(ctx, CreateProcessDefinitionServiceMock());

    private ProductionBatch CreateBatch(AppDbContext ctx, string batchNo, string workOrderNo,
        string processName, int seqNumber, bool isFinished,
        int weight = 1000,
        string? currentGroupName = null,
        string? currentSectionName = null,
        bool? currentSectionCompleted = null,
        BatchStatus status = BatchStatus.InProgress,
        string? spec = "219*8",
        string? sourceSpec = null)
    {
        var batch = new ProductionBatch
        {
            BatchNo = batchNo,
            Status = status,
            WorkOrderNo = workOrderNo,
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "业务员",
            DeliveryDate = DateTime.Today.AddMonths(1),
            MaterialName = "无缝管",
            SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163",
            DeliveryState = "SolutionAnnealedAndPickled",
            LengthStatus = "Fixed",
            ManufacturingItem = "OrderFinished",
            PlantGrade = "304",
            Specification = spec!,
            SourceSpecification = sourceSpec,
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = weight,
            TotalItemCount = 1,
            TechnicalRequirements = "NORMAL",
            CurrentValidWeight = weight,
            CurrentGroupName = currentGroupName,
            CurrentSectionName = currentSectionName,
            CurrentSectionCompleted = currentSectionCompleted,
            RowVersion = new byte[8],
            ProcessGroups = new List<ProcessGroup>
            {
                new()
                {
                    ProcessName = processName,
                    SequenceNumber = seqNumber,
                    ColdRollDraw = 1,
                    ManufacturingSpec = spec,
                }
            }
        };
        ctx.ProductionBatches.Add(batch);
        return batch;
    }

    private void SeedSummary(AppDbContext ctx, string workOrderNo,
        int scheduleStage = 1, string? urgencyLevel = null,
        string? productionFlowProperty = null,
        string? mainNoAttentionProcess = null)
    {
        int wid = Math.Abs(workOrderNo.GetHashCode());
        ctx.Set<WorkOrderExecutionSummary>().Add(new WorkOrderExecutionSummary
        {
            Id = wid,
            WorkOrderId = wid,
            WorkOrderNo = workOrderNo,
            Salesman = "业务员",
            CustomerName = "客户",
            SettlementMethod = "Theoretical",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            MaterialName = "无缝管",
            DeliveryState = "SolutionAnnealedAndPickled",
            PlantGrade = "304",
            Specification = "219*8",
            LengthStatus = "Fixed",
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = 2500m,
            ScheduleStage = scheduleStage,
            UrgencyLevel = urgencyLevel,
            ProductionFlowProperty = productionFlowProperty,
            MainNoAttentionProcess = mainNoAttentionProcess,
        });
    }

    /// <summary>排程设置：按 (ProcessType, BilletSpec, RollingSpec, IsFinished) 键记录，CompletionType/RollType 任一非 None 即视为已排程</summary>
    private void SeedSchedule(AppDbContext ctx, string processType, string billetSpec, string rollingSpec, bool isFinished,
        string? completionType = "All", string? rollType = "All")
    {
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = processType,
            BilletSpec = billetSpec,
            RollingSpec = rollingSpec,
            IsFinished = isFinished,
            CompletionType = string.IsNullOrEmpty(completionType) ? "None" : completionType,
            RollType = string.IsNullOrEmpty(rollType) ? "None" : rollType,
        });
    }

    // ==================== GetPlanAsync 测试 ====================

    [Fact]
    public async Task GetPlanAsync_冷轧批次聚合为正确时间桶()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: null, currentSectionName: null, weight: 2000);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPlanAsync(null);

        result.Should().HaveCount(1);
        var row = result[0];
        row.ProcessType.Should().Be(ProcessKeys.ColdRoll60);
        row.BilletSpec.Should().Be(""); // 无前序工序组
        row.RollingSpec.Should().Be("219*8");
        row.IsFinished.Should().BeTrue(); // 只有单个工序组，是最后一个
        row.BatchCount.Should().Be(1);
        // CurrentGroupName=null → diff=0 但不满足 isProducing → positionDiff=1 → 今日待轧
        row.WeightToday.Should().Be(2000m);
        row.WeightTotal.Should().Be(2000m);
    }

    [Fact]
    public async Task GetPlanAsync_非冷轧工序被忽略()
    {
        using var ctx = CreateDbContext();
        var batch = new ProductionBatch
        {
            BatchNo = "B001",
            Status = BatchStatus.InProgress,
            WorkOrderNo = "WO001",
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "业务员",
            DeliveryDate = DateTime.Today.AddMonths(1),
            MaterialName = "无缝管",
            SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163",
            DeliveryState = "SolutionAnnealedAndPickled",
            LengthStatus = "Fixed",
            ManufacturingItem = "OrderFinished",
            PlantGrade = "304",
            Specification = "219*8",
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = 1000m,
            TotalItemCount = 1,
            TechnicalRequirements = "NORMAL",
            CurrentValidWeight = 1000,
            RowVersion = new byte[8],
            ProcessGroups = new List<ProcessGroup>
            {
                new() { ProcessName = "酸洗", SequenceNumber = 1, Pickle = 1, ManufacturingSpec = "219*8" }
            }
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPlanAsync(null);

        // 酸洗不是冷轧/冷拔工序，应被忽略
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPlanAsync_近日在轧批次()
    {
        using var ctx = CreateDbContext();
        // 批次当前正在做 60冷轧 的 冷轧拔 且未完成
        var batch = CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false,
            weight: 3000);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPlanAsync(null);

        var row = result.Single();
        row.WeightProd.Should().Be(3000m);
        row.WeightToday.Should().Be(0);
    }

    [Fact]
    public async Task GetPlanAsync_工段筛选()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 1000);
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll50, 1, isFinished: false, weight: 2000);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPlanAsync("60冷轧");

        result.Should().HaveCount(1);
        result[0].ProcessType.Should().Be(ProcessKeys.ColdRoll60);
        result[0].WeightTotal.Should().Be(1000m);
    }

    [Fact]
    public async Task GetPlanAsync_空数据返回空列表()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var result = await svc.GetPlanAsync(null);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPlanAsync_已完成批次被排除()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false, status: BatchStatus.Completed);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPlanAsync(null);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPlanAsync_急件标记()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: null, currentSectionName: null, weight: 2000);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPlanAsync(null);

        var row = result.Single();
        row.WeightWaitNearUrgent.Should().Be(2000m);
    }

    /// <summary>
    /// 三组批次：荒管处理(1) → 三辊冷轧(2) → 附加成检(3)，规格 18*1.5
    /// </summary>
    private ProductionBatch CreateBatchMultiGroups(AppDbContext ctx, string batchNo, string workOrderNo,
        string? currentGroupName = null, string? currentSectionName = null, int weight = 1000)
    {
        var batch = new ProductionBatch
        {
            BatchNo = batchNo,
            Status = BatchStatus.InProgress,
            WorkOrderNo = workOrderNo,
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "业务员",
            DeliveryDate = DateTime.Today.AddMonths(1),
            MaterialName = "无缝管",
            SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163",
            DeliveryState = "SolutionAnnealedAndPickled",
            LengthStatus = "Fixed",
            ManufacturingItem = "OrderFinished",
            PlantGrade = "304",
            Specification = "18*1.5",
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = weight,
            TotalItemCount = 1,
            TechnicalRequirements = "NORMAL",
            CurrentValidWeight = weight,
            CurrentGroupName = currentGroupName,
            CurrentSectionName = currentSectionName,
            CurrentSectionCompleted = false,
            RowVersion = new byte[8],
            ProcessGroups = new List<ProcessGroup>
            {
                new() { ProcessName = ProcessKeys.RoughTubeProcessing, SequenceNumber = 1, Inspection = 1, ManufacturingSpec = "18*1.5" },
                new() { ProcessName = ProcessKeys.ThreeRollColdRoll, SequenceNumber = 2, ColdRollDraw = 1, ManufacturingSpec = "18*1.5" },
                new() { ProcessName = ProcessKeys.AdditionalFinalInspection, SequenceNumber = 3, Inspection = 1, ManufacturingSpec = "18*1.5" },
            }
        };
        ctx.ProductionBatches.Add(batch);
        return batch;
    }

    [Fact]
    public async Task GetPlanAsync_批次已过冷轧工序组_不显示待轧()
    {
        using var ctx = CreateDbContext();
        // 批次已轧完三辊冷轧，当前在后续的附加成检工序组 → 三辊冷轧不应再显示待轧
        CreateBatchMultiGroups(ctx, "B001", "WO001",
            currentGroupName: ProcessKeys.AdditionalFinalInspection,
            currentSectionName: SectionKeys.Inspection);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPlanAsync(null);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPlanAsync_批次在冷轧工序之前_显示今日待轧()
    {
        using var ctx = CreateDbContext();
        // 批次当前在荒管处理（三辊冷轧之前，工序组序号差=1），三辊冷轧应显示为待轧今日桶
        CreateBatchMultiGroups(ctx, "B001", "WO001",
            currentGroupName: ProcessKeys.RoughTubeProcessing,
            currentSectionName: SectionKeys.Inspection);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPlanAsync(null);

        var row = result.Single();
        row.ProcessType.Should().Be(ProcessKeys.ThreeRollColdRoll);
        row.WeightToday.Should().Be(1000m);
    }

    [Fact]
    public async Task GetPlanAsync_未投产批次_多冷轧组按工序组序号差分桶()
    {
        using var ctx = CreateDbContext();
        // 未投产批次（无当前工序组）→ 当前执行序号=0：三辊冷轧(seq=2) 序号差=2 → 明日桶
        CreateBatchMultiGroups(ctx, "B001", "WO001");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPlanAsync(null);

        var row = result.Single();
        row.ProcessType.Should().Be(ProcessKeys.ThreeRollColdRoll);
        row.WeightTomorrow.Should().Be(1000m);
        row.WeightToday.Should().Be(0);
    }

    // ==================== GetScheduleSummaryAsync 测试 ====================

    [Fact]
    public async Task GetScheduleSummaryAsync_在轧特急分档()
    {
        using var ctx = CreateDbContext();
        // 批次正在 60冷轧 做冷轧拔未完成 → 在轧(positionDiff=0)；A+急+正常+关注冷轧 → 特急
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false,
            weight: 3000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSummaryAsync(null, null);

        var row = result.Single();
        row.ProcessType.Should().Be(ProcessKeys.ColdRoll60);
        row.ProdTotalWeight.Should().Be(3000m);
        row.ProdUrgentWeight.Should().Be(3000m); // 特急 = 正常流转∧关注==当前冷轧
        row.ProdUrgentSubWeight.Should().Be(0m);
        row.ProdOtherWeight.Should().Be(0m);
        row.ProdRestWeight.Should().Be(0m);
        row.WaitTotalWeight.Should().Be(0m);
        row.TotalFlowWeight.Should().Be(3000m); // 总流转重量 = 在轧总量
        row.BatchCount.Should().Be(1);
    }

    [Fact]
    public async Task GetScheduleSummaryAsync_在轧特急负分档_非冷轧关注()
    {
        using var ctx = CreateDbContext();
        // 在轧 + A+急+正常+关注非冷轧(荒管处理) → 特急-（正常流转∧关注≠当前冷轧）
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false,
            weight: 3000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.RoughTubeProcessing);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSummaryAsync(null, null);

        var row = result.Single();
        row.ProdTotalWeight.Should().Be(3000m);
        row.ProdUrgentWeight.Should().Be(0m);
        row.ProdUrgentSubWeight.Should().Be(3000m); // 特急- = 正常流转∧关注≠当前冷轧
        row.ProdOtherWeight.Should().Be(0m);
        row.ProdRestWeight.Should().Be(0m);
    }

    [Fact]
    public async Task GetScheduleSummaryAsync_在轧余量分档_普通批次()
    {
        using var ctx = CreateDbContext();
        // 在轧 + 无紧急度 → 特急/特急-/急 均为 0，余量 = 在轧总量
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false,
            weight: 3000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSummaryAsync(null, null);

        var row = result.Single();
        row.ProdTotalWeight.Should().Be(3000m);
        row.ProdUrgentWeight.Should().Be(0m);
        row.ProdUrgentSubWeight.Should().Be(0m);
        row.ProdOtherWeight.Should().Be(0m);
        row.ProdRestWeight.Should().Be(3000m); // 余量 = 总量 − 特急 − 特急- − 急
    }

    [Fact]
    public async Task GetScheduleSummaryAsync_待轧特急与特急负分档()
    {
        using var ctx = CreateDbContext();
        // 未投产 → 今日待轧(positionDiff=1)
        // B001：A+急+正常+关注冷轧(60冷轧) → 待轧(特急)；B002：关注非冷轧(荒管) → 待轧(特急-)
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: null, currentSectionName: null, weight: 1000);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: null, currentSectionName: null, weight: 2000);
        SeedSummary(ctx, "WO002", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.RoughTubeProcessing);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSummaryAsync(null, null);

        var row = result.Single();
        row.WaitTotalWeight.Should().Be(3000m);
        row.WaitUrgentWeight.Should().Be(1000m);     // 待轧(特急) = 正常流转∧关注==当前冷轧
        row.WaitUrgentSubWeight.Should().Be(2000m);  // 待轧(特急-) = 正常流转∧关注≠当前冷轧
        row.WaitOtherWeight.Should().Be(0m);
        row.WaitRestWeight.Should().Be(0m);
        row.ProdTotalWeight.Should().Be(0m);
        row.BatchCount.Should().Be(2);
    }

    [Fact]
    public async Task GetScheduleSummaryAsync_maxDiff过滤待轧范围()
    {
        using var ctx = CreateDbContext();
        // 荒管处理(seq1) → 60冷轧(seq2) → 三辊冷轧(seq3) → 附加成检(seq4) → 50冷轧(seq5)
        // 批次当前在荒管处理做检验 → 当前执行工作序=1
        // 60冷轧 目标=2 → diff=1 → 今日；三辊 目标=3 → diff=2 → 明日；50冷轧 目标=5 → diff=4 → 延3
        var batch = new ProductionBatch
        {
            BatchNo = "B001",
            Status = BatchStatus.InProgress,
            WorkOrderNo = "WO001",
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "业务员",
            DeliveryDate = DateTime.Today.AddMonths(1),
            MaterialName = "无缝管",
            SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163",
            DeliveryState = "SolutionAnnealedAndPickled",
            LengthStatus = "Fixed",
            ManufacturingItem = "OrderFinished",
            PlantGrade = "304",
            Specification = "219*8",
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = 1000m,
            TotalItemCount = 1,
            TechnicalRequirements = "NORMAL",
            CurrentValidWeight = 1000,
            CurrentGroupName = ProcessKeys.RoughTubeProcessing,
            CurrentSectionName = "Inspection",
            CurrentSectionCompleted = false,
            RowVersion = new byte[8],
            ProcessGroups = new List<ProcessGroup>
            {
                new() { ProcessName = ProcessKeys.RoughTubeProcessing, SequenceNumber = 1, Inspection = 1, ManufacturingSpec = "219*8" },
                new() { ProcessName = ProcessKeys.ColdRoll60, SequenceNumber = 2, ColdRollDraw = 1, ManufacturingSpec = "219*8" },
                new() { ProcessName = ProcessKeys.ThreeRollColdRoll, SequenceNumber = 3, ColdRollDraw = 1, ManufacturingSpec = "219*8" },
                new() { ProcessName = ProcessKeys.AdditionalFinalInspection, SequenceNumber = 4, Inspection = 1, ManufacturingSpec = "219*8" },
                new() { ProcessName = ProcessKeys.ColdRoll50, SequenceNumber = 5, ColdRollDraw = 1, ManufacturingSpec = "219*8" },
            }
        };
        ctx.ProductionBatches.Add(batch);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "219*8", "219*8", isFinished: false);
        SeedSchedule(ctx, ProcessKeys.ThreeRollColdRoll, "219*8", "219*8", isFinished: false);
        SeedSchedule(ctx, ProcessKeys.ColdRoll50, "219*8", "219*8", isFinished: true);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);

        var all = await svc.GetScheduleSummaryAsync(null, null);
        all.Select(r => r.ProcessType).Should().HaveCount(3);
        all.Select(r => r.ProcessType).Should().Contain(ProcessKeys.ColdRoll60)
            .And.Contain(ProcessKeys.ThreeRollColdRoll)
            .And.Contain(ProcessKeys.ColdRoll50);

        var near2 = await svc.GetScheduleSummaryAsync(null, 2);
        near2.Select(r => r.ProcessType).Should().Contain(ProcessKeys.ColdRoll60)
            .And.Contain(ProcessKeys.ThreeRollColdRoll);
        near2.Select(r => r.ProcessType).Should().NotContain(ProcessKeys.ColdRoll50);
    }

    [Fact]
    public async Task GetScheduleSummaryAsync_未排程规格_不计入汇总()
    {
        using var ctx = CreateDbContext();
        // 批次在轧 60冷轧，但排程设置中无该规格记录 → 汇总不显示该行
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false,
            weight: 3000);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSummaryAsync(null, null);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetScheduleSummaryAsync_排程记录双None_不计入()
    {
        using var ctx = CreateDbContext();
        // 排程设置中存在该规格行但 CompletionType 与 RollType 均 None（明确不排程）→ 不计入汇总
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false,
            weight: 3000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true, completionType: "", rollType: "");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSummaryAsync(null, null);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetScheduleSummaryAsync_在轧要求特急严格档_只统计特急批次()
    {
        using var ctx = CreateDbContext();
        // 同规格两在轧批次：B001 特急(关注冷轧)、B002 特急-(关注荒管)
        // 在轧要求=CrOnly(特急严格档) → 仅 B001 计入，B002(特急-)被严格档过滤
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false,
            weight: 500);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false,
            weight: 300);
        SeedSummary(ctx, "WO002", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.RoughTubeProcessing);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true, completionType: "CrOnly", rollType: "");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSummaryAsync(null, null);

        var row = result.Single();
        row.ProdTotalWeight.Should().Be(500m);        // 仅特急批次
        row.ProdUrgentWeight.Should().Be(500m);
        row.ProdUrgentSubWeight.Should().Be(0m);      // 特急-批次被严格档过滤
        row.ProdOtherWeight.Should().Be(0m);
        row.ProdRestWeight.Should().Be(0m);
        row.TotalFlowWeight.Should().Be(500m);        // 总流转 = 实际执行的 500kg
        row.BatchCount.Should().Be(1);
    }

    [Fact]
    public async Task GetScheduleSummaryAsync_在轧要求特急急_统计特急与特急负()
    {
        using var ctx = CreateDbContext();
        // 同上两批次（特急500 + 特急-300），在轧要求=Partial2(特急-急档) → 两者都计入
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false,
            weight: 500);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false,
            weight: 300);
        SeedSummary(ctx, "WO002", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.RoughTubeProcessing);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true, completionType: "Partial2", rollType: "");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSummaryAsync(null, null);

        var row = result.Single();
        row.ProdTotalWeight.Should().Be(800m);
        row.ProdUrgentWeight.Should().Be(500m);       // 特急档
        row.ProdUrgentSubWeight.Should().Be(300m);    // 特急-档
        row.ProdOtherWeight.Should().Be(0m);
        row.ProdRestWeight.Should().Be(0m);
        row.BatchCount.Should().Be(2);
    }

    [Fact]
    public async Task GetScheduleSummaryAsync_在轧要求None_在轧批次不计入()
    {
        using var ctx = CreateDbContext();
        // 在轧批次，排程设置 CompletionType=None(在轧要求未排)、RollType=All(仅待轧侧排程)
        // → 在轧侧不统计，汇总为空
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false,
            weight: 3000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true, completionType: "", rollType: "All");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSummaryAsync(null, null);

        result.Should().BeEmpty();
    }
}
