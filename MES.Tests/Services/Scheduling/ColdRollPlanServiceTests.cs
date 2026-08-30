using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Constants;
using MES.Core.DTOs.Scheduling;
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
    private ColdRollPlanService CreateService(AppDbContext ctx)
    {
        SeedMachineGroupConfigs(ctx);
        return new(ctx, CreateProcessDefinitionServiceMock(), new MemoryCache(new MemoryCacheOptions()));
    }

    /// <summary>引擎测试辅助：标准 4 组种子 + 附加冷轧/冷拔工序 Key（模拟配置表新增工序）</summary>
    private ColdRollPlanService CreateServiceWithExtraProcessKeys(AppDbContext ctx, params string[] extraKeys)
    {
        SeedMachineGroupConfigs(ctx);
        return new(ctx, CreateProcessDefinitionServiceMock(extraKeys), new MemoryCache(new MemoryCacheOptions()));
    }

    /// <summary>
    /// 预置 4 机台组（与 DbInitializer 8d 段同构）：5060 供给目标 2030、2030/三辊/冷拔无供给目标。
    /// 引擎归组已配置表驱动（LoadMachineGroupsAsync），无种子则 GetMachineEstimateAsync 全过滤、GetScheduleSuggestionAsync 崩溃。
    /// 供需链由 SupplyTargetGroupKey 显式表达（方案 A，组角色字段已移除），5060 → 2030 单链回归种子。
    /// </summary>
    private static void SeedMachineGroupConfigs(AppDbContext ctx)
    {
        if (ctx.ColdRollMachineGroupConfigs.Any()) return;
        ctx.ColdRollMachineGroupConfigs.AddRange(
            new ColdRollMachineGroupConfig
            {
                GroupKey = ColdRollMachineGroupKeys.Roll5060,
                DisplayName = ColdRollMachineGroupKeys.Roll5060Display,
                ProcessKeys = $"{ProcessKeys.ColdRoll60},{ProcessKeys.ColdRoll50}",
                DisplayOrder = 1,
                SupplyTargetGroupKey = ColdRollMachineGroupKeys.Roll2030,
            },
            new ColdRollMachineGroupConfig
            {
                GroupKey = ColdRollMachineGroupKeys.Roll2030,
                DisplayName = ColdRollMachineGroupKeys.Roll2030Display,
                ProcessKeys = $"{ProcessKeys.ColdRoll20},{ProcessKeys.ColdRoll30}",
                DisplayOrder = 2,
            },
            new ColdRollMachineGroupConfig
            {
                GroupKey = ColdRollMachineGroupKeys.ThreeRoll,
                DisplayName = ColdRollMachineGroupKeys.ThreeRollDisplay,
                ProcessKeys = ProcessKeys.ThreeRollColdRoll,
                DisplayOrder = 3,
            },
            new ColdRollMachineGroupConfig
            {
                GroupKey = ColdRollMachineGroupKeys.Draw,
                DisplayName = ColdRollMachineGroupKeys.DrawDisplay,
                ProcessKeys = ProcessKeys.ColdDraw,
                DisplayOrder = 4,
            });
        ctx.SaveChanges();
    }

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
        string? completionType = "All", string? rollType = "All", decimal? dailyOutput = null)
    {
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = processType,
            BilletSpec = billetSpec,
            RollingSpec = rollingSpec,
            IsFinished = isFinished,
            CompletionType = string.IsNullOrEmpty(completionType) ? "None" : completionType,
            RollType = string.IsNullOrEmpty(rollType) ? "None" : rollType,
            DailyOutput = dailyOutput,
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

    [Fact]
    public async Task GetPlanAsync_排程档位命中_在轧待轧要求标记在档()
    {
        using var ctx = CreateDbContext();
        // 在轧急+批次（60冷轧）：正常流转、关注=当前冷轧，排程行 CrOnly → 在轧要求在档
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: true, weight: 3000,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.APlusUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true, completionType: "CrOnly", rollType: "CrOnly");

        // 待轧急+批次（三辊冷轧今日待轧 diff=1）：批次在荒管处理，排程行 CrOnly → 待轧要求在档
        CreateBatchMultiGroups(ctx, "B002", "WO002",
            currentGroupName: ProcessKeys.RoughTubeProcessing,
            currentSectionName: SectionKeys.Inspection);
        SeedSummary(ctx, "WO002", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.APlusUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ThreeRollColdRoll);
        SeedSchedule(ctx, ProcessKeys.ThreeRollColdRoll, "18*1.5", "18*1.5", isFinished: false, completionType: "CrOnly", rollType: "CrOnly");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPlanAsync(null);

        result.Should().HaveCount(2);
        var prodRow = result.Single(x => x.ProcessType == ProcessKeys.ColdRoll60);
        prodRow.ProdTierMatched.Should().BeTrue();   // 在轧急+ 命中 CrOnly
        prodRow.WaitTierMatched.Should().BeFalse();  // 该行无待轧批次
        var waitRow = result.Single(x => x.ProcessType == ProcessKeys.ThreeRollColdRoll);
        waitRow.WaitTierMatched.Should().BeTrue();   // 待轧急+ 命中 CrOnly
        waitRow.ProdTierMatched.Should().BeFalse();  // 该行无在轧批次
    }

    [Fact]
    public async Task GetPlanAsync_排程行档位None_在轧要求不标记()
    {
        using var ctx = CreateDbContext();
        // 在轧批次存在但排程行档位为 None（无排程计划）→ 「在轧要求」留空
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: true, weight: 3000,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.APlusUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true, completionType: "None", rollType: "None");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPlanAsync(null);

        var row = result.Single();
        row.ProdTierMatched.Should().BeFalse();   // 排程行档位 None → 不标记
        row.WaitTierMatched.Should().BeFalse();
    }

    [Fact]
    public async Task GetPlanAsync_有待流转量但批次不命中档位_待轧要求不标记()
    {
        using var ctx = CreateDbContext();
        // 用户场景：某规格存在「待流转的量」，但未设入本次「流转计划」——即待轧批次存在、排程行档位也有
        // （如「急+/急」），但批次属性不命中该档位（如 B顺 非急）→ 该规格不在本次排程建议内，「待轧要求」留空
        CreateBatchMultiGroups(ctx, "B001", "WO001",
            currentGroupName: ProcessKeys.RoughTubeProcessing, // 三辊冷轧之前 → 今日待轧 diff=1
            currentSectionName: SectionKeys.Inspection);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.BOrder,
            productionFlowProperty: ProductionFlowKeys.Normal);
        SeedSchedule(ctx, ProcessKeys.ThreeRollColdRoll, "18*1.5", "18*1.5", isFinished: false,
            completionType: "Urgent", rollType: "Urgent");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPlanAsync(null);

        var row = result.Single(x => x.ProcessType == ProcessKeys.ThreeRollColdRoll);
        row.WaitTierMatched.Should().BeFalse();   // 有待流转量+有档位，但批次(B顺)不命中「急+/急」→ 不在本次流转计划
        row.ProdTierMatched.Should().BeFalse();   // 该行无在轧批次
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

    // ==================== GetMachineEstimateAsync 测试 ====================

    [Fact]
    public async Task GetMachineEstimateAsync_按轧机类型归并_在制与成品拆分()
    {
        using var ctx = CreateDbContext();
        // 同一批次含 60冷轧(seq2,非最后=在制) + 50冷轧(seq5,最后=成品)，两工序组均归入「冷轧5060」
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
                new() { ProcessName = ProcessKeys.ColdRoll60, SequenceNumber = 2, ColdRollDraw = 1, ManufacturingSpec = "219*8" },
                new() { ProcessName = ProcessKeys.ColdRoll50, SequenceNumber = 5, ColdRollDraw = 1, ManufacturingSpec = "219*8" },
            }
        };
        ctx.ProductionBatches.Add(batch);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: false);
        SeedSchedule(ctx, ProcessKeys.ColdRoll50, "219*8", "219*8", isFinished: true);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetMachineEstimateAsync();

        result.Should().HaveCount(4); // 固定返回 4 行
        var row = result.Single(r => r.MachineType == "冷轧5060");
        row.FlowTotalWeight.Should().Be(2000m);   // 60在制 + 50成品 归并
        row.FinishedWeight.Should().Be(1000m);    // 50冷轧=最后工序组=成品
        row.InProcessWeight.Should().Be(1000m);   // 60冷轧=中间工序组=在制
    }

    [Fact]
    public async Task GetMachineEstimateAsync_机台需求_每日台数四舍五入()
    {
        using var ctx = CreateDbContext();
        // 15000kg / (5000×6) = 0.5；3000kg / (5000×6) = 0.1；合计 0.6 → AwayFromZero 四舍五入 = 1
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 15000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true, dailyOutput: 5000m);
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll50, 1, isFinished: false, weight: 3000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll50, "", "219*8", isFinished: true, dailyOutput: 5000m);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetMachineEstimateAsync();

        var row = result.Single(r => r.MachineType == "冷轧5060");
        row.MachineCount.Should().Be(1);
    }

    [Fact]
    public async Task GetMachineEstimateAsync_未排程规格不计入()
    {
        using var ctx = CreateDbContext();
        // 批次无排程设置记录 → 各轧机类型行均为 0
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 3000);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetMachineEstimateAsync();

        result.Should().HaveCount(4);
        result.Should().OnlyContain(r => r.FlowTotalWeight == 0m && r.FinishedWeight == 0m
            && r.InProcessWeight == 0m && r.MachineCount == 0);
    }

    [Fact]
    public async Task GetMachineEstimateAsync_DailyOutput为空_机台数不计入该量()
    {
        using var ctx = CreateDbContext();
        // 有排程记录但单机单日量为空 → 流转量计入，但机台需求不计入该规格
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 12000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetMachineEstimateAsync();

        var row = result.Single(r => r.MachineType == "冷轧5060");
        row.FlowTotalWeight.Should().Be(12000m);
        row.MachineCount.Should().Be(0);
    }

    [Fact]
    public async Task GetMachineEstimateAsync_参数表单机单日量优先于排程小表()
    {
        using var ctx = CreateDbContext();
        // 小表 4000，参数表 12000；批次 48000kg → 参数表:48000/12000/6=0.67→1；若误读小表:48000/4000/6=2→2
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 48000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true, dailyOutput: 4000m);
        ctx.ColdRollCapacities.Add(new ColdRollCapacity
        {
            ProcessType = ProcessKeys.ColdRoll60,
            BilletSpec = "",
            RollingSpec = "219*8",
            IsFinished = true,
            MachineNo = "60-1#",
            DailyOutput = 12000m,
            SampleCount = 1,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetMachineEstimateAsync();

        var row = result.Single(r => r.MachineType == "冷轧5060");
        row.FlowTotalWeight.Should().Be(48000m);
        row.MachineCount.Should().Be(1);
    }

    [Fact]
    public async Task GetMachineEstimateAsync_参数表缺该维度_回退排程小表()
    {
        using var ctx = CreateDbContext();
        // 参数表无该规格 → 用小表 5000；批次 60000 → 60000/5000/6=2 → 2
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 60000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true, dailyOutput: 5000m);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetMachineEstimateAsync();

        var row = result.Single(r => r.MachineType == "冷轧5060");
        row.FlowTotalWeight.Should().Be(60000m);
        row.MachineCount.Should().Be(2);
    }

    [Fact]
    public async Task GetMachineEstimateAsync_参数表日产能为空_回退排程小表()
    {
        using var ctx = CreateDbContext();
        // 参数表有该维度但单机单日量为空 → 回退小表 5000；批次 30000 → 30000/5000/6=1 → 1
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 30000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true, dailyOutput: 5000m);
        ctx.ColdRollCapacities.Add(new ColdRollCapacity
        {
            ProcessType = ProcessKeys.ColdRoll60,
            BilletSpec = "",
            RollingSpec = "219*8",
            IsFinished = true,
            DailyOutput = null,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetMachineEstimateAsync();

        var row = result.Single(r => r.MachineType == "冷轧5060");
        row.FlowTotalWeight.Should().Be(30000m);
        row.MachineCount.Should().Be(1);
    }

    [Fact]
    public async Task GetMachineEstimateAsync_5060拆档_在制成品各自四舍五入再相加()
    {
        using var ctx = CreateDbContext();
        // 与排程建议同口径：5060 组在制/成品分档各自取整再相加（非整组一次取整）
        // 在制 156000/(10000×6)=2.6 → Round=3；成品 78000/(5000×6)=2.6 → Round=3 → 3+3=6
        // （整组一次取整 Round(2.6+2.6)=Round(5.2)=5，两处会不一致——本次修正为拆档）
        CreateBatch60Chain(ctx, "B001", "WO001", "219*8", ProcessKeys.ColdRoll30, "180*8", weight: 156000);
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 78000, spec: "273*8");
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: false, dailyOutput: 10000m);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "273*8", isFinished: true, dailyOutput: 5000m);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetMachineEstimateAsync();

        var row = result.Single(r => r.MachineType == "冷轧5060");
        row.FlowTotalWeight.Should().Be(234000m);
        row.InProcessWeight.Should().Be(156000m);
        row.FinishedWeight.Should().Be(78000m);
        row.MachineCount.Should().Be(6);
    }

    // ==================== GetScheduleSuggestionAsync 测试 ====================

    /// <summary>机台数配置：按单冷轧类型（排程建议产能平衡输入）</summary>
    private void SeedMachineConfig(AppDbContext ctx, string processType,
        int ownedCount, int minMachines, int maxMachines, decimal? estimatedDailyOutput = null)
    {
        ctx.ColdRollMachineConfigs.Add(new ColdRollMachineConfig
        {
            ProcessType = processType,
            OwnedCount = ownedCount,
            MinMachines = minMachines,
            MaxMachines = maxMachines,
            EstimatedDailyOutput = estimatedDailyOutput,
        });
    }

    /// <summary>5060 → 下游链批次：60冷轧(在制) → 下一冷轧/冷拔工序组，用于方式B流转折算</summary>
    private ProductionBatch CreateBatch60Chain(AppDbContext ctx, string batchNo, string workOrderNo,
        string firstSpec, string nextProcess, string nextSpec, int weight)
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
            Specification = firstSpec,
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = weight,
            TotalItemCount = 1,
            TechnicalRequirements = "NORMAL",
            CurrentValidWeight = weight,
            CurrentGroupName = ProcessKeys.ColdRoll60,
            CurrentSectionName = SectionKeys.ColdRollDraw,
            CurrentSectionCompleted = false,
            RowVersion = new byte[8],
            ProcessGroups = new List<ProcessGroup>
            {
                new() { ProcessName = ProcessKeys.ColdRoll60, SequenceNumber = 1, ColdRollDraw = 1, ManufacturingSpec = firstSpec },
                new() { ProcessName = nextProcess, SequenceNumber = 2, ColdRollDraw = 1, ManufacturingSpec = nextSpec },
            }
        };
        ctx.ProductionBatches.Add(batch);
        return batch;
    }

    /// <summary>通用冷轧链批次：当前工序组在制 → 下一冷轧/冷拔工序组（多链/多级链流转折算用）</summary>
    private ProductionBatch CreateBatchChain(AppDbContext ctx, string batchNo, string workOrderNo, string currentProcess,
        string firstSpec, string nextProcess, string nextSpec, int weight)
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
            Specification = firstSpec,
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = weight,
            TotalItemCount = 1,
            TechnicalRequirements = "NORMAL",
            CurrentValidWeight = weight,
            CurrentGroupName = currentProcess,
            CurrentSectionName = SectionKeys.ColdRollDraw,
            CurrentSectionCompleted = false,
            RowVersion = new byte[8],
            ProcessGroups = new List<ProcessGroup>
            {
                new() { ProcessName = currentProcess, SequenceNumber = 1, ColdRollDraw = 1, ManufacturingSpec = firstSpec },
                new() { ProcessName = nextProcess, SequenceNumber = 2, ColdRollDraw = 1, ManufacturingSpec = nextSpec },
            }
        };
        ctx.ProductionBatches.Add(batch);
        return batch;
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_特急锁定_无配置默认Partial2且急加锁定()
    {
        using var ctx = CreateDbContext();
        // B001：急+批次（A急/正常/关注=60冷轧）待轧 spec 219*8 → 无配置默认「急+/急/急-」，急+行标记锁定
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 3000);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        // B002：普通批次 spec 273*8 → 无急+不标记锁定
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 2000, spec: "273*8");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var group = result.Single(g => g.MachineType == "冷轧5060");
        group.HasUrgentPlus.Should().BeTrue();
        group.Status.Should().Be("OK");
        group.SuggestedTier.Should().Be("急+/急/急-"); // 无配置无机台数区间 → 保持默认原始档
        // 组「本次计划流转量」= 明细行计划量之和（锁定行待轧 3000 + 新增行 0，建议档位口径）
        group.PlannedFlowWeight.Should().Be(3000m);

        var urgentItem = group.Items.Single(i => i.RollingSpec == "219*8");
        urgentItem.HasUrgentPlus.Should().BeTrue();
        urgentItem.SuggestedCompletionType.Should().Be("Partial2");
        urgentItem.SuggestedRollType.Should().Be("Partial2");
        urgentItem.RowStatus.Should().Be("锁定");
        urgentItem.InProdExists.Should().BeFalse(); // 无在轧批次，在制档位仍必须设
        // 锁定行：实际流转档两侧均按建议填入（即使在轧侧计划量 0，锁定优先两侧非空）
        urgentItem.PlannedInProdWeight.Should().Be(0);
        urgentItem.PlannedInWaitWeight.Should().Be(3000m); // 待轧急+ 命中 Partial2
        urgentItem.ActualCompletionTier.Should().Be("Partial2");
        urgentItem.ActualRollTier.Should().Be("Partial2");

        var normalItem = group.Items.Single(i => i.RollingSpec == "273*8");
        normalItem.HasUrgentPlus.Should().BeFalse();
        normalItem.SuggestedCompletionType.Should().Be("Partial2");
        normalItem.SuggestedRollType.Should().Be("Partial2");
        normalItem.RowStatus.Should().Be("新增");
        // 非锁定新增行：批次不命中档位 → 计划量 0 → 实际档留空（不在本次流转计划，不写入排程设置）
        normalItem.PlannedInWaitWeight.Should().Be(0);
        normalItem.ActualCompletionTier.Should().Be("");
        normalItem.ActualRollTier.Should().Be("");
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_实际流转档_计划量为0侧留空_有量侧填建议()
    {
        using var ctx = CreateDbContext();
        // 有排程行（现有行）无急+：产能平衡放宽到 All（min1 max3），全部批次命中 → 建议 All/All
        // B001 在轧（spec 219*8）、B002 待轧（spec 273*8），均非急非锁定 → 现有 OK 行
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 30000,
            currentGroupName: ProcessKeys.ColdRoll60, currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false);
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 6000, spec: "273*8");
        // 单工序组批次 allocation IsFinished=true；产能档案 5000/日（机台数计算依赖排程日产量）
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true, dailyOutput: 5000m);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "273*8", isFinished: true, dailyOutput: 5000m);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll60, ownedCount: 2, minMachines: 1, maxMachines: 3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var group = result.Single(g => g.MachineType == "冷轧5060");
        group.SuggestedTier.Should().Be("全量");

        // 在轧行：计划在轧量=30000（命中 All）→ 实际在轧流转档=All；无待轧批次 → 实际待轧留空
        var inProdRow = group.Items.Single(i => i.RollingSpec == "219*8");
        inProdRow.RowStatus.Should().Be("OK");
        inProdRow.PlannedInProdWeight.Should().Be(30000m);
        inProdRow.PlannedInWaitWeight.Should().Be(0);
        inProdRow.ActualCompletionTier.Should().Be("All");
        inProdRow.ActualRollTier.Should().Be("");

        // 待轧行：计划待轧量=6000（命中 All）→ 实际待轧流转档=All；无在轧批次 → 实际在轧留空
        var waitRow = group.Items.Single(i => i.RollingSpec == "273*8");
        waitRow.RowStatus.Should().Be("OK");
        waitRow.PlannedInProdWeight.Should().Be(0);
        waitRow.PlannedInWaitWeight.Should().Be(6000m);
        waitRow.ActualCompletionTier.Should().Be("");
        waitRow.ActualRollTier.Should().Be("All");

        // 组「本次计划流转量」= 明细行计划量之和（建议档位口径，与组头显示一致）
        group.PlannedFlowWeight.Should().Be(30000m + 6000m);
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_自动分配_不考虑人工已设档位()
    {
        using var ctx = CreateDbContext();
        // B001：急+ spec 219*8，现有排程 None/None → 无配置默认档 Partial2（覆盖）
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 3000);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true, completionType: "", rollType: "");
        // B002：急+ spec 273*8，现有排程 Urgent/Urgent → 自动分配覆盖为 Partial2（不考虑人工已设档位）
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 3000, spec: "273*8");
        SeedSummary(ctx, "WO002", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "273*8", isFinished: true, completionType: "Urgent", rollType: "Urgent");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var group = result.Single(g => g.MachineType == "冷轧5060");
        group.Items.Single(i => i.RollingSpec == "219*8")
            .Should().Match<ColdRollScheduleSuggestionItemDto>(i =>
                i.SuggestedCompletionType == "Partial2" && i.SuggestedRollType == "Partial2" && i.RowStatus == "锁定");
        group.Items.Single(i => i.RollingSpec == "273*8")
            .Should().Match<ColdRollScheduleSuggestionItemDto>(i =>
                i.SuggestedCompletionType == "Partial2" && i.SuggestedRollType == "Partial2");
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_产能平衡_逐档放宽至满足最小机台数()
    {
        using var ctx = CreateDbContext();
        // 冷轧30 min=3 max=4；三批各 30000/(5000×6)=1 台
        // B001 急(正常∧关注≠30) → 命中 Urgent 起；B002 急-(非正常) → 命中 Partial2 起；B003 普通 → 仅 All
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll30, 1, isFinished: false, weight: 30000);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll30, 1, isFinished: false, weight: 30000, spec: "273*8");
        SeedSummary(ctx, "WO002", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent);
        CreateBatch(ctx, "B003", "WO003", ProcessKeys.ColdRoll30, 1, isFinished: false, weight: 30000, spec: "325*8");
        SeedSchedule(ctx, ProcessKeys.ColdRoll30, "", "219*8", isFinished: true, completionType: "CrOnly", rollType: "CrOnly", dailyOutput: 5000m);
        SeedSchedule(ctx, ProcessKeys.ColdRoll30, "", "273*8", isFinished: true, completionType: "CrOnly", rollType: "CrOnly", dailyOutput: 5000m);
        SeedSchedule(ctx, ProcessKeys.ColdRoll30, "", "325*8", isFinished: true, completionType: "CrOnly", rollType: "CrOnly", dailyOutput: 5000m);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll30, ownedCount: 3, minMachines: 3, maxMachines: 4);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var group = result.Single(g => g.MachineType == "冷轧2030");
        group.CurrentTier.Should().Be("急+");
        group.SuggestedTier.Should().Be("全量");
        group.TierChanged.Should().BeTrue();
        group.MachineCount.Should().Be(3);
        group.Status.Should().Be("OK");
        group.Items.Should().OnlyContain(i => i.SuggestedCompletionType == "All" && i.SuggestedRollType == "All");
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_现状达标_保持现档()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll60, currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false, weight: 30000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true, dailyOutput: 5000m);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll60, ownedCount: 2, minMachines: 1, maxMachines: 3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var group = result.Single(g => g.MachineType == "冷轧5060");
        // 建议档=现状档（All/全量），始终显示档位名（不显示「保持」，语义即建议值）
        group.SuggestedTier.Should().Be("全量");
        group.TierChanged.Should().BeFalse();
        group.MachineCount.Should().Be(1); // 30000/(5000×6)=1 ∈ [1,3]
        group.Status.Should().Be("OK");
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_覆盖关系_机台需求按组聚合()
    {
        using var ctx = CreateDbContext();
        // 60/50/30/20 各一在轧 30000，配置各 min1 max3
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll60, currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false, weight: 30000);
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll50, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll50, currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false, weight: 30000, spec: "273*8");
        CreateBatch(ctx, "B003", "WO003", ProcessKeys.ColdRoll30, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll30, currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false, weight: 30000, spec: "180*8");
        CreateBatch(ctx, "B004", "WO004", ProcessKeys.ColdRoll20, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll20, currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false, weight: 30000, spec: "356*8");
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true, dailyOutput: 5000m);
        SeedSchedule(ctx, ProcessKeys.ColdRoll50, "", "273*8", isFinished: true, dailyOutput: 5000m);
        SeedSchedule(ctx, ProcessKeys.ColdRoll30, "", "180*8", isFinished: true, dailyOutput: 5000m);
        SeedSchedule(ctx, ProcessKeys.ColdRoll20, "", "356*8", isFinished: true, dailyOutput: 5000m);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll60, ownedCount: 2, minMachines: 1, maxMachines: 3);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll50, ownedCount: 2, minMachines: 1, maxMachines: 3);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll30, ownedCount: 2, minMachines: 1, maxMachines: 3);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll20, ownedCount: 2, minMachines: 1, maxMachines: 3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var group5060 = result.Single(g => g.MachineType == "冷轧5060");
        group5060.MinMachines.Should().Be(2); // 50+60
        group5060.MaxMachines.Should().Be(6);
        group5060.MachineCount.Should().Be(2);
        group5060.Items.Should().HaveCount(2);
        group5060.MemberProcessTypes.Should().Contain(ProcessKeys.ColdRoll50).And.Contain(ProcessKeys.ColdRoll60);

        var group2030 = result.Single(g => g.MachineType == "冷轧2030");
        group2030.MinMachines.Should().Be(2); // 20+30
        group2030.Items.Should().HaveCount(2);
        group2030.MachineCount.Should().Be(2);
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_流转保底_方式B折算2030机台需求()
    {
        using var ctx = CreateDbContext();
        // B001：60在制 → 下游 30(180*8)，方式B 产能档案按规格反算 → 90000/(5000×6)=3 台
        CreateBatch60Chain(ctx, "B001", "WO001", "219*8", ProcessKeys.ColdRoll30, "180*8", weight: 90000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: false); // 60在制行排程（All 命中）→ 有流转要求才计入 flowDemand
        ctx.ColdRollCapacities.Add(new ColdRollCapacity
        {
            ProcessType = ProcessKeys.ColdRoll30,
            BilletSpec = "219*8",
            RollingSpec = "180*8",
            IsFinished = true,
            DailyOutput = 5000m,
        });
        // B002：2030 本组在制 60000/(5000×6)=2 台
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll30, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll30, currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false, weight: 60000, spec: "180*8");
        SeedSchedule(ctx, ProcessKeys.ColdRoll30, "", "180*8", isFinished: true, dailyOutput: 5000m);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll30, ownedCount: 3, minMachines: 1, maxMachines: 3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var demander = result.Single(g => g.MachineType == "冷轧2030");
        demander.FlowState.Should().NotBeNull();
        demander.FlowState!.Role.Should().Be("Demander");
        demander.FlowState.SupplyMachines.Should().Be(3); // 方式B：90000/(5000×6)=3
        demander.FlowState.NeedMachines.Should().Be(1);
        demander.FlowState.Balanced.Should().BeTrue();
        // target=max(1,3)=3 > 现状 2 台 → 矛盾A（需求抬升至流转保底量）
        demander.Status.Should().Be("A");
        demander.Conflicts.Should().Contain(c => c.Contains("全量排程仍不足机台需求"));
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_流转保底_方式B优先方式A兜底()
    {
        using var ctx = CreateDbContext();
        // B001：60→30(180*8)，产能档案有 → 方式B 60000/(10000×6)=1
        CreateBatch60Chain(ctx, "B001", "WO001", "219*8", ProcessKeys.ColdRoll30, "180*8", weight: 60000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: false); // 60在制行排程（All 命中）→ 有流转要求才计入 flowDemand
        ctx.ColdRollCapacities.Add(new ColdRollCapacity
        {
            ProcessType = ProcessKeys.ColdRoll30,
            BilletSpec = "219*8",
            RollingSpec = "180*8",
            IsFinished = true,
            DailyOutput = 10000m,
        });
        // B002：60→20(273*8)，产能档案无 + 机台配置估算日产空 → 0
        CreateBatch60Chain(ctx, "B002", "WO002", "273*8", ProcessKeys.ColdRoll20, "325*8", weight: 60000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "273*8", isFinished: false); // 60在制行排程（All 命中）
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll20, ownedCount: 2, minMachines: 1, maxMachines: 2);
        // B003：60→30(356*8)，产能档案无 + 机台配置估算日产 5000 → 方式A 60000/(5000×6)=2
        CreateBatch60Chain(ctx, "B003", "WO003", "219*8", ProcessKeys.ColdRoll30, "356*8", weight: 60000);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll30, ownedCount: 3, minMachines: 1, maxMachines: 3, estimatedDailyOutput: 5000m);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var demander = result.Single(g => g.MachineType == "冷轧2030");
        demander.FlowState.Should().NotBeNull();
        demander.FlowState!.SupplyMachines.Should().Be(3); // 方式B 1 + 方式A 2
        demander.FlowState.NeedMachines.Should().Be(2);    // 30 min1 + 20 min1
        demander.FlowState.Balanced.Should().BeTrue();
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_流转保底_仅5060在制下游2030折算()
    {
        using var ctx = CreateDbContext();
        // 60在制 → 下游三辊（非 2030）→ 不计入 2030 流转折算
        CreateBatch60Chain(ctx, "B001", "WO001", "219*8", ProcessKeys.ThreeRollColdRoll, "180*8", weight: 60000);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll30, ownedCount: 3, minMachines: 1, maxMachines: 3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var supplier = result.Single(g => g.MachineType == "冷轧5060");
        supplier.FlowState.Should().NotBeNull();
        supplier.FlowState!.Role.Should().Be("Supplier");
        supplier.FlowState.SupplyMachines.Should().Be(0); // 三辊不计入

        var demander = result.Single(g => g.MachineType == "冷轧2030");
        demander.FlowState.Should().NotBeNull();
        demander.FlowState!.SupplyMachines.Should().Be(0);
        demander.FlowState.NeedMachines.Should().Be(1);
        demander.FlowState.Balanced.Should().BeFalse();

        result.Single(g => g.MachineType == "冷轧三辊").FlowState.Should().BeNull();
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_矛盾A_全量仍不足最小机台数()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll60, currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false, weight: 30000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true, dailyOutput: 5000m);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll60, ownedCount: 3, minMachines: 3, maxMachines: 5);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var group = result.Single(g => g.MachineType == "冷轧5060");
        group.Status.Should().Be("A");
        group.Conflicts.Should().Contain(c => c.Contains("全量排程仍不足机台需求"));
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_矛盾A_急加锁定超最大机台数()
    {
        using var ctx = CreateDbContext();
        // 两急+批次在轧，各 60000/(5000×6)=2 台 → CrOnly=4 台 > 最大 1 台
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll60, currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false, weight: 60000);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll60, currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false, weight: 60000, spec: "273*8");
        SeedSummary(ctx, "WO002", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true, dailyOutput: 5000m);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "273*8", isFinished: true, dailyOutput: 5000m);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll60, ownedCount: 1, minMachines: 1, maxMachines: 1);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var group = result.Single(g => g.MachineType == "冷轧5060");
        group.Status.Should().Be("A'");
        group.Conflicts.Should().Contain(c => c.Contains("急+锁定已超最大机台数"));
        group.HasUrgentPlus.Should().BeTrue();
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_矛盾B_可叠加于A()
    {
        using var ctx = CreateDbContext();
        // B001：60→30 在制，方式B → 30000/(5000×6)=1 台 2030 流转需求
        CreateBatch60Chain(ctx, "B001", "WO001", "219*8", ProcessKeys.ColdRoll30, "180*8", weight: 30000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: false); // 60在制行排程（All 命中）→ 有流转要求才计入 flowDemand
        ctx.ColdRollCapacities.Add(new ColdRollCapacity
        {
            ProcessType = ProcessKeys.ColdRoll30,
            BilletSpec = "219*8",
            RollingSpec = "180*8",
            IsFinished = true,
            DailyOutput = 5000m,
        });
        // B002：2030 本组在制 30000/(5000×6)=1 台 → 现状 1 < 最小 3 → 矛盾A；流转 1 < 最小 3 → 叠加矛盾B
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll30, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll30, currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false, weight: 30000, spec: "180*8");
        SeedSchedule(ctx, ProcessKeys.ColdRoll30, "", "180*8", isFinished: true, dailyOutput: 5000m);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll30, ownedCount: 3, minMachines: 3, maxMachines: 5);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var group = result.Single(g => g.MachineType == "冷轧2030");
        group.Status.Should().Be("A,B");
        group.Conflicts.Should().Contain(c => c.Contains("全量排程仍不足机台需求"));
        group.Conflicts.Should().Contain(c => c.Contains("2030 下次承接流转 1 台"));
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_四维合并_现有行保批新增行提()
    {
        using var ctx = CreateDbContext();
        // 现有行 A：219*8 无批次（suggested=existing 原样保留）
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true, dailyOutput: 5000m);
        // 现有行 C + 批次：325*8
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "325*8", isFinished: true, dailyOutput: 5000m);
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll60, currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false, weight: 30000, spec: "325*8");
        // 新增行：273*8 有批次无排程
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 6000, spec: "273*8");
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll60, ownedCount: 2, minMachines: 1, maxMachines: 3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var group = result.Single(g => g.MachineType == "冷轧5060");
        group.Items.Should().HaveCount(3);

        var row219 = group.Items.Single(i => i.RollingSpec == "219*8");
        row219.RowStatus.Should().Be("OK");
        row219.SuggestedCompletionType.Should().Be("All");
        row219.SuggestedRollType.Should().Be("All");
        row219.InProdExists.Should().BeFalse();

        var row325 = group.Items.Single(i => i.RollingSpec == "325*8");
        row325.RowStatus.Should().Be("OK");

        var row273 = group.Items.Single(i => i.RollingSpec == "273*8");
        row273.RowStatus.Should().Be("新增");
        row273.SuggestedCompletionType.Should().Be("All"); // 组目标档位 All 叠加到新增行
        row273.SuggestedRollType.Should().Be("All");
        row273.InWaitExists.Should().BeTrue();
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_2030过度_收窄至急加急档()
    {
        using var ctx = CreateDbContext();
        // B001 急+（正常∧关注=30）、B002/B003 急-（非正常）各在轧 30000
        // cPartial2=3 > max2 → 向窄收 Urgent：仅急+命中 → 1 ∈ [1,2] → 「急+/急」
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll30, 1, isFinished: false, weight: 30000);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll30);
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll30, 1, isFinished: false, weight: 30000, spec: "273*8");
        SeedSummary(ctx, "WO002", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent);
        CreateBatch(ctx, "B003", "WO003", ProcessKeys.ColdRoll30, 1, isFinished: false, weight: 30000, spec: "325*8");
        SeedSummary(ctx, "WO003", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent);
        SeedSchedule(ctx, ProcessKeys.ColdRoll30, "", "219*8", isFinished: true, dailyOutput: 5000m);
        SeedSchedule(ctx, ProcessKeys.ColdRoll30, "", "273*8", isFinished: true, dailyOutput: 5000m);
        SeedSchedule(ctx, ProcessKeys.ColdRoll30, "", "325*8", isFinished: true, dailyOutput: 5000m);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll30, ownedCount: 2, minMachines: 1, maxMachines: 2);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var group = result.Single(g => g.MachineType == "冷轧2030");
        group.SuggestedTier.Should().Be("急+/急");
        group.TierChanged.Should().BeTrue();
        group.MachineCount.Should().Be(1);
        group.Status.Should().Be("OK");
        group.Items.Single(i => i.RollingSpec == "219*8").SuggestedCompletionType.Should().Be("Urgent");
        group.Items.Single(i => i.RollingSpec == "273*8").SuggestedCompletionType.Should().Be("Urgent");
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_2030区间内_保持默认档()
    {
        using var ctx = CreateDbContext();
        // B001/B002 急-（非正常）各在轧 30000 → cPartial2=2 ∈ [1,3] → 保持「急+/急/急-」原始档
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll30, 1, isFinished: false, weight: 30000);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent);
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll30, 1, isFinished: false, weight: 30000, spec: "273*8");
        SeedSummary(ctx, "WO002", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent);
        SeedSchedule(ctx, ProcessKeys.ColdRoll30, "", "219*8", isFinished: true, dailyOutput: 5000m);
        SeedSchedule(ctx, ProcessKeys.ColdRoll30, "", "273*8", isFinished: true, dailyOutput: 5000m);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll30, ownedCount: 3, minMachines: 1, maxMachines: 3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var group = result.Single(g => g.MachineType == "冷轧2030");
        group.SuggestedTier.Should().Be("急+/急/急-");
        group.MachineCount.Should().Be(2);
        group.Status.Should().Be("OK");
        group.Items.Should().OnlyContain(i => i.SuggestedCompletionType == "Partial2" && i.SuggestedRollType == "Partial2");
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_三辊配了机台数上限_不足放宽全量仍不足_矛盾A()
    {
        using var ctx = CreateDbContext();
        // 统一产能平衡（2026-08-29）：三辊普通批次在轧 30000，配了机台数上限（min3 max5）也走产能平衡——
        // cPartial2=0（普通批次不命中急+/急/急-）< min3 → 放宽 Partial3/All，All 仍仅 1 台 < 3 → 矛盾 A「全量排程仍不足机台需求」
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ThreeRollColdRoll, 1, isFinished: false, weight: 30000);
        SeedSchedule(ctx, ProcessKeys.ThreeRollColdRoll, "", "219*8", isFinished: true, dailyOutput: 5000m);
        SeedMachineConfig(ctx, ProcessKeys.ThreeRollColdRoll, ownedCount: 4, minMachines: 3, maxMachines: 5);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var group = result.Single(g => g.MachineType == "冷轧三辊");
        group.SuggestedTier.Should().Be("-");
        group.MachineCount.Should().Be(1); // 矛盾无建议档位 → 现状机台（排程 CompletionType=All 命中普通批次 → 1 台）
        group.Status.Should().Be("A");
        group.Conflicts.Should().ContainSingle().Which.Should().Contain("全量排程仍不足机台需求");
        group.InProdTier.Should().BeNull();
        group.FinishedTier.Should().BeNull();
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_5060取消供给目标组_超上限矛盾A_受机台数上限约束()
    {
        using var ctx = CreateDbContext();
        // 复现用户场景：5060 取消「供给目标组」→ 无供需独立池；配了机台数上限（组 min2 max2）仍须受约束。
        // 急+批次 60 在轧 90000（daily 5000 → 机台需求 3 台）> 组 max2 → 收窄 Urgent/CrOnly 仍 3 台 > 2 → 矛盾 A'
        SeedMachineGroupConfigs(ctx);
        ctx.ColdRollMachineGroupConfigs.Single(g => g.GroupKey == ColdRollMachineGroupKeys.Roll5060).SupplyTargetGroupKey = null;
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll50, ownedCount: 1, minMachines: 1, maxMachines: 1);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll60, ownedCount: 1, minMachines: 1, maxMachines: 1);
        CreateBatch(ctx, "B001", "WO001", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 90000,
            currentGroupName: ProcessKeys.ColdRoll60, currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: true, dailyOutput: 5000m);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var group = result.Single(g => g.MachineType == "冷轧5060");
        group.FlowState.Should().BeNull();      // 无供需链 → 无流转状态
        group.Status.Should().Be("A'");         // 急+锁定已超最大机台数（不再静默绕过）
        group.SuggestedTier.Should().Be("-");
        group.Conflicts.Should().ContainSingle().Which.Should().Contain("急+锁定已超最大机台数");
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_5060流转平衡_两阶段_在制放宽喂饱2030总负荷未超上限成品不压缩()
    {
        using var ctx = CreateDbContext();
        // 两阶段判据统一 2030 基准：
        //  阶段1 = 在制品堆按「2030 产能档案 daily」折算的供给机台 < flowFrom5060 → 只放宽在制档位（成品不动）。
        //  阶段2 = 放宽后 5060 组总机台（在制+成品，5060 本组产能 daily 口径）> maxMachines → 才压缩成品档位。
        // flowFrom5060 = 全部 5060→2030 在制品折算 = B001(2) + B002(2) = 4；
        // 阶段1：Partial2 档仅急+B001 命中 → 供给 2 < 4 → 放宽；至 All 两批都命中 → 供给 4 ≥ 4 → 停，在制=All。
        // 阶段2：在制(10000 daily)2 台 + 成品(5000 daily)1 台 = 3 ≤ max 5 → 不触发，成品保持组档 Partial2。
        // B001：60→30(180*8) 在制 60000 急+（CreateBatch60Chain 默认 PositionDiff=0）→ Partial2 起命中，2030 折算 2 台
        CreateBatch60Chain(ctx, "B001", "WO001", "219*8", ProcessKeys.ColdRoll30, "180*8", weight: 60000);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        // B002：60→30(180*8) 在制 60000 普通（C缓，非急非顺）→ 仅 All 档命中，2030 折算 2 台
        CreateBatch60Chain(ctx, "B002", "WO002", "219*8", ProcessKeys.ColdRoll30, "180*8", weight: 60000);
        SeedSummary(ctx, "WO002", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.CSlow,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        // 2030 产能档案（方式B）：B001/B002 共用 30|219*8|180*8|true daily=5000
        ctx.ColdRollCapacities.Add(new ColdRollCapacity
        {
            ProcessType = ProcessKeys.ColdRoll30,
            BilletSpec = "219*8",
            RollingSpec = "180*8",
            IsFinished = true,
            DailyOutput = 5000m,
        });
        // B003：60成品(IsFinished=true) 30000 急+ spec 273*8 → 阶段2未触发（总负荷未超上限），成品保持组档不压缩
        CreateBatch(ctx, "B003", "WO003", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 30000, spec: "273*8");
        SeedSummary(ctx, "WO003", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        // 5060 本组产能档案（组机台总量约束用 5060 daily）：在制行 daily=10000、成品行 daily=5000
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: false, dailyOutput: 10000m);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "273*8", isFinished: true, dailyOutput: 5000m);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll60, ownedCount: 2, minMachines: 1, maxMachines: 5);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var group = result.Single(g => g.MachineType == "冷轧5060");
        group.SuggestedTier.Should().Be("急+/急/急-"); // ①产能 Partial2=2 ∈ [1,5] 保持默认档
        group.InProdTier.Should().Be("All");           // ②阶段1：在制增档至全量（普通在制品也需排上喂饱 2030）
        group.FinishedTier.Should().Be("Partial2");    // ②阶段2：总负荷 3 ≤ max 5 未超上限 → 成品不压缩
        group.MachineCount.Should().Be(3);             // 在制 2 + 成品 1
        group.Status.Should().Be("OK");

        var inProdItem = group.Items.Single(i => i.RollingSpec == "219*8");
        inProdItem.SuggestedCompletionType.Should().Be("All");
        inProdItem.RowStatus.Should().Be("锁定");

        var finishedItem = group.Items.Single(i => i.RollingSpec == "273*8");
        finishedItem.SuggestedCompletionType.Should().Be("Partial2");
        finishedItem.RowStatus.Should().Be("锁定");
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_5060流转平衡_在制供给已够2030_不触发对倒()
    {
        using var ctx = CreateDbContext();
        // 对倒判据统一 2030 基准：在制品堆按 2030 daily 折算的供给 = flowDemand2030 → 不触发对倒，
        // 在制/成品保持组档（普通成品不会被 5060 daily>2030 daily 的差异无脑压到 CrOnly）。
        // B001：60→30(180*8) 在制 60000 急+ → 2030 折算 2 台
        CreateBatch60Chain(ctx, "B001", "WO001", "219*8", ProcessKeys.ColdRoll30, "180*8", weight: 60000);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        ctx.ColdRollCapacities.Add(new ColdRollCapacity
        {
            ProcessType = ProcessKeys.ColdRoll30,
            BilletSpec = "219*8",
            RollingSpec = "180*8",
            IsFinished = true,
            DailyOutput = 5000m,
        });
        // B002：60成品(IsFinished=true) 30000 急+ → 在制供给已够 2030，急+成品也不被无脑压 CrOnly
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 30000, spec: "273*8");
        SeedSummary(ctx, "WO002", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: false, dailyOutput: 10000m);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "273*8", isFinished: true, dailyOutput: 5000m);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll60, ownedCount: 2, minMachines: 1, maxMachines: 3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var group = result.Single(g => g.MachineType == "冷轧5060");
        group.SuggestedTier.Should().Be("急+/急/急-"); // ①产能 cPartial2=B001(1)+B002(1)=2 ∈ [1,3] 保持默认
        group.InProdTier.Should().Be("Partial2");      // ②供给=flowDemand=2 不触发对倒，保持组档
        group.FinishedTier.Should().Be("Partial2");
        group.MachineCount.Should().Be(2);             // 在制 1 + 成品 1
        group.Status.Should().Be("OK");

        var inProdItem = group.Items.Single(i => i.RollingSpec == "219*8");
        inProdItem.SuggestedCompletionType.Should().Be("Partial2");
        var finishedItem = group.Items.Single(i => i.RollingSpec == "273*8");
        finishedItem.SuggestedCompletionType.Should().Be("Partial2");
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_5060流转平衡_两阶段_在制放宽后总负荷超上限再压缩成品()
    {
        using var ctx = CreateDbContext();
        // 阶段1：flowFrom5060 = B001(2) + B002(2) = 4；Partial2 档仅急+B001 命中 → 供给 2 < 4 → 放宽至 All → 4 ≥ 4 停，在制=All。
        // 阶段2：放宽后总机台 = 在制(5060 daily 10000)2 台 + 成品(5060 daily 5000)2 台 = 4 > max 3 → 压缩成品：
        //   Urgent 档急+成品仍命中 → 总 4>3 继续；CrOnly 档该急+成品关注≠60(关注30) 不命中 → 总 2 ≤ 3 → 停，成品=CrOnly。
        // B001：60→30(180*8) 在制 60000 急+ → 2030 折算 2 台
        CreateBatch60Chain(ctx, "B001", "WO001", "219*8", ProcessKeys.ColdRoll30, "180*8", weight: 60000);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        // B002：60→30(180*8) 在制 60000 普通（C缓）→ 仅 All 档命中，2030 折算 2 台
        CreateBatch60Chain(ctx, "B002", "WO002", "219*8", ProcessKeys.ColdRoll30, "180*8", weight: 60000);
        SeedSummary(ctx, "WO002", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.CSlow,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        // 2030 产能档案（方式B）：B001/B002 共用 30|219*8|180*8|true daily=5000
        ctx.ColdRollCapacities.Add(new ColdRollCapacity
        {
            ProcessType = ProcessKeys.ColdRoll30,
            BilletSpec = "219*8",
            RollingSpec = "180*8",
            IsFinished = true,
            DailyOutput = 5000m,
        });
        // B003：60成品(IsFinished=true) 60000 急+ spec 273*8，关注工序=30≠60 → CrOnly 档不命中，压缩至 CrOnly 让位
        CreateBatch(ctx, "B003", "WO003", ProcessKeys.ColdRoll60, 1, isFinished: false, weight: 60000, spec: "273*8");
        SeedSummary(ctx, "WO003", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll30);
        // 5060 本组产能档案（组机台总量约束用 5060 daily）：在制行 daily=10000、成品行 daily=5000
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: false, dailyOutput: 10000m);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "273*8", isFinished: true, dailyOutput: 5000m);
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll60, ownedCount: 3, minMachines: 1, maxMachines: 3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var group = result.Single(g => g.MachineType == "冷轧5060");
        group.SuggestedTier.Should().Be("急+/急/急-"); // ①产能 cPartial2=B001(1)+B003(2)=3 == max 3 区间内保持默认档
        group.InProdTier.Should().Be("All");           // ②阶段1：在制增档至全量喂饱 2030
        group.FinishedTier.Should().Be("CrOnly");      // ②阶段2：放宽后总负荷 4 > max 3 → 压缩成品至急+（关注不匹配者让位）
        group.MachineCount.Should().Be(2);             // 在制 2 + 成品(仅关注匹配者) 0
        group.Status.Should().Be("OK");

        var inProdItem = group.Items.Single(i => i.RollingSpec == "219*8");
        inProdItem.SuggestedCompletionType.Should().Be("All");
        var finishedItem = group.Items.Single(i => i.RollingSpec == "273*8");
        finishedItem.SuggestedCompletionType.Should().Be("CrOnly");
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_流转保底_窗口口径PD1至6待轧5060计入flowDemand()
    {
        using var ctx = CreateDbContext();
        // PD≤6 窗口口径：未产 5060→30 批次（Status=None → PositionDiff=1，非当日在轧）仍计入 6 天流转窗口
        // → flowDemand2030=3（原 PD==0 口径会漏计，5060 在制=73t 与仅 17.6t 的差额即此）
        var batch = CreateBatch60Chain(ctx, "B001", "WO001", "219*8", ProcessKeys.ColdRoll30, "180*8", weight: 90000);
        batch.Status = BatchStatus.None; // 未投产 → PD=diff=1
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: false); // 60行排程（All 命中）→ 有流转要求才计入 flowDemand
        ctx.ColdRollCapacities.Add(new ColdRollCapacity
        {
            ProcessType = ProcessKeys.ColdRoll30,
            BilletSpec = "219*8",
            RollingSpec = "180*8",
            IsFinished = true,
            DailyOutput = 5000m,
        });
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll30, ownedCount: 3, minMachines: 1, maxMachines: 3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var supplier = result.Single(g => g.MachineType == "冷轧5060");
        supplier.FlowState.Should().NotBeNull();
        supplier.FlowState!.SupplyMachines.Should().Be(3); // PD=1 计入：90000/(5000×6)=3
        var demander = result.Single(g => g.MachineType == "冷轧2030");
        demander.FlowState.Should().NotBeNull();
        demander.FlowState!.SupplyMachines.Should().Be(3);
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_流转保底_无流转要求档位不命中的5060不计入flowDemand()
    {
        using var ctx = CreateDbContext();
        // B001：60→30(180*8) 在制 60000 普通（CSlow，非急非顺）→ 60 行档位 CrOnly（仅急+命中）不命中
        // → 该料无流转要求，不计入 2030 需求（对应真实场景：50.3t 档位不命中的料不产生 flowDemand）
        CreateBatch60Chain(ctx, "B001", "WO001", "219*8", ProcessKeys.ColdRoll30, "180*8", weight: 60000);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.CSlow,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        ctx.ColdRollCapacities.Add(new ColdRollCapacity
        {
            ProcessType = ProcessKeys.ColdRoll30,
            BilletSpec = "219*8",
            RollingSpec = "180*8",
            IsFinished = true,
            DailyOutput = 5000m,
        });
        // 60 在制行仅排急+档（CrOnly）→ 普通批次不命中
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: false,
            completionType: "CrOnly", rollType: "CrOnly");
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll30, ownedCount: 3, minMachines: 1, maxMachines: 3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var demander = result.Single(g => g.MachineType == "冷轧2030");
        demander.FlowState.Should().NotBeNull();
        demander.FlowState!.SupplyMachines.Should().Be(0); // CrOnly 档不命中普通批次 → 不计入需求
        demander.FlowState.NeedMachines.Should().Be(1);
        demander.FlowState.Balanced.Should().BeFalse();
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_流转保底_2030本组本次未定流转计入flowDemand()
    {
        using var ctx = CreateDbContext();
        // 部分二：60→30 在制 60000 急+（60 行 All 命中）→ 30 产能 5000 → 流入 2 台
        CreateBatch60Chain(ctx, "B001", "WO001", "219*8", ProcessKeys.ColdRoll30, "180*8", weight: 60000);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll60);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: false); // 60 在制行 All 命中
        ctx.ColdRollCapacities.Add(new ColdRollCapacity
        {
            ProcessType = ProcessKeys.ColdRoll30,
            BilletSpec = "219*8",
            RollingSpec = "180*8",
            IsFinished = true,
            DailyOutput = 5000m,
        });
        // 部分一：2030 本组在制 30000 普通（CSlow）→ 30 行档位 CrOnly（仅急+命中）不命中 → 本次未定流转计入 1 台
        CreateBatch(ctx, "B002", "WO002", ProcessKeys.ColdRoll30, 1, isFinished: false,
            currentGroupName: ProcessKeys.ColdRoll30, currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false, weight: 30000, spec: "180*8");
        SeedSummary(ctx, "WO002", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.CSlow,
            productionFlowProperty: ProductionFlowKeys.Normal, mainNoAttentionProcess: ProcessKeys.ColdRoll30);
        SeedSchedule(ctx, ProcessKeys.ColdRoll30, "", "180*8", isFinished: true,
            completionType: "CrOnly", rollType: "CrOnly");
        ctx.ColdRollCapacities.Add(new ColdRollCapacity
        {
            ProcessType = ProcessKeys.ColdRoll30,
            BilletSpec = "",
            RollingSpec = "180*8",
            IsFinished = true,
            DailyOutput = 5000m,
        });
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll30, ownedCount: 3, minMachines: 1, maxMachines: 3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var demander = result.Single(g => g.MachineType == "冷轧2030");
        demander.FlowState.Should().NotBeNull();
        // 下次 2030 承接 = 5060 流入 2 + 2030 本组本次未定流转 1 = 3 台
        demander.FlowState!.SupplyMachines.Should().Be(3);
        demander.FlowState.NeedMachines.Should().Be(1);
        demander.FlowState.Balanced.Should().BeTrue();
    }

    // ==================== 机台组配置表驱动 集成测试 ====================

    [Fact]
    public async Task GetMachineEstimateAsync_新增冷轧工序归入现有5060组_按组聚合()
    {
        using var ctx = CreateDbContext();
        SeedMachineGroupConfigs(ctx);
        // 配置化归组：5060 组 ProcessKeys 追加 ColdRoll75（免代码把新工序并入现有组）
        var g5060 = ctx.ColdRollMachineGroupConfigs.First(g => g.GroupKey == "5060");
        g5060.ProcessKeys += $",ColdRoll75";
        ctx.SaveChanges();

        // 同批两个 ColdRoll75 工序组：seq2=在制、seq5=最后=成品，均归入「冷轧5060」行
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
                new() { ProcessName = "ColdRoll75", SequenceNumber = 2, ColdRollDraw = 1, ManufacturingSpec = "219*8" },
                new() { ProcessName = "ColdRoll75", SequenceNumber = 5, ColdRollDraw = 1, ManufacturingSpec = "219*8" },
            }
        };
        ctx.ProductionBatches.Add(batch);
        SeedSchedule(ctx, "ColdRoll75", "", "219*8", isFinished: false);
        SeedSchedule(ctx, "ColdRoll75", "219*8", "219*8", isFinished: true);
        await ctx.SaveChangesAsync();

        var svc = CreateServiceWithExtraProcessKeys(ctx, "ColdRoll75");
        var result = await svc.GetMachineEstimateAsync();

        result.Should().HaveCount(4); // 组数不变：新工序并入现有 5060 组
        var row = result.Single(r => r.MachineType == "冷轧5060");
        row.FlowTotalWeight.Should().Be(2000m);   // 在制 + 成品归并
        row.InProcessWeight.Should().Be(1000m);   // seq2 非最后 = 在制
        row.FinishedWeight.Should().Be(1000m);    // seq5 最后 = 成品
    }

    [Fact]
    public async Task GetMachineEstimateAsync_新建55组None_新组独立输出()
    {
        using var ctx = CreateDbContext();
        SeedMachineGroupConfigs(ctx);
        // 新建组：GroupKey=55、工序 ColdRoll55、无供给目标（独立池）
        ctx.ColdRollMachineGroupConfigs.Add(new ColdRollMachineGroupConfig
        {
            GroupKey = "55",
            DisplayName = "冷轧55",
            ProcessKeys = "ColdRoll55",
            DisplayOrder = 5,
        });
        ctx.SaveChanges();

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
            TotalWeight = 3000m,
            TotalItemCount = 1,
            TechnicalRequirements = "NORMAL",
            CurrentValidWeight = 3000,
            RowVersion = new byte[8],
            ProcessGroups = new List<ProcessGroup>
            {
                new() { ProcessName = "ColdRoll55", SequenceNumber = 2, ColdRollDraw = 1, ManufacturingSpec = "219*8" },
                new() { ProcessName = "ColdRoll55", SequenceNumber = 5, ColdRollDraw = 1, ManufacturingSpec = "219*8" },
            }
        };
        ctx.ProductionBatches.Add(batch);
        SeedSchedule(ctx, "ColdRoll55", "", "219*8", isFinished: false);
        SeedSchedule(ctx, "ColdRoll55", "219*8", "219*8", isFinished: true);
        await ctx.SaveChangesAsync();

        var svc = CreateServiceWithExtraProcessKeys(ctx, "ColdRoll55");
        var result = await svc.GetMachineEstimateAsync();

        result.Should().HaveCount(5); // 4 组 + 新组
        var row = result.Single(r => r.MachineType == "冷轧55");
        row.FlowTotalWeight.Should().Be(6000m);
    }

    // ==================== 方案 A：多链 / 多级链 集成测试 ====================

    [Fact]
    public async Task GetScheduleSuggestionAsync_多供给方并行链_需求方汇聚两供给流入()
    {
        using var ctx = CreateDbContext();
        SeedMachineGroupConfigs(ctx);
        // 新增第二供给方组 Sup75（ColdRoll75，供给目标 2030）→ 多供给方并行链：5060→2030、Sup75→2030
        ctx.ColdRollMachineGroupConfigs.Add(new ColdRollMachineGroupConfig
        {
            GroupKey = "Sup75",
            DisplayName = "冷轧75",
            ProcessKeys = "ColdRoll75",
            DisplayOrder = 5,
            SupplyTargetGroupKey = ColdRollMachineGroupKeys.Roll2030,
        });
        await ctx.SaveChangesAsync();

        // B001：60 在制 → 30(180*8)：5060 供给流入 2030 = 60000/(5000×6)=2
        CreateBatch60Chain(ctx, "B001", "WO001", "219*8", ProcessKeys.ColdRoll30, "180*8", weight: 60000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: false);
        // B002：75 在制 → 30(180*8)：Sup75 供给流入 2030 = 2
        CreateBatchChain(ctx, "B002", "WO002", "ColdRoll75", "219*8", ProcessKeys.ColdRoll30, "180*8", weight: 60000);
        SeedSchedule(ctx, "ColdRoll75", "", "219*8", isFinished: false);
        ctx.ColdRollCapacities.Add(new ColdRollCapacity
        {
            ProcessType = ProcessKeys.ColdRoll30,
            BilletSpec = "219*8",
            RollingSpec = "180*8",
            IsFinished = true,
            DailyOutput = 5000m,
        });
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll30, ownedCount: 3, minMachines: 1, maxMachines: 3);
        await ctx.SaveChangesAsync();

        var svc = CreateServiceWithExtraProcessKeys(ctx, "ColdRoll75");
        var result = await svc.GetScheduleSuggestionAsync();

        var demander = result.Single(g => g.MachineType == "冷轧2030");
        demander.FlowState.Should().NotBeNull();
        demander.FlowState!.Role.Should().Be("Demander");
        demander.FlowState.SupplyMachines.Should().Be(4); // 5060 供给 2 + Sup75 供给 2 汇聚

        var sup75 = result.Single(g => g.MachineType == "冷轧75");
        sup75.FlowState.Should().NotBeNull();
        sup75.FlowState!.Role.Should().Be("Supplier");
        sup75.FlowState.SupplyMachines.Should().Be(2); // 仅本组→2030 流入，不含 5060 的

        var sup5060 = result.Single(g => g.MachineType == "冷轧5060");
        sup5060.FlowState.Should().NotBeNull();
        sup5060.FlowState!.Role.Should().Be("Supplier");
        sup5060.FlowState.SupplyMachines.Should().Be(2);
    }

    [Fact]
    public async Task GetScheduleSuggestionAsync_多级链_中间节点角色Both()
    {
        using var ctx = CreateDbContext();
        SeedMachineGroupConfigs(ctx);
        // 末端需求组 55（工序 ColdRoll20）：2030 指向它 → 2030 成为多级链中间节点（既承接 5060 又再供给 55）
        // 工序全局唯一归属：20 从 2030 组移出划归 55 组（引擎流转折算的 next 探测仍走内置 ProcessKeys.IsColdRollOrColdDraw，须用内置冷轧工序）
        ctx.ColdRollMachineGroupConfigs.Add(new ColdRollMachineGroupConfig
        {
            GroupKey = "55",
            DisplayName = "冷轧55",
            ProcessKeys = ProcessKeys.ColdRoll20,
            DisplayOrder = 5,
        });
        var grp2030 = ctx.ColdRollMachineGroupConfigs.Single(g => g.GroupKey == ColdRollMachineGroupKeys.Roll2030);
        grp2030.ProcessKeys = ProcessKeys.ColdRoll30;
        grp2030.SupplyTargetGroupKey = "55";
        await ctx.SaveChangesAsync();

        // B001：60 在制 → 30(180*8)：5060 供给流入 2030 = 2
        CreateBatch60Chain(ctx, "B001", "WO001", "219*8", ProcessKeys.ColdRoll30, "180*8", weight: 60000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll60, "", "219*8", isFinished: false);
        ctx.ColdRollCapacities.Add(new ColdRollCapacity
        {
            ProcessType = ProcessKeys.ColdRoll30,
            BilletSpec = "219*8",
            RollingSpec = "180*8",
            IsFinished = true,
            DailyOutput = 5000m,
        });
        // B002：30 在制 → 20：2030 供给流入 55 = 2
        CreateBatchChain(ctx, "B002", "WO002", ProcessKeys.ColdRoll30, "180*8", ProcessKeys.ColdRoll20, "76*4", weight: 60000);
        SeedSchedule(ctx, ProcessKeys.ColdRoll30, "", "180*8", isFinished: false);
        ctx.ColdRollCapacities.Add(new ColdRollCapacity
        {
            ProcessType = ProcessKeys.ColdRoll20,
            BilletSpec = "180*8",
            RollingSpec = "76*4",
            IsFinished = true,
            DailyOutput = 5000m,
        });
        SeedMachineConfig(ctx, ProcessKeys.ColdRoll30, ownedCount: 3, minMachines: 1, maxMachines: 3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetScheduleSuggestionAsync();

        var mid = result.Single(g => g.MachineType == "冷轧2030");
        mid.FlowState.Should().NotBeNull();
        mid.FlowState!.Role.Should().Be("Both"); // 中间节点：既承接 5060 又再供给 55
        mid.FlowState.SupplyMachines.Should().Be(2); // 承接流入（5060 供给）

        var end = result.Single(g => g.MachineType == "冷轧55");
        end.FlowState.Should().NotBeNull();
        end.FlowState!.Role.Should().Be("Demander");
        end.FlowState.SupplyMachines.Should().Be(2); // 2030 供给流入

        var sup = result.Single(g => g.MachineType == "冷轧5060");
        sup.FlowState.Should().NotBeNull();
        sup.FlowState!.Role.Should().Be("Supplier");
    }
}
