using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Scheduling;
using MES.Core.Enums;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.WorkOrder;
using MES.Services.Scheduling;
using MES.Tests.Tests;

namespace MES.Tests.Services.Scheduling;

/// <summary>
/// 批次计划薄表（BatchPlanSchedule）计划安排测试：PlanAllAsync 三规则
/// 规则(1) 冷轧排程优先 → 规则(2) 重点生产批次兜底 → 规则(3) 降级
/// </summary>
public class BatchPlanScheduleServiceTests : TestBase
{
    private BatchPlanScheduleService CreateService(AppDbContext ctx) => new(ctx, CreateProcessDefinitionServiceMock());

    private ProductionBatch CreateBatch(AppDbContext ctx, string batchNo, string workOrderNo,
        BatchStatus status = BatchStatus.InProgress,
        string? currentGroupName = null, string? currentSectionName = null,
        bool? currentSectionCompleted = null,
        string? currentSpec = null, string? correspondingSpec = null,
        string? currentEquipmentName = null, string? currentOutsource = null,
        string? nextProcess = null, string? nextSectionName = null)
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
            Specification = "219*8",
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = 2500m,
            TotalItemCount = 1,
            TechnicalRequirements = "NORMAL",
            CurrentValidWeight = 1000,
            CurrentGroupName = currentGroupName,
            CurrentSectionName = currentSectionName,
            CurrentSectionCompleted = currentSectionCompleted,
            CurrentSpec = currentSpec,
            CorrespondingSpec = correspondingSpec,
            CurrentEquipmentName = currentEquipmentName,
            CurrentOutsource = currentOutsource,
            NextProcess = nextProcess,
            NextSectionName = nextSectionName,
            RowVersion = new byte[8],
        };
        ctx.ProductionBatches.Add(batch);
        return batch;
    }

    private void SeedSummary(AppDbContext ctx, string workOrderNo,
        int scheduleStage = 1, string? urgencyLevel = null,
        string? mainNoAttentionProcess = null, string? productionFlowProperty = null)
    {
        int wid = Math.Abs(workOrderNo.GetHashCode());
        var summary = new WorkOrderExecutionSummary
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
            MainNoAttentionProcess = mainNoAttentionProcess,
            ProductionFlowProperty = productionFlowProperty,
        };
        ctx.Set<WorkOrderExecutionSummary>().Add(summary);
    }

    private static ProcessGroup AddProcessGroup(AppDbContext ctx, int batchId, string processName,
        int sequenceNumber, string? manufacturingSpec = null,
        int? coldRollDraw = null, int? inspection = null)
    {
        var pg = new ProcessGroup
        {
            ProductionBatchId = batchId,
            ProcessName = processName,
            SequenceNumber = sequenceNumber,
            ManufacturingSpec = manufacturingSpec,
            ColdRollDraw = coldRollDraw,
            Inspection = inspection,
        };
        ctx.ProcessGroups.Add(pg);
        return pg;
    }

    private async Task<BatchPlanSchedule> SinglePlanAsync(AppDbContext ctx)
    {
        var plan = ctx.BatchPlanSchedules.Single();
        // EF InMemory 跟踪实体字段可能不即时，用 AsNoTracking 再读一遍保证断言准确
        return await ctx.BatchPlanSchedules.AsNoTracking().SingleAsync();
    }

    // ==================== 规则(3) 降级 ====================

    [Fact]
    public async Task PlanAllAsync_规则3_无冷轧排程非重点_降级()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress);
        // BOrder 非急单：既无冷轧排程命中（无小表），重点判定前置条件（A+急/A急）也不满足
        SeedSummary(ctx, "WO001",
            scheduleStage: 3,
            urgencyLevel: UrgencyLevelKeys.BOrder,
            mainNoAttentionProcess: ProcessKeys.ColdRoll60,
            productionFlowProperty: ProductionFlowKeys.Normal);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var ok = await svc.PlanAllAsync(null);
        ok.Should().BeTrue();

        var plan = await SinglePlanAsync(ctx);
        plan.IsFlow.Should().BeFalse();
        plan.FlowLevel.Should().Be(5);   // 略
        plan.FlowTarget.Should().BeNull();
        plan.FlowCRType.Should().BeNull();
        plan.PlanOuterDiameterSpan.Should().BeNull();
        plan.FlowExecSpec.Should().BeNull();
        plan.TargetSequence.Should().Be(0);
        plan.ExecutionSequence.Should().BeNull();
    }

    [Fact]
    public async Task PlanAllAsync_规则3_在产降级批次_执行序按现执行序填入()
    {
        using var ctx = CreateDbContext();
        // 在产批次：当前工序组冷轧60、冷轧拔工段未完成 → 现执行序 = 冷轧拔工段序 1
        var batch = CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false);
        AddProcessGroup(ctx, batch.Id, ProcessKeys.ColdRoll60, 2, coldRollDraw: 1);
        // BOrder 非急单：既无冷轧排程命中，重点判定也不满足 → 规则3 降级
        SeedSummary(ctx, "WO001",
            scheduleStage: 3,
            urgencyLevel: UrgencyLevelKeys.BOrder,
            mainNoAttentionProcess: ProcessKeys.ColdRoll60,
            productionFlowProperty: ProductionFlowKeys.Normal);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.PlanAllAsync(null);

        var plan = await SinglePlanAsync(ctx);
        plan.IsFlow.Should().BeFalse();
        plan.FlowLevel.Should().Be(5);   // 略
        plan.TargetSequence.Should().Be(0);
        // 即使流转=否，执行序仍按状态跟踪"现执行序"填入（供执行反馈组原/现工量差判定）
        plan.ExecutionSequence.Should().Be(1);
    }

    // ==================== 规则(2) 重点生产批次兜底 ====================

    [Fact]
    public async Task PlanAllAsync_规则2_荒管处理重点批次_兜底填充()
    {
        using var ctx = CreateDbContext();
        var batch = CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress);
        // 未产批次（无当前工序组）：执行序视为 0，0 < 相应工段序(2) → 重点
        AddProcessGroup(ctx, batch.Id, ProcessKeys.RoughTubeProcessing, 1,
            manufacturingSpec: "219*8", inspection: 2);
        SeedSummary(ctx, "WO001",
            scheduleStage: 3,
            urgencyLevel: UrgencyLevelKeys.AUrgent,
            mainNoAttentionProcess: ProcessKeys.RoughTubeProcessing,
            productionFlowProperty: ProductionFlowKeys.Normal);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.PlanAllAsync(null);

        var plan = await SinglePlanAsync(ctx);
        plan.IsFlow.Should().BeTrue();
        plan.FlowLevel.Should().Be(2);   // 急
        plan.FlowTarget.Should().Be(FlowTargetKeys.RoughTubeCheck);   // 冷轧类型=荒管处理 → 荒管检
        plan.FlowCRType.Should().Be(ProcessKeys.RoughTubeProcessing);
        plan.PlanOuterDiameterSpan.Should().BeNull();
        plan.FlowExecSpec.Should().Be("219*8");   // 关注工序对应工序组的规格
        plan.TargetSequence.Should().Be(2);        // 相应工段序 = 检验工段序
        plan.ExecutionSequence.Should().BeNull();
    }

    [Fact]
    public async Task PlanAllAsync_规则2_生产收尾_执行规格取状态跟踪组()
    {
        using var ctx = CreateDbContext();
        var batch = CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false,
            currentSpec: "89*8",
            correspondingSpec: "76*8",
            currentEquipmentName: "M1");
        // 最大工序组（成品检验）检验工段序 3 → 相应工段序 2
        AddProcessGroup(ctx, batch.Id, ProcessKeys.RoughTubeProcessing, 1, manufacturingSpec: "110*8");
        AddProcessGroup(ctx, batch.Id, ProcessKeys.ColdRoll60, 2, manufacturingSpec: "89*8", coldRollDraw: 1);
        AddProcessGroup(ctx, batch.Id, ProcessKeys.AdditionalFinalInspection, 3, manufacturingSpec: "89*8", inspection: 3);
        SeedSummary(ctx, "WO001",
            scheduleStage: 3,
            urgencyLevel: UrgencyLevelKeys.AUrgent,
            mainNoAttentionProcess: ProductionAttentionKeys.Finish,
            productionFlowProperty: ProductionFlowKeys.Normal);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.PlanAllAsync(null);

        var plan = await SinglePlanAsync(ctx);
        plan.IsFlow.Should().BeTrue();
        plan.FlowLevel.Should().Be(2);   // 急
        plan.FlowTarget.Should().Be(FlowTargetKeys.FinalCheck);   // 冷轧类型=生产收尾 → 成品检验
        plan.FlowCRType.Should().Be(ProductionAttentionKeys.Finish);
        plan.FlowExecSpec.Should().Be("89*8");   // 生产收尾 → 状态跟踪组执行规格（未完工 → CurrentSpec）
        plan.TargetSequence.Should().Be(2);        // 最大工序组检验 3 - 1
        plan.ExecutionSequence.Should().Be(1);     // 当前工序组冷轧拔工段序
    }

    [Fact]
    public async Task PlanAllAsync_规则2_在制修检重点批次_流转目标在制检()
    {
        using var ctx = CreateDbContext();
        var batch = CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress);
        // 未产批次：执行序视为 0，0 < 相应工段序(2) → 重点
        AddProcessGroup(ctx, batch.Id, ProcessKeys.InProcessRepair, 1,
            manufacturingSpec: "219*8", inspection: 2);
        SeedSummary(ctx, "WO001",
            scheduleStage: 3,
            urgencyLevel: UrgencyLevelKeys.AUrgent,
            mainNoAttentionProcess: ProcessKeys.InProcessRepair,
            productionFlowProperty: ProductionFlowKeys.Normal);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.PlanAllAsync(null);

        var plan = await SinglePlanAsync(ctx);
        plan.IsFlow.Should().BeTrue();
        plan.FlowLevel.Should().Be(2);   // 急
        plan.FlowTarget.Should().Be(FlowTargetKeys.InProcessCheck);   // 冷轧类型=在制修检 → 在制检
        plan.FlowCRType.Should().Be(ProcessKeys.InProcessRepair);
    }

    [Fact]
    public async Task PlanAllAsync_规则2_冷轧类关注工序_流转目标冷轧()
    {
        using var ctx = CreateDbContext();
        var batch = CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress);
        AddProcessGroup(ctx, batch.Id, ProcessKeys.ColdRoll60, 1,
            manufacturingSpec: "219*8", coldRollDraw: 1);
        SeedSummary(ctx, "WO001",
            scheduleStage: 3,
            urgencyLevel: UrgencyLevelKeys.AUrgent,
            mainNoAttentionProcess: ProcessKeys.ColdRoll60,
            productionFlowProperty: ProductionFlowKeys.Normal);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.PlanAllAsync(null);

        var plan = await SinglePlanAsync(ctx);
        plan.IsFlow.Should().BeTrue();
        plan.FlowLevel.Should().Be(2);   // 急
        plan.FlowTarget.Should().Be(FlowTargetKeys.ColdRoll);   // 剩余（冷轧类工序）→ 冷轧
        plan.FlowCRType.Should().Be(ProcessKeys.ColdRoll60);
    }

    // ==================== 规则(1) 冷轧排程优先 ====================

    [Fact]
    public async Task PlanAllAsync_规则1_冷轧排程命中_按实时值填充()
    {
        using var ctx = CreateDbContext();
        var batch = CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false,
            currentSpec: "89*8",
            correspondingSpec: "76*8",
            currentEquipmentName: "M1");
        AddProcessGroup(ctx, batch.Id, ProcessKeys.RoughTubeProcessing, 1, manufacturingSpec: "110*8");
        AddProcessGroup(ctx, batch.Id, ProcessKeys.ColdRoll60, 2, manufacturingSpec: "89*8", coldRollDraw: 1);
        // 关注工序 ≠ 命中冷轧排程行工序 → AttentionMatchesCurrentCR=false → 排程档位 2 急
        SeedSummary(ctx, "WO001",
            scheduleStage: 3,
            urgencyLevel: UrgencyLevelKeys.AUrgent,
            mainNoAttentionProcess: ProcessKeys.ColdRoll50,
            productionFlowProperty: ProductionFlowKeys.Normal);
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = ProcessKeys.ColdRoll60,
            BilletSpec = "110*8",
            RollingSpec = "89*8",
            IsFinished = true,
            CompletionType = "All",
            RollType = "None",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.PlanAllAsync(null);

        var plan = await SinglePlanAsync(ctx);
        plan.IsFlow.Should().BeTrue();
        plan.FlowLevel.Should().Be(2);                    // 实时档位 2 急 → 薄表 2 急
        plan.FlowTarget.Should().Be(FlowTargetKeys.CompletionColdRoll);
        plan.FlowCRType.Should().Be(ProcessKeys.ColdRoll60);
        plan.PlanOuterDiameterSpan.Should().Be("110-89");
        plan.FlowExecSpec.Should().Be("89*8");
        plan.TargetSequence.Should().Be(2);               // 冷轧拔工段序 1 + 1
        plan.ExecutionSequence.Should().Be(1);            // 当前工序组冷轧拔工段序
    }

    [Fact]
    public async Task PlanAllAsync_规则1_急加档位_映射薄表急加()
    {
        using var ctx = CreateDbContext();
        var batch = CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false,
            currentSpec: "89*8",
            correspondingSpec: "76*8",
            currentEquipmentName: "M1");
        AddProcessGroup(ctx, batch.Id, ProcessKeys.RoughTubeProcessing, 1, manufacturingSpec: "110*8");
        AddProcessGroup(ctx, batch.Id, ProcessKeys.ColdRoll60, 2, manufacturingSpec: "89*8", coldRollDraw: 1);
        // 关注工序 == 命中冷轧排程行工序 → AttentionMatchesCurrentCR=true → 排程档位 1 急+
        SeedSummary(ctx, "WO001",
            scheduleStage: 3,
            urgencyLevel: UrgencyLevelKeys.AUrgent,
            mainNoAttentionProcess: ProcessKeys.ColdRoll60,
            productionFlowProperty: ProductionFlowKeys.Normal);
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = ProcessKeys.ColdRoll60,
            BilletSpec = "110*8",
            RollingSpec = "89*8",
            IsFinished = true,
            CompletionType = "All",
            RollType = "None",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.PlanAllAsync(null);

        var plan = await SinglePlanAsync(ctx);
        plan.IsFlow.Should().BeTrue();
        plan.FlowLevel.Should().Be(1);   // 实时档位 1 急+ → 薄表 1 急+
    }

    [Fact]
    public async Task PlanAllAsync_规则1_计划备注默认填充待轧设备号()
    {
        using var ctx = CreateDbContext();
        // 待轧批次：在轧设备为空 → 待轧要求场景1 匹配本层冷轧维度（命中冷轧排程 MachineNo）
        var batch = CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false,
            currentSpec: "89*8",
            correspondingSpec: "76*8");
        AddProcessGroup(ctx, batch.Id, ProcessKeys.RoughTubeProcessing, 1, manufacturingSpec: "110*8");
        AddProcessGroup(ctx, batch.Id, ProcessKeys.ColdRoll60, 2, manufacturingSpec: "89*8", coldRollDraw: 1);
        SeedSummary(ctx, "WO001",
            scheduleStage: 3,
            urgencyLevel: UrgencyLevelKeys.BOrder,
            mainNoAttentionProcess: ProcessKeys.ColdRoll50,
            productionFlowProperty: ProductionFlowKeys.Normal);
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = ProcessKeys.ColdRoll60,
            BilletSpec = "110*8",
            RollingSpec = "89*8",
            IsFinished = true,
            CompletionType = "None",
            RollType = "All",
            MachineNo = "60-1#；60-2#",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.PlanAllAsync(null);

        var plan = await SinglePlanAsync(ctx);
        plan.IsFlow.Should().BeTrue();
        // 计划备注默认 = 关联冷轧排程的待轧设备号
        plan.PlanRemark.Should().Be("60-1#；60-2#");
    }

    // ==================== 已有计划记录：保留抢单/备注，字段被覆盖 ====================

    [Fact]
    public async Task PlanAllAsync_已有计划记录_覆盖自动字段_保留抢单备注()
    {
        using var ctx = CreateDbContext();
        var batch = CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress);
        SeedSummary(ctx, "WO001",
            scheduleStage: 3,
            urgencyLevel: UrgencyLevelKeys.BOrder,
            mainNoAttentionProcess: ProcessKeys.ColdRoll60,
            productionFlowProperty: ProductionFlowKeys.Normal);
        ctx.BatchPlanSchedules.Add(new BatchPlanSchedule
        {
            BatchId = batch.Id,
            IsFlow = true,
            FlowLevel = 3,
            FlowTarget = "手工目标",
            FlowCRType = ProcessKeys.ColdRoll60,
            PlanOuterDiameterSpan = "手工跨度",
            FlowExecSpec = "手工规格",
            TargetSequence = 99,
            ExecutionSequence = 88,
            IsGrabOrder = true,
            PlanRemark = "手工备注",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.PlanAllAsync(null);

        var plan = ctx.BatchPlanSchedules.AsNoTracking().Single();
        // 降级覆盖自动字段
        plan.IsFlow.Should().BeFalse();
        plan.FlowLevel.Should().Be(5);
        plan.TargetSequence.Should().Be(0);
        plan.ExecutionSequence.Should().BeNull();
        // 保留 抢单 和 备注
        plan.IsGrabOrder.Should().BeTrue();
        plan.PlanRemark.Should().Be("手工备注");
    }

    [Fact]
    public async Task PlanAllAsync_已有计划记录_空备注补填设备号_非空备注保留()
    {
        using var ctx = CreateDbContext();
        // 批次 A：已有非空备注；批次 B：已有空备注；两者均待轧（在轧设备为空）命中冷轧排程设备号
        var batchA = CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false);
        var batchB = CreateBatch(ctx, "B002", "WO002", BatchStatus.InProgress,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false);
        AddProcessGroup(ctx, batchA.Id, ProcessKeys.RoughTubeProcessing, 1, manufacturingSpec: "110*8");
        AddProcessGroup(ctx, batchA.Id, ProcessKeys.ColdRoll60, 2, manufacturingSpec: "89*8", coldRollDraw: 1);
        AddProcessGroup(ctx, batchB.Id, ProcessKeys.RoughTubeProcessing, 1, manufacturingSpec: "110*8");
        AddProcessGroup(ctx, batchB.Id, ProcessKeys.ColdRoll60, 2, manufacturingSpec: "89*8", coldRollDraw: 1);
        SeedSummary(ctx, "WO001",
            scheduleStage: 3,
            urgencyLevel: UrgencyLevelKeys.BOrder,
            mainNoAttentionProcess: ProcessKeys.ColdRoll50,
            productionFlowProperty: ProductionFlowKeys.Normal);
        SeedSummary(ctx, "WO002",
            scheduleStage: 3,
            urgencyLevel: UrgencyLevelKeys.BOrder,
            mainNoAttentionProcess: ProcessKeys.ColdRoll50,
            productionFlowProperty: ProductionFlowKeys.Normal);
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = ProcessKeys.ColdRoll60,
            BilletSpec = "110*8",
            RollingSpec = "89*8",
            IsFinished = true,
            CompletionType = "None",
            RollType = "All",
            MachineNo = "60-1#；60-2#",
        });
        ctx.BatchPlanSchedules.Add(new BatchPlanSchedule
        {
            BatchId = batchA.Id,
            IsFlow = false,
            FlowLevel = 5,
            PlanRemark = "手工备注",
        });
        ctx.BatchPlanSchedules.Add(new BatchPlanSchedule
        {
            BatchId = batchB.Id,
            IsFlow = false,
            FlowLevel = 5,
            PlanRemark = null,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.PlanAllAsync(null);

        var planA = ctx.BatchPlanSchedules.AsNoTracking().Single(x => x.BatchId == batchA.Id);
        planA.PlanRemark.Should().Be("手工备注");   // 非空备注保留不覆盖
        var planB = ctx.BatchPlanSchedules.AsNoTracking().Single(x => x.BatchId == batchB.Id);
        planB.PlanRemark.Should().Be("60-1#；60-2#");   // 空备注补填默认设备号
    }

    [Fact]
    public async Task SaveAsync_暂停是_保留原流转字段_切回否照常覆盖()
    {
        using var ctx = CreateDbContext();
        var batch = CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);

        // 1. 新建：未暂停，写入流转
        await svc.SaveAsync(new BatchPlanScheduleDto
        {
            BatchId = batch.Id,
            IsFlow = true,
            FlowLevel = 1,
            FlowTarget = "F",
            FlowCRType = ProcessKeys.ColdRoll60,
            PlanOuterDiameterSpan = "span",
            FlowExecSpec = "spec",
            TargetSequence = 5,
            ExecutionSequence = 3,
        });

        // 2. 暂停=是：dto 带读时覆盖后的值，但 DB 保留原流转字段
        await svc.SaveAsync(new BatchPlanScheduleDto
        {
            BatchId = batch.Id,
            IsPaused = true,
            IsFlow = false,
            FlowLevel = 5,
            FlowTarget = null,
            FlowCRType = null,
            PlanOuterDiameterSpan = null,
            FlowExecSpec = null,
            TargetSequence = null,
            ExecutionSequence = null,
        });

        var paused = ctx.BatchPlanSchedules.AsNoTracking().Single(x => x.BatchId == batch.Id);
        paused.IsPaused.Should().BeTrue();
        paused.IsFlow.Should().BeTrue();      // 保留原流转
        paused.FlowLevel.Should().Be(1);
        paused.FlowTarget.Should().Be("F");
        paused.FlowCRType.Should().Be(ProcessKeys.ColdRoll60);
        paused.PlanOuterDiameterSpan.Should().Be("span");
        paused.FlowExecSpec.Should().Be("spec");
        paused.TargetSequence.Should().Be(5);
        paused.ExecutionSequence.Should().Be(3);

        // 3. 切回"否"：仅改暂停标记，流转字段保留 DB 原值（读时覆盖消失 → 恢复原流转）
        await svc.SaveAsync(new BatchPlanScheduleDto
        {
            BatchId = batch.Id,
            IsPaused = false,
            IsFlow = false,      // 覆盖后假值，不应写入
            FlowLevel = 5,
            FlowTarget = null,
            FlowCRType = null,
            PlanOuterDiameterSpan = null,
            FlowExecSpec = null,
            TargetSequence = null,
            ExecutionSequence = null,
        });

        var resumed = ctx.BatchPlanSchedules.AsNoTracking().Single(x => x.BatchId == batch.Id);
        resumed.IsPaused.Should().BeFalse();
        resumed.IsFlow.Should().BeTrue();      // 保留原流转（未被覆盖后假值破坏）
        resumed.FlowLevel.Should().Be(1);
        resumed.FlowTarget.Should().Be("F");
        resumed.FlowCRType.Should().Be(ProcessKeys.ColdRoll60);
        resumed.PlanOuterDiameterSpan.Should().Be("span");
        resumed.FlowExecSpec.Should().Be("spec");
        resumed.TargetSequence.Should().Be(5);
        resumed.ExecutionSequence.Should().Be(3);

        // 抢单/备注仍可正常保存（不受暂停影响）
        await svc.SaveAsync(new BatchPlanScheduleDto
        {
            BatchId = batch.Id,
            IsGrabOrder = true,
            PlanRemark = "备注",
        });
        var grab = ctx.BatchPlanSchedules.AsNoTracking().Single(x => x.BatchId == batch.Id);
        grab.IsGrabOrder.Should().BeTrue();
        grab.PlanRemark.Should().Be("备注");
        grab.IsFlow.Should().BeTrue(); // 流转字段依旧不受影响
    }
}
