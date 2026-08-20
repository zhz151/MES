using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Scheduling;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Scheduling;
using MES.Tests.Tests;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.WorkOrder;

namespace MES.Tests.Services.Scheduling;

/// <summary>
/// 在产明细计划服务测试：分页查询、工段筛选、关键词搜索、汇总
/// </summary>
public class BatchPlanServiceTests : TestBase
{
    private BatchPlanService CreateService(AppDbContext ctx) => new(ctx, CreateProcessDefinitionServiceMock());

    private ProductionBatch CreateBatch(AppDbContext ctx, string batchNo, string workOrderNo,
        BatchStatus status = BatchStatus.InProgress,
        string? currentGroupName = null, string? currentSectionName = null,
        bool? currentSectionCompleted = null,
        string? nextProcess = null, string? nextSectionName = null,
        int currentValidWeight = 1000)
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
            CurrentValidWeight = currentValidWeight,
            CurrentGroupName = currentGroupName,
            CurrentSectionName = currentSectionName,
            CurrentSectionCompleted = currentSectionCompleted,
            NextProcess = nextProcess,
            NextSectionName = nextSectionName,
            RowVersion = new byte[8],
        };
        ctx.ProductionBatches.Add(batch);
        return batch;
    }

    private void SeedSummary(AppDbContext ctx, string workOrderNo,
        int scheduleStage = 1, string? urgencyLevel = null)
    {
        // Use a deterministic hash as WorkOrderId
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
        });
    }

    // ==================== GetPagedAsync 测试 ====================

    [Fact]
    public async Task GetPagedAsync_返回在产和待产批次()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress);
        CreateBatch(ctx, "B002", "WO001", BatchStatus.None);
        CreateBatch(ctx, "B003", "WO001", BatchStatus.Completed); // 应排除
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetPagedAsync_主号暂停批次排除()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress);
        SeedSummary(ctx, "WO001");
        CreateBatch(ctx, "B002", "WO002", BatchStatus.InProgress);
        SeedSummary(ctx, "WO002");
        await ctx.SaveChangesAsync();
        // 主号暂停：读模型 IsPaused=true → 其批次不参与批次计划
        var pausedSummary = ctx.Set<WorkOrderExecutionSummary>().Single(s => s.WorkOrderNo == "WO002");
        pausedSummary.IsPaused = true;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(1);
        result.Items.Single().BatchNo.Should().Be("B001");
    }

    [Fact]
    public async Task GetPagedAsync_关键字搜索批次号()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "BATCH001", "WO001");
        CreateBatch(ctx, "BATCH002", "WO002");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "BATCH001" });

        result.TotalCount.Should().Be(1);
        result.Items.Single().BatchNo.Should().Be("BATCH001");
    }

    [Fact]
    public async Task GetPagedAsync_关键字搜索工单号()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001");
        CreateBatch(ctx, "B002", "WO002");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "WO002" });

        result.TotalCount.Should().Be(1);
        result.Items.Single().BatchNo.Should().Be("B002");
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索紧急级别()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001");
        CreateBatch(ctx, "B002", "WO002");
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: UrgencyLevelKeys.AUrgent);
        SeedSummary(ctx, "WO002", scheduleStage: 1, urgencyLevel: UrgencyLevelKeys.BOrder);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = UrgencyLevelKeys.AUrgent });

        // Should only find B001 (linked to WO001 with A急)
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_分页正确()
    {
        using var ctx = CreateDbContext();
        for (int i = 1; i <= 5; i++)
            CreateBatch(ctx, $"B{i:D3}", $"WO{i:D3}");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var page1 = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 2, SortBy = "BatchNo", IsDescending = false });
        var page2 = await svc.GetPagedAsync(new QueryParams { PageIndex = 2, PageSize = 2, SortBy = "BatchNo", IsDescending = false });

        page1.TotalCount.Should().Be(5);
        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(2);
        page1.Items[0].BatchNo.Should().Be("B001");
    }

    [Fact]
    public async Task GetPagedAsync_空表返回空()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_关联Summary数据()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001");
        SeedSummary(ctx, "WO001", scheduleStage: 2);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var item = result.Items.Single();
        // summary 5 档(2=原料锁定) 映射为排程 4 档(1)
        item.ScheduleStage.Should().Be(1); // From WorkOrderExecutionSummary
    }

    [Fact]
    public async Task GetPagedAsync_工段筛选冷轧类()
    {
        using var ctx = CreateDbContext();

        // 批次在60冷轧工序，冷轧拔工段，未完成
        var batch = CreateBatch(ctx, "B001", "WO001",
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false);
        // 需要 ProcessGroup 数据支持检查
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.ColdRoll60,
            SequenceNumber = 1,
            ColdRollDraw = 1,
        });

        await ctx.SaveChangesAsync();

        // 使用 __SectionTab = "60冷轧" 进行工段筛选
        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "__SectionTab", Value = "60冷轧" }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items.Single().BatchNo.Should().Be("B001");
    }

    [Fact]
    public async Task GetPagedAsync_断切筛选_不误匹配油管断()
    {
        using var ctx = CreateDbContext();
        // 断切工段批次（英文 Key "Cut"）
        CreateBatch(ctx, "B001", "WO001",
            currentSectionName: SectionKeys.Cut,
            currentSectionCompleted: false);
        // 油管断工段批次（英文 Key "OilPipeCut"），含 "Cut" 子串，不得被"断切"筛出
        CreateBatch(ctx, "B002", "WO002",
            currentSectionName: SectionKeys.OilPipeCut,
            currentSectionCompleted: false);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "__SectionTab", Value = "断切" }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items.Single().BatchNo.Should().Be("B001");
    }

    [Fact]
    public async Task GetPagedAsync_Extras包含汇总数据()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001", currentValidWeight: 500);
        CreateBatch(ctx, "B002", "WO002", currentValidWeight: 1500);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Extras.Should().ContainKey("batchCount");
        result.Extras["batchCount"].Should().Be(2);
        result.Extras.Should().ContainKey("totalWeight");
        result.Extras["totalWeight"].Should().Be(2000m);
    }

    // ==================== GetFilterContextsAsync 测试 ====================

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001");
        CreateBatch(ctx, "B002", "WO002");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetFilterContextsAsync();

        result.Should().ContainKeys("BatchNo", "PlantGrade", "WorkOrderNo", "Specification");
        result["BatchNo"].Should().BeEquivalentTo(new[] { "B001", "B002" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var result = await svc.GetFilterContextsAsync();

        foreach (var kvp in result)
            kvp.Value.Should().BeEmpty();
    }

    // ==================== ComputeTargetSequence 测试（英文 Key 匹配） ====================

    [Fact]
    public void ComputeTargetSequence_英文Key_正确计算()
    {
        var pgs = new List<ProcessGroup>
        {
            new() { ProcessName = ProcessKeys.RoughTubeProcessing, SequenceNumber = 1, Inspection = 5, ColdRollDraw = 2 },
            new() { ProcessName = ProcessKeys.ColdRoll60, SequenceNumber = 2, Inspection = 6, ColdRollDraw = 3 },
        };

        // 成检：取最大 Inspection 工段序号
        BatchPlanService.ComputeTargetSequence(pgs, FlowTargetKeys.Inspection, null).Should().Be(6);
        // 完工冷轧：匹配冷轧类型 + ColdRollDraw 字段值 + 1
        BatchPlanService.ComputeTargetSequence(pgs, FlowTargetKeys.CompletionColdRoll, ProcessKeys.ColdRoll60).Should().Be(4);
        // 冷轧：匹配冷轧类型 + ColdRollDraw 字段值
        BatchPlanService.ComputeTargetSequence(pgs, FlowTargetKeys.ColdRoll, ProcessKeys.ColdRoll60).Should().Be(3);
        // 中文 Key（迁移前存量）不再命中 → null（修复后只认英文 Key）
        BatchPlanService.ComputeTargetSequence(pgs, "成检", null).Should().BeNull();
        // flowTarget 为空 → null
        BatchPlanService.ComputeTargetSequence(pgs, null, null).Should().BeNull();
    }

    // ==================== ScheduleTier / 流转判定测试（英文紧急性 Key） ====================

    [Fact]
    public void BatchPlanDto_ScheduleTier_英文紧急性分级()
    {
        // A急 + 在轧要求=Partial2 → 流转，目标=完工冷轧，等级2
        var dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = UrgencyLevelKeys.APlusUrgent,
            ScheduleStage = 2,
            CR_CompletionType = "Partial2",
        };
        dto.IsFlow.Should().BeTrue();
        dto.FlowTarget.Should().Be(FlowTargetKeys.CompletionColdRoll);
        dto.ScheduleTier.Should().Be(3); // A急 + 非正常流转（无 ProductionFlowProperty）→ 急-

        // B顺 + 在轧要求=Partial3 → 流转，等级3
        dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = UrgencyLevelKeys.BOrder,
            ScheduleStage = 2,
            CR_CompletionType = "Partial3",
        };
        dto.IsFlow.Should().BeTrue();
        dto.ScheduleTier.Should().Be(4); // B顺 + Partial3 → 顺

        // C缓 + 在轧要求=Partial3 → 不满足 isPartial3 → 不流转，等级4(略)
        dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = UrgencyLevelKeys.CSlow,
            ScheduleStage = 2,
            CR_CompletionType = "Partial3",
        };
        dto.IsFlow.Should().BeFalse();
        dto.ScheduleTier.Should().Be(6); // 不流转 → 略

        // V5.35 实时重点兜底：重点生产批次（IsKeyBatch）且冷轧排程未命中 → 按主号关注工序兜底流转，等级2(急)
        dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = UrgencyLevelKeys.CSlow,
            ScheduleStage = 2,
            IsKeyBatch = true,
        };
        dto.IsFlow.Should().BeTrue();
        dto.ScheduleTier.Should().Be(2); // V5.35 重点兜底 → 急
        dto.FlowCRType.Should().Be(ProcessKeys.ColdRoll60);
        dto.FlowTarget.Should().Be(FlowTargetKeys.ColdRoll);
    }

    [Fact]
    public void BatchPlanDto_ScheduleTier_四档合并与中文显示()
    {
        // B顺 + All 档 → 流转，档位顺(4)
        var dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = UrgencyLevelKeys.BOrder,
            ScheduleStage = 2,
            CR_CompletionType = "All",
        };
        dto.IsFlow.Should().BeTrue();
        dto.ScheduleTier.Should().Be(4); // B顺 → 顺
        dto.ScheduleTierDisplay.Should().Be("顺");

        // C缓 + All 档 → 流转，档位带(5)
        dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = UrgencyLevelKeys.CSlow,
            ScheduleStage = 2,
            CR_CompletionType = "All",
        };
        dto.IsFlow.Should().BeTrue();
        dto.ScheduleTier.Should().Be(5); // C缓 + All → 带
        dto.ScheduleTierDisplay.Should().Be("带");

        // A急 + All 档 → 档位急-(3)（非正常流转，无 ProductionFlowProperty）
        dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = UrgencyLevelKeys.AUrgent,
            ScheduleStage = 2,
            CR_CompletionType = "All",
        };
        dto.ScheduleTier.Should().Be(3); // 急-（非正常流转）
        dto.ScheduleTierDisplay.Should().Be("急-");

        // 非流转 → 等级4(略)
        dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = UrgencyLevelKeys.CSlow,
            ScheduleStage = 2,
            CR_CompletionType = null,
        };
        dto.ScheduleTier.Should().Be(6); // 不流转 → 略
        dto.ScheduleTierDisplay.Should().Be("略");

        // 重点批次（IsKeyBatch）+ 流转 + 非急单 → 档位带(5)，IsKeyBatch 不把档位拉高
        dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = UrgencyLevelKeys.CSlow,
            ScheduleStage = 2,
            IsKeyBatch = true,
            CR_CompletionType = "All",
            CurrentSectionCompleted = false,
            CurrentGroupName = ProcessKeys.ColdRoll60,
        };
        dto.IsFlow.Should().BeTrue();
        dto.ScheduleTier.Should().Be(5); // C缓 + All → 带
        dto.ScheduleTierDisplay.Should().Be("带");

        dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = UrgencyLevelKeys.CSlow,
            ScheduleStage = 2,
            IsKeyBatch = true,
            CR_CompletionType = "All",
            CurrentSectionCompleted = false,
            CurrentGroupName = ProcessKeys.ColdDraw,
        };
        dto.IsFlow.Should().BeTrue();
        dto.ScheduleTier.Should().Be(5); // C缓 + All → 带
        dto.ScheduleTierDisplay.Should().Be("带");

        // 重点批次 + 流转 + 急单 → 档位急-(3)，IsKeyBatch 不影响急档（非正常流转）
        dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = UrgencyLevelKeys.APlusUrgent,
            ScheduleStage = 2,
            IsKeyBatch = true,
            CR_CompletionType = "All",
        };
        dto.IsFlow.Should().BeTrue();
        dto.ScheduleTier.Should().Be(3); // 急-（非正常流转）
        dto.ScheduleTierDisplay.Should().Be("急-");
    }

    [Fact]
    public void BatchPlanDto_PlanFlowLevelDisplay_薄表五档()
    {
        // 薄表 PlanFlowLevel（V5.28 五档：1=急+ 2=急 3=急- 4=一般 5=略，特急A/B 手工档已删除，急+ 直接透传实时档位）
        var dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            PlanFlowLevel = 1,
            PlanFlowCRType = ProcessKeys.ColdRoll60,
        };
        dto.PlanFlowLevelDisplay.Should().Be("急+");

        dto = new BatchPlanDto
        {
            PlanFlowLevel = 2,
        };
        dto.PlanFlowLevelDisplay.Should().Be("急");

        dto = new BatchPlanDto
        {
            PlanFlowLevel = 3,
        };
        dto.PlanFlowLevelDisplay.Should().Be("急-");

        dto = new BatchPlanDto
        {
            PlanFlowLevel = 4,
        };
        dto.PlanFlowLevelDisplay.Should().Be("一般");

        dto = new BatchPlanDto
        {
            PlanFlowLevel = 5,
        };
        dto.PlanFlowLevelDisplay.Should().Be("略");
    }

    [Fact]
    public void BatchPlanDto_IsFlow_中文紧急性Key不命中_安全兜底()
    {
        // 迁移前中文存量（"A急"）不命中英文 Key 判定 → 不流转，避免误判（存量应由读模型归一，此为兜底行为）
        var dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = "A急",
            ScheduleStage = 2,
            CR_CompletionType = "Partial2",
        };
        dto.IsFlow.Should().BeFalse();
        dto.ScheduleTier.Should().Be(6); // 不流转 → 略
    }

    [Fact]
    public void BatchPlanDto_IsFlow_关注工序为空_仅按冷轧排程档位判定()
    {
        // 收尾阶段（关注工序为空）不再兜底"流转-成检"；仅按冷轧排程档位判定（与排程保存判定一致）
        // 无排程档位 → 不流转
        var dto = new BatchPlanDto
        {
            MainNoAttentionProcess = null,
            UrgencyLevel = UrgencyLevelKeys.APlusUrgent,
            ScheduleStage = 2,
            CR_CompletionType = null,
            CR_RollType = null,
        };
        dto.IsFlow.Should().BeFalse();
        dto.FlowTarget.Should().BeNull();
        dto.ScheduleTier.Should().Be(6); // 不流转 → 略

        // 有排程档位（在轧要求=All）→ 仍按档位流转，目标=完工冷轧
        dto = new BatchPlanDto
        {
            MainNoAttentionProcess = null,
            UrgencyLevel = UrgencyLevelKeys.APlusUrgent,
            ScheduleStage = 2,
            CR_CompletionType = "All",
        };
        dto.IsFlow.Should().BeTrue();
        dto.FlowTarget.Should().Be(FlowTargetKeys.CompletionColdRoll);
    }

    [Fact]
    public void BatchPlanDto_IsFlow_Urgent档_正常流转特急才流转()
    {
        // Urgent 档位 = isUrgent(特急) && isNormal(正常流转)；不再认 IsKeyBatch/IsGeneralKeyBatch（Model B）
        // 在轧侧：A+急 + 非正常流转 → 不流转
        var dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = UrgencyLevelKeys.APlusUrgent,
            ProductionFlowProperty = ProductionFlowKeys.Waiting,
            IsKeyBatch = false,
            CR_CompletionType = "Urgent",
        };
        dto.IsFlow.Should().BeFalse();
        dto.ScheduleTier.Should().Be(6); // 不流转 → 略

        // 即使催单/分批交货为真，排程档位仍不触发（与冷轧计划排程选中口径一致）；但 V5.35 重点兜底：
        // 重点生产批次（IsKeyBatch）且排程档位未命中（Urgent 因非正常流转不生效）→ 实时按重点兜底流转
        dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = UrgencyLevelKeys.APlusUrgent,
            ProductionFlowProperty = null,
            ScheduleStage = 1,
            IsUrging = true,
            IsBatchDelivery = true,
            IsKeyBatch = true,
            CR_CompletionType = "Urgent",
        };
        dto.IsFlow.Should().BeTrue();
        dto.ScheduleTier.Should().Be(2); // V5.35 重点兜底 → 急
        dto.FlowCRType.Should().Be(ProcessKeys.ColdRoll60);
        dto.FlowTarget.Should().Be(FlowTargetKeys.ColdRoll);

        // 在轧侧 Urgent 档：A+急 + 正常流转 → 流转（不论 IsKeyBatch），等级2急
        dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = UrgencyLevelKeys.APlusUrgent,
            ProductionFlowProperty = ProductionFlowKeys.Normal,
            IsKeyBatch = false,
            CR_CompletionType = "Urgent",
        };
        dto.IsFlow.Should().BeTrue();
        dto.FlowTarget.Should().Be(FlowTargetKeys.CompletionColdRoll);
        dto.ScheduleTier.Should().Be(2); // A急 + 正常流转 → 急

        // 待轧侧 Urgent 档：A+急 + 正常流转 → 流转（不论 IsGeneralKeyBatch，非冷轧关注也流转）
        dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.RoughTubeProcessing,
            UrgencyLevel = UrgencyLevelKeys.APlusUrgent,
            ProductionFlowProperty = ProductionFlowKeys.Normal,
            CR_RollType = "Urgent",
        };
        dto.IsFlow.Should().BeTrue();
        dto.FlowTarget.Should().Be(FlowTargetKeys.ColdRoll);
        dto.ScheduleTier.Should().Be(2); // A急 + 正常流转 → 急（非冷轧关注）
    }

    [Fact]
    public void BatchPlanDto_IsFlow_CrOnly严格档_关注当前冷轧才流转()
    {
        // CrOnly(特急严格档) = isUrgent && isNormal && AttentionMatchesCurrentCR
        // 在轧侧：关注工序 == 当前冷轧行 → 流转
        var dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = UrgencyLevelKeys.APlusUrgent,
            ProductionFlowProperty = ProductionFlowKeys.Normal,
            AttentionMatchesCurrentCR = true,
            CR_CompletionType = "CrOnly",
        };
        dto.IsFlow.Should().BeTrue();
        dto.FlowTarget.Should().Be(FlowTargetKeys.CompletionColdRoll);
        dto.ScheduleTier.Should().Be(1); // CrOnly + 关注匹配 → 急+

        // 在轧侧：关注工序 != 当前冷轧行 → 不流转
        dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = UrgencyLevelKeys.APlusUrgent,
            ProductionFlowProperty = ProductionFlowKeys.Normal,
            AttentionMatchesCurrentCR = false,
            CR_CompletionType = "CrOnly",
        };
        dto.IsFlow.Should().BeFalse();
        dto.ScheduleTier.Should().Be(6); // 不流转 → 略

        // 待轧侧 CrOnly 同理：关注 == 当前冷轧 → 流转
        dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = UrgencyLevelKeys.APlusUrgent,
            ProductionFlowProperty = ProductionFlowKeys.Normal,
            AttentionMatchesCurrentCR = true,
            CR_RollType = "CrOnly",
        };
        dto.IsFlow.Should().BeTrue();
        dto.FlowTarget.Should().Be(FlowTargetKeys.ColdRoll);
        dto.ScheduleTier.Should().Be(1); // CrOnly + 关注匹配 → 急+

        // 待轧侧 CrOnly：关注 != 当前冷轧 → 不流转
        dto = new BatchPlanDto
        {
            MainNoAttentionProcess = ProcessKeys.ColdRoll60,
            UrgencyLevel = UrgencyLevelKeys.APlusUrgent,
            ProductionFlowProperty = ProductionFlowKeys.Normal,
            AttentionMatchesCurrentCR = false,
            CR_RollType = "CrOnly",
        };
        dto.IsFlow.Should().BeFalse();
        dto.ScheduleTier.Should().Be(6); // 不流转 → 略
    }

    [Fact]
    public void BatchPlanDto_ScheduleTier_六档判定()
    {
        // 略（6）：不在排程内（IsFlow=false）——Urgent 档 + 非正常流转
        var dto = new BatchPlanDto
        {
            UrgencyLevel = UrgencyLevelKeys.APlusUrgent,
            ProductionFlowProperty = ProductionFlowKeys.Waiting,
            CR_CompletionType = "Urgent",
        };
        dto.IsFlow.Should().BeFalse();
        dto.ScheduleTier.Should().Be(6);
        dto.ScheduleTierDisplay.Should().Be("略");

        // 急+（1）：正常流转∧关注==当前冷轧——CrOnly 档
        dto = new BatchPlanDto
        {
            UrgencyLevel = UrgencyLevelKeys.APlusUrgent,
            ProductionFlowProperty = ProductionFlowKeys.Normal,
            AttentionMatchesCurrentCR = true,
            CR_CompletionType = "CrOnly",
        };
        dto.IsFlow.Should().BeTrue();
        dto.ScheduleTier.Should().Be(1);
        dto.ScheduleTierDisplay.Should().Be("急+");

        // 急（2）：正常流转∧关注≠当前冷轧——Urgent 档
        dto = new BatchPlanDto
        {
            UrgencyLevel = UrgencyLevelKeys.AUrgent,
            ProductionFlowProperty = ProductionFlowKeys.Normal,
            AttentionMatchesCurrentCR = false,
            CR_CompletionType = "Urgent",
        };
        dto.IsFlow.Should().BeTrue();
        dto.ScheduleTier.Should().Be(2);
        dto.ScheduleTierDisplay.Should().Be("急");

        // 急-（3）：非正常流转——Partial2 档（A+急/A急）
        dto = new BatchPlanDto
        {
            UrgencyLevel = UrgencyLevelKeys.APlusUrgent,
            ProductionFlowProperty = ProductionFlowKeys.Waiting,
            CR_CompletionType = "Partial2",
        };
        dto.IsFlow.Should().BeTrue();
        dto.ScheduleTier.Should().Be(3);
        dto.ScheduleTierDisplay.Should().Be("急-");

        // 顺（4）：非急但流转（B顺）——Partial3 档
        dto = new BatchPlanDto
        {
            UrgencyLevel = UrgencyLevelKeys.BOrder,
            ProductionFlowProperty = ProductionFlowKeys.Normal,
            CR_CompletionType = "Partial3",
        };
        dto.IsFlow.Should().BeTrue();
        dto.ScheduleTier.Should().Be(4);
        dto.ScheduleTierDisplay.Should().Be("顺");

        // 带（5）：All 档下非急非顺的普通批次
        dto = new BatchPlanDto
        {
            UrgencyLevel = null,
            ProductionFlowProperty = ProductionFlowKeys.Normal,
            CR_RollType = "All",
        };
        dto.IsFlow.Should().BeTrue();
        dto.ScheduleTier.Should().Be(5);
        dto.ScheduleTierDisplay.Should().Be("带");
    }

    // ==================== 主列表 GetPagedAsync 目标序计算测试 ====================

    [Fact]
    public async Task GetPagedAsync_流转批次_目标序计算()
    {
        using var ctx = CreateDbContext();
        var batch = CreateBatch(ctx, "B001", "WO001",
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false);
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.ColdRoll60,
            SequenceNumber = 1,
            ColdRollDraw = 1,
            ManufacturingSpec = "89*8",
        });
        // Summary：主号关注工序=ColdRoll60（冷轧）、流转性=正常；ScheduleStage=3 → 排程 4 档 2（生产执行）
        SeedSummary(ctx, "WO001",
            scheduleStage: 3,
            urgencyLevel: UrgencyLevelKeys.APlusUrgent);
        await ctx.SaveChangesAsync();
        var summary = ctx.Set<WorkOrderExecutionSummary>().First(s => s.WorkOrderNo == "WO001");
        summary.MainNoAttentionProcess = ProcessKeys.ColdRoll60;
        summary.ProductionFlowProperty = ProductionFlowKeys.Normal;

        // 冷轧排程小表：待轧要求=All（全量冷轧），匹配键 ColdRoll60||89*8|True
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = ProcessKeys.ColdRoll60,
            BilletSpec = "",
            RollingSpec = "89*8",
            IsFinished = true,
            CompletionType = "None",
            RollType = "All",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var item = result.Items.Single();
        item.IsFlow.Should().BeTrue();
        item.FlowTarget.Should().Be(FlowTargetKeys.ColdRoll);
        item.FlowCRType.Should().Be(ProcessKeys.ColdRoll60);
        item.TargetSequence.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_本层冷轧无排程档位_下层有档位_在下层匹配流转()
    {
        using var ctx = CreateDbContext();
        // 工序组：RoughTubeProcessing(67*5.5) → ColdRoll50(38*3.2) → ColdRoll30(25*2.5)
        // 当前已过 ColdRoll50 工段（CurrentSectionCompleted=true）→ 待轧工序=NextProcess=ColdRoll50（本层冷轧）
        var batch = CreateBatch(ctx, "B001", "WO001",
            currentGroupName: ProcessKeys.ColdRoll50,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: true,
            nextProcess: ProcessKeys.ColdRoll50,
            nextSectionName: SectionKeys.ColdRollDraw);
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.RoughTubeProcessing,
            SequenceNumber = 1,
            ColdRollDraw = 2,
            ManufacturingSpec = "67*5.5",
        });
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.ColdRoll50,
            SequenceNumber = 2,
            ColdRollDraw = 2,
            ManufacturingSpec = "38*3.2",
        });
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.ColdRoll30,
            SequenceNumber = 3,
            ColdRollDraw = 2,
            ManufacturingSpec = "25*2.5",
        });
        // A+急 + 非正常流转（Waiting）→ 急-档；Partial2 档只需 isUrgent → 流转
        SeedSummary(ctx, "WO001", scheduleStage: 3, urgencyLevel: UrgencyLevelKeys.APlusUrgent);
        await ctx.SaveChangesAsync();
        var summary = ctx.Set<WorkOrderExecutionSummary>().First(s => s.WorkOrderNo == "WO001");
        summary.MainNoAttentionProcess = ProcessKeys.ColdRoll50;
        summary.ProductionFlowProperty = ProductionFlowKeys.Waiting;
        await ctx.SaveChangesAsync();

        // 排程表：本层 ColdRoll50(67*5.5→38*3.2) 无档位；下层 ColdRoll30(38*3.2→25*2.5) 有待轧要求=Partial2
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = ProcessKeys.ColdRoll30,
            BilletSpec = "38*3.2",
            RollingSpec = "25*2.5",
            IsFinished = true,
            CompletionType = "None",
            RollType = "Partial2",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        // 本层无档位不锁死 → 在下层 ColdRoll30 匹配 Partial2 → 急-批次正确流转
        var item = result.Items.Single();
        item.CurrentCR_ProcessType.Should().Be(ProcessKeys.ColdRoll50);
        item.NextCR_ProcessType.Should().Be(ProcessKeys.ColdRoll30);
        item.CR_RollType.Should().Be("Partial2");
        item.IsFlow.Should().BeTrue();
        // 流转显示应指向实际匹配层（下层），而非本层
        item.FlowCRType.Should().Be(ProcessKeys.ColdRoll30);
        item.FlowExecSpec.Should().Be("25*2.5");
        item.OuterDiameterSpan.Should().Be("38-25");
    }

    [Fact]
    public async Task GetPagedAsync_本层档位生效_停在生效层()
    {
        using var ctx = CreateDbContext();
        var batch = CreateBatch(ctx, "B001", "WO001",
            currentGroupName: ProcessKeys.ColdRoll50,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false);
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.ColdRoll50,
            SequenceNumber = 1,
            ColdRollDraw = 2,
            ManufacturingSpec = "38*3.2",
        });
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.ColdRoll30,
            SequenceNumber = 2,
            ColdRollDraw = 2,
            ManufacturingSpec = "25*2.5",
        });
        SeedSummary(ctx, "WO001", scheduleStage: 3, urgencyLevel: UrgencyLevelKeys.APlusUrgent);
        await ctx.SaveChangesAsync();
        var summary = ctx.Set<WorkOrderExecutionSummary>().First(s => s.WorkOrderNo == "WO001");
        summary.ProductionFlowProperty = ProductionFlowKeys.Waiting;
        await ctx.SaveChangesAsync();

        // 本层与下层都有 Partial2 档位 → 本层档位对该批次生效即停（V5.34 去锁定：生效才停，不回退下层）
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = ProcessKeys.ColdRoll50,
            BilletSpec = "",
            RollingSpec = "38*3.2",
            IsFinished = false,
            CompletionType = "None",
            RollType = "Partial2",
        });
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = ProcessKeys.ColdRoll30,
            BilletSpec = "38*3.2",
            RollingSpec = "25*2.5",
            IsFinished = true,
            CompletionType = "None",
            RollType = "Partial2",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var item = result.Items.Single();
        item.CR_RollType.Should().Be("Partial2");
        item.IsFlow.Should().BeTrue();
        item.FlowCRType.Should().Be(ProcessKeys.ColdRoll50);
        item.FlowExecSpec.Should().Be("38*3.2");
    }

    [Fact]
    public async Task GetPagedAsync_本层冷轧拔已轧过_待轧跳下层_下层档位生效流转()
    {
        using var ctx = CreateDbContext();
        // 工序组：RoughTubeProcessing(67*5.5) → ColdRoll50(38*3.2) → ColdRoll30(25*2.5)
        // 当前已过 ColdRoll50 冷轧拔工段（当前工段=冷轧拔已完工）→ 本层 ColdRoll50 不是下一个冷轧拔层
        var batch = CreateBatch(ctx, "B001", "WO001",
            currentGroupName: ProcessKeys.ColdRoll50,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: true,
            nextProcess: ProcessKeys.ColdRoll50,
            nextSectionName: SectionKeys.ColdRollDraw);
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.RoughTubeProcessing,
            SequenceNumber = 1,
            ColdRollDraw = 2,
            ManufacturingSpec = "67*5.5",
        });
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.ColdRoll50,
            SequenceNumber = 2,
            ColdRollDraw = 2,
            ManufacturingSpec = "38*3.2",
        });
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.ColdRoll30,
            SequenceNumber = 3,
            ColdRollDraw = 2,
            ManufacturingSpec = "25*2.5",
        });
        // 非急批次（CSlow）+ 正常流转 → Partial2/All 均不区分，All 全量命中
        SeedSummary(ctx, "WO001", scheduleStage: 3, urgencyLevel: UrgencyLevelKeys.CSlow);
        await ctx.SaveChangesAsync();
        var summary = ctx.Set<WorkOrderExecutionSummary>().First(s => s.WorkOrderNo == "WO001");
        summary.MainNoAttentionProcess = ProcessKeys.ColdRoll50;
        summary.ProductionFlowProperty = ProductionFlowKeys.Normal;
        await ctx.SaveChangesAsync();

        // 排程表：本层 ColdRoll50(67*5.5→38*3.2) 有档位 Partial2——但本层冷轧拔已轧过，不是下一个冷轧拔层 → 跳过；
        // 下层 ColdRoll30(38*3.2→25*2.5) 有档位 All（全量）→ 在下层流转
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = ProcessKeys.ColdRoll50,
            BilletSpec = "67*5.5",
            RollingSpec = "38*3.2",
            IsFinished = false,
            CompletionType = "None",
            RollType = "Partial2",
        });
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = ProcessKeys.ColdRoll30,
            BilletSpec = "38*3.2",
            RollingSpec = "25*2.5",
            IsFinished = true,
            CompletionType = "None",
            RollType = "All",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        // 本层冷轧拔已轧过 → 本层档位（Partial2）不参与；待轧跳下层 ColdRoll30 → All 全量 → 流转
        var item = result.Items.Single();
        item.CurrentCR_ProcessType.Should().Be(ProcessKeys.ColdRoll50); // 本层显示仍=当前工序组
        item.NextCR_ProcessType.Should().Be(ProcessKeys.ColdRoll30);
        item.CR_RollType.Should().Be("All");
        item.IsFlow.Should().BeTrue();
        // 流转显示指向实际匹配层（下层），而非本层
        item.FlowCRType.Should().Be(ProcessKeys.ColdRoll30);
        item.FlowExecSpec.Should().Be("25*2.5");
        // 变形序完成=完成（本层冷轧拔已轧过）→ 冷轧排程(实时)取下层的规格信息
        item.CurrentCR_DeformedSeqCompleted.Should().BeTrue();
        item.RealTimeCR_ProcessType.Should().Be(ProcessKeys.ColdRoll30);
        item.RealTimeCR_RollingSpec.Should().Be("25*2.5");
        item.RealTimeCR_BilletSpec.Should().Be("38*3.2");
    }

    [Fact]
    public async Task GetPagedAsync_本层冷轧拔已完工_本层在轧_不在轧匹配本层_转待轧下一冷轧拔层流转()
    {
        using var ctx = CreateDbContext();
        // 工序组：ColdRoll50(38*3.2, 冷轧拔=1, 酸洗=2) → ColdRoll30(25*2.5, 冷轧拔=2)
        // 当前在 ColdRoll50 组内酸洗在轧（酸洗在冷轧拔之后）→ 本层冷轧拔已完工但本层仍在轧（PendingEquipment=M1）
        var batch = CreateBatch(ctx, "B001", "WO001",
            currentGroupName: ProcessKeys.ColdRoll50,
            currentSectionName: SectionKeys.Pickle,
            currentSectionCompleted: false);
        batch.CurrentEquipmentName = "M1";
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.ColdRoll50,
            SequenceNumber = 1,
            ColdRollDraw = 1,
            Pickle = 2,
            ManufacturingSpec = "38*3.2",
        });
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.ColdRoll30,
            SequenceNumber = 2,
            ColdRollDraw = 2,
            ManufacturingSpec = "25*2.5",
        });
        // 非急批次（CSlow）+ 正常流转 → Partial2 不生效、All 全量生效
        SeedSummary(ctx, "WO001", scheduleStage: 3, urgencyLevel: UrgencyLevelKeys.CSlow);
        await ctx.SaveChangesAsync();
        var summary = ctx.Set<WorkOrderExecutionSummary>().First(s => s.WorkOrderNo == "WO001");
        summary.MainNoAttentionProcess = ProcessKeys.ColdRoll50;
        summary.ProductionFlowProperty = ProductionFlowKeys.Normal;
        await ctx.SaveChangesAsync();

        // 排程表：本层 ColdRoll50 有在轧要求 Partial2——但本层冷轧拔已完工（V5.35 在轧对齐），
        // 不在轧匹配本层 CompletionType，转待轧逐层 → 本层 IsColdRollPassDone=true 跳过 → 下层 ColdRoll30 All → 流转
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = ProcessKeys.ColdRoll50,
            BilletSpec = "76*6.5",
            RollingSpec = "38*3.2",
            IsFinished = false,
            CompletionType = "Partial2",
            RollType = "None",
        });
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = ProcessKeys.ColdRoll30,
            BilletSpec = "38*3.2",
            RollingSpec = "25*2.5",
            IsFinished = true,
            CompletionType = "None",
            RollType = "All",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var item = result.Items.Single();
        item.CurrentCR_ProcessType.Should().Be(ProcessKeys.ColdRoll50);
        // 本层冷轧拔已完工且本层在轧 → 不在轧匹配本层 CompletionType（V5.35）
        item.CR_CompletionType.Should().BeNull();
        // 转待轧逐层 → 本层跳过、下层 ColdRoll30 All → 流转
        item.CR_RollType.Should().Be("All");
        item.IsFlow.Should().BeTrue();
        item.FlowCRType.Should().Be(ProcessKeys.ColdRoll30);
        item.FlowExecSpec.Should().Be("25*2.5");
        item.CurrentCR_DeformedSeqCompleted.Should().BeTrue();
        item.RealTimeCR_ProcessType.Should().Be(ProcessKeys.ColdRoll30);
        item.RealTimeCR_RollingSpec.Should().Be("25*2.5");
    }

    [Fact]
    public async Task GetPagedAsync_本层档位不生效_去锁定跳下层_下层生效流转()
    {
        using var ctx = CreateDbContext();
        // 工序组：ColdRoll50(38*3.2, 酸洗=1, 冷轧拔=2) → ColdRoll30(25*2.5, 冷轧拔=2)
        // 当前在 ColdRoll50 组内酸洗（冷轧拔之前）→ 本层 ColdRoll50 就是批次的「下一个冷轧拔层」（未轧过）
        var batch = CreateBatch(ctx, "B001", "WO001",
            currentGroupName: ProcessKeys.ColdRoll50,
            currentSectionName: SectionKeys.Pickle,
            currentSectionCompleted: false);
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.ColdRoll50,
            SequenceNumber = 1,
            Pickle = 1,
            ColdRollDraw = 2,
            ManufacturingSpec = "38*3.2",
        });
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.ColdRoll30,
            SequenceNumber = 2,
            ColdRollDraw = 2,
            ManufacturingSpec = "25*2.5",
        });
        // 急-批次（A+急 + Waiting）→ Urgent 档需正常流转，不生效
        SeedSummary(ctx, "WO001", scheduleStage: 3, urgencyLevel: UrgencyLevelKeys.APlusUrgent);
        await ctx.SaveChangesAsync();
        var summary = ctx.Set<WorkOrderExecutionSummary>().First(s => s.WorkOrderNo == "WO001");
        summary.MainNoAttentionProcess = ProcessKeys.ColdRoll50;
        summary.ProductionFlowProperty = ProductionFlowKeys.Waiting;
        await ctx.SaveChangesAsync();

        // 排程表：本层 ColdRoll50 有档位 Urgent（对急-不生效）；下层 ColdRoll30 有档位 All（全量）——
        // V5.34 去锁定：本层档位不生效 → 不锁定、继续下层 → All 全量命中 → 流转（与排程侧每层独立一致）
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = ProcessKeys.ColdRoll50,
            BilletSpec = "",
            RollingSpec = "38*3.2",
            IsFinished = false,
            CompletionType = "None",
            RollType = "Urgent",
        });
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = ProcessKeys.ColdRoll30,
            BilletSpec = "38*3.2",
            RollingSpec = "25*2.5",
            IsFinished = true,
            CompletionType = "None",
            RollType = "All",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        // 本层档位(Urgent)不生效 → 跳下层 ColdRoll30 → All 全量命中 → 流转
        var item = result.Items.Single();
        item.CR_RollType.Should().Be("All");                  // 匹配层=下层（本层 Urgent 不生效被覆盖）
        item.IsFlow.Should().BeTrue();
        item.FlowCRType.Should().Be(ProcessKeys.ColdRoll50);  // 显示层=物理本层（V5.33，与实时组一致）
        item.FlowExecSpec.Should().Be("38*3.2");
        item.OuterDiameterSpan.Should().BeNull();             // 本层为第一道冷轧（无来料规格），跨度空
        item.FlowTarget.Should().Be(FlowTargetKeys.ColdRoll);
        item.TargetSequence.Should().Be(2);                   // 本层冷轧拔工段序
        // 变形序完成=否（当前在酸洗、冷轧拔之前）→ 冷轧排程(实时)取本层的规格信息
        item.CurrentCR_DeformedSeqCompleted.Should().BeFalse();
        item.RealTimeCR_ProcessType.Should().Be(ProcessKeys.ColdRoll50);
        item.RealTimeCR_RollingSpec.Should().Be("38*3.2");
        item.RealTimeCR_BilletSpec.Should().BeNull(); // 本层为第一道冷轧（无前序工序），无来料规格
    }

    [Fact]
    public async Task GetPagedAsync_本层未轧过无档位_下层有档位_流转显示物理本层()
    {
        using var ctx = CreateDbContext();
        // 工序组：ColdRoll50(38*3.2, 酸洗=1, 冷轧拔=2) → ColdRoll30(25*2.5, 冷轧拔=2)
        // 当前在 ColdRoll50 组内酸洗（冷轧拔之前）→ 本层 ColdRoll50 是「下一个冷轧拔层」（未轧过）
        var batch = CreateBatch(ctx, "B001", "WO001",
            currentGroupName: ProcessKeys.ColdRoll50,
            currentSectionName: SectionKeys.Pickle,
            currentSectionCompleted: false);
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.ColdRoll50,
            SequenceNumber = 1,
            Pickle = 1,
            ColdRollDraw = 2,
            ManufacturingSpec = "38*3.2",
        });
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.ColdRoll30,
            SequenceNumber = 2,
            ColdRollDraw = 2,
            ManufacturingSpec = "25*2.5",
        });
        // 非急批次（CSlow）+ 正常流转 → All 全量命中
        SeedSummary(ctx, "WO001", scheduleStage: 3, urgencyLevel: UrgencyLevelKeys.CSlow);
        await ctx.SaveChangesAsync();
        var summary = ctx.Set<WorkOrderExecutionSummary>().First(s => s.WorkOrderNo == "WO001");
        summary.MainNoAttentionProcess = ProcessKeys.ColdRoll50;
        summary.ProductionFlowProperty = ProductionFlowKeys.Normal;
        await ctx.SaveChangesAsync();

        // 排程表：本层 ColdRoll50(38*3.2) 无档位记录；下层 ColdRoll30(25*2.5) 有待轧要求=All（全量）
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = ProcessKeys.ColdRoll30,
            BilletSpec = "38*3.2",
            RollingSpec = "25*2.5",
            IsFinished = true,
            CompletionType = "None",
            RollType = "All",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        // 本层无档位 → 待轧跳下层 ColdRoll30 匹配 All → 流转；但显示层=「冷轧排程(实时)」物理本层（V5.33）
        var item = result.Items.Single();
        item.CurrentCR_ProcessType.Should().Be(ProcessKeys.ColdRoll50);
        item.NextCR_ProcessType.Should().Be(ProcessKeys.ColdRoll30);
        item.CR_RollType.Should().Be("All");                  // 匹配层=下层（IsFlow/机台依据）
        item.IsFlow.Should().BeTrue();
        item.FlowCRType.Should().Be(ProcessKeys.ColdRoll50);  // 显示层=物理本层（与实时组一致）
        item.FlowExecSpec.Should().Be("38*3.2");
        item.OuterDiameterSpan.Should().BeNull();             // 本层为第一道冷轧（无来料规格），跨度空
        item.FlowTarget.Should().Be(FlowTargetKeys.ColdRoll);
        item.TargetSequence.Should().Be(2);                   // 本层冷轧拔工段序
        // 变形序完成=否（当前在酸洗、冷轧拔之前）→ 冷轧排程(实时)取本层的规格信息
        item.CurrentCR_DeformedSeqCompleted.Should().BeFalse();
        item.RealTimeCR_ProcessType.Should().Be(ProcessKeys.ColdRoll50);
        item.RealTimeCR_RollingSpec.Should().Be("38*3.2");
        item.RealTimeCR_BilletSpec.Should().BeNull();
    }

    [Fact]
    public async Task GetPagedAsync_本层非冷轧_变形序完成默认完成_实时取下层的规格()
    {
        using var ctx = CreateDbContext();
        // 工序组：RoughTubeProcessing(67*5.5, 检验=2) → ColdRoll50(38*3.2) → ColdRoll30(25*2.5)
        // 当前在荒管组检验（非冷轧）→ 本层 CurrentCR 为空（无冷轧拔）→ 变形序完成默认完成 → 实时取下层 ColdRoll50
        var batch = CreateBatch(ctx, "B001", "WO001",
            currentGroupName: ProcessKeys.RoughTubeProcessing,
            currentSectionName: SectionKeys.Inspection,
            currentSectionCompleted: false);
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.RoughTubeProcessing,
            SequenceNumber = 1,
            Inspection = 2,
            ManufacturingSpec = "67*5.5",
        });
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.ColdRoll50,
            SequenceNumber = 2,
            ColdRollDraw = 2,
            ManufacturingSpec = "38*3.2",
        });
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.ColdRoll30,
            SequenceNumber = 3,
            ColdRollDraw = 2,
            ManufacturingSpec = "25*2.5",
        });
        SeedSummary(ctx, "WO001", scheduleStage: 3, urgencyLevel: UrgencyLevelKeys.CSlow);
        await ctx.SaveChangesAsync();
        var summary = ctx.Set<WorkOrderExecutionSummary>().First(s => s.WorkOrderNo == "WO001");
        summary.ProductionFlowProperty = ProductionFlowKeys.Normal;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var item = result.Items.Single();
        item.CurrentCR_ProcessType.Should().BeNull(); // 本层（待产工序=荒管）非冷轧
        item.NextCR_ProcessType.Should().Be(ProcessKeys.ColdRoll50);
        // 无本层冷轧拔 → 变形序完成默认完成 → 实时组取下层（下一个冷轧拔层）规格
        item.CurrentCR_DeformedSeqCompleted.Should().BeNull();
        item.RealTimeCR_ProcessType.Should().Be(ProcessKeys.ColdRoll50);
        item.RealTimeCR_BilletSpec.Should().Be("67*5.5");
        item.RealTimeCR_RollingSpec.Should().Be("38*3.2");
        item.RealTimeCR_IsFinished.Should().BeFalse();
    }

    // ==================== 重点生产批次（IsKeyBatch）判定测试 ====================

    [Fact]
    public async Task GetPagedAsync_未产批次_重点生产批次纳入()
    {
        using var ctx = CreateDbContext();
        var batch = CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress);
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.RoughTubeProcessing,
            SequenceNumber = 1,
            Inspection = 2,
        });
        SeedSummary(ctx, "WO001", scheduleStage: 3, urgencyLevel: UrgencyLevelKeys.AUrgent);
        await ctx.SaveChangesAsync();
        var summary = ctx.Set<WorkOrderExecutionSummary>().First(s => s.WorkOrderNo == "WO001");
        summary.MainNoAttentionProcess = ProcessKeys.RoughTubeProcessing;
        summary.ProductionFlowProperty = ProductionFlowKeys.Normal;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        // 未产批次（无当前工序组）：执行序视为 0，0 < 相应工段序(2) → 重点
        var item = result.Items.Single();
        item.ExecutionSequence.Should().BeNull();
        item.AttentionProcessSectionSequence.Should().Be(2);
        item.IsKeyBatch.Should().BeTrue();
    }

    [Fact]
    public async Task GetPagedAsync_生产收尾_急单正常流转_重点生产批次()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress);
        SeedSummary(ctx, "WO001", scheduleStage: 3, urgencyLevel: UrgencyLevelKeys.AUrgent);
        await ctx.SaveChangesAsync();
        var summary = ctx.Set<WorkOrderExecutionSummary>().First(s => s.WorkOrderNo == "WO001");
        summary.MainNoAttentionProcess = ProductionAttentionKeys.Finish;
        summary.ProductionFlowProperty = ProductionFlowKeys.Normal;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        // 生产收尾：变形工序已完成、与成品检验衔接 → 直接重点（不要求序号）
        var item = result.Items.Single();
        item.IsKeyBatch.Should().BeTrue();
    }

    [Fact]
    public async Task GetPagedAsync_生产收尾_非急单_非重点生产批次()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress);
        SeedSummary(ctx, "WO001", scheduleStage: 3, urgencyLevel: UrgencyLevelKeys.BOrder);
        await ctx.SaveChangesAsync();
        var summary = ctx.Set<WorkOrderExecutionSummary>().First(s => s.WorkOrderNo == "WO001");
        summary.MainNoAttentionProcess = ProductionAttentionKeys.Finish;
        summary.ProductionFlowProperty = ProductionFlowKeys.Normal;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        // 生产收尾但非 A+急/A急 → 前置条件不满足
        var item = result.Items.Single();
        item.IsKeyBatch.Should().BeFalse();
    }

    [Fact]
    public async Task GetPagedAsync_生产收尾_流转非正常_非重点生产批次()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress);
        SeedSummary(ctx, "WO001", scheduleStage: 3, urgencyLevel: UrgencyLevelKeys.AUrgent);
        await ctx.SaveChangesAsync();
        var summary = ctx.Set<WorkOrderExecutionSummary>().First(s => s.WorkOrderNo == "WO001");
        summary.MainNoAttentionProcess = ProductionAttentionKeys.Finish;
        summary.ProductionFlowProperty = ProductionFlowKeys.Waiting;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        // 生产收尾但流转性≠正常 → 前置条件不满足
        var item = result.Items.Single();
        item.IsKeyBatch.Should().BeFalse();
    }

    [Fact]
    public async Task GetPagedAsync_在产冷轧批次_序号比较_重点生产批次()
    {
        using var ctx = CreateDbContext();
        var batch = CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false);
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.ColdRoll60,
            SequenceNumber = 1,
            ColdRollDraw = 1,
        });
        SeedSummary(ctx, "WO001", scheduleStage: 3, urgencyLevel: UrgencyLevelKeys.AUrgent);
        await ctx.SaveChangesAsync();
        var summary = ctx.Set<WorkOrderExecutionSummary>().First(s => s.WorkOrderNo == "WO001");
        summary.MainNoAttentionProcess = ProcessKeys.ColdRoll60;
        summary.ProductionFlowProperty = ProductionFlowKeys.Normal;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        // 在产冷轧：执行序 1 < 相应工段序 1+1 → 重点（验证 ExecutionSequence 已提前计算，非生产收尾也判定）
        var item = result.Items.Single();
        item.ExecutionSequence.Should().Be(1);
        item.AttentionProcessSectionSequence.Should().Be(1);
        item.IsKeyBatch.Should().BeTrue();
    }

    [Fact]
    public async Task GetPagedAsync_在产冷轧批次_序号不满足_非重点生产批次()
    {
        using var ctx = CreateDbContext();
        // 在产冷轧执行序 2（断切工段），关注工序仍为冷轧60（相应工段序 1）：2 < 1+1 不成立 → 非重点
        var batch = CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress,
            currentGroupName: ProcessKeys.ColdRoll60,
            currentSectionName: SectionKeys.Cut,
            currentSectionCompleted: false);
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.ColdRoll60,
            SequenceNumber = 1,
            ColdRollDraw = 1,
            Cut = 2,
        });
        SeedSummary(ctx, "WO001", scheduleStage: 3, urgencyLevel: UrgencyLevelKeys.AUrgent);
        await ctx.SaveChangesAsync();
        var summary = ctx.Set<WorkOrderExecutionSummary>().First(s => s.WorkOrderNo == "WO001");
        summary.MainNoAttentionProcess = ProcessKeys.ColdRoll60;
        summary.ProductionFlowProperty = ProductionFlowKeys.Normal;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var item = result.Items.Single();
        item.ExecutionSequence.Should().Be(2);
        item.AttentionProcessSectionSequence.Should().Be(1);
        item.IsKeyBatch.Should().BeFalse();
    }

    [Fact]
    public async Task GetPagedAsync_重点批次排程未命中_实时重点兜底流转()
    {
        using var ctx = CreateDbContext();
        // 未产荒管批次（无当前工序组，执行序视为 0）：0 < 相应工段序(2) → 重点
        var batch = CreateBatch(ctx, "B001", "WO001", BatchStatus.InProgress);
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = ProcessKeys.RoughTubeProcessing,
            SequenceNumber = 1,
            Inspection = 2,
            ManufacturingSpec = "219*8",
        });
        SeedSummary(ctx, "WO001", scheduleStage: 3, urgencyLevel: UrgencyLevelKeys.AUrgent);
        await ctx.SaveChangesAsync();
        var summary = ctx.Set<WorkOrderExecutionSummary>().First(s => s.WorkOrderNo == "WO001");
        summary.MainNoAttentionProcess = ProcessKeys.RoughTubeProcessing;
        summary.ProductionFlowProperty = ProductionFlowKeys.Normal;
        await ctx.SaveChangesAsync();

        // 无冷轧排程记录 → 排程未命中（_trigger=None）→ V5.35 实时重点兜底
        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var item = result.Items.Single();
        item.IsKeyBatch.Should().BeTrue();
        item.IsFlow.Should().BeTrue();                    // 重点兜底流转
        item.ScheduleTier.Should().Be(2);                 // 急
        item.FlowCRType.Should().Be(ProcessKeys.RoughTubeProcessing);   // 冷轧类型=主号关注工序
        item.FlowTarget.Should().Be(FlowTargetKeys.RoughTubeCheck);     // 荒管处理 → 荒管检
        item.FlowExecSpec.Should().Be("219*8");           // 关注工序对应工序组规格
        item.TargetSequence.Should().Be(2);               // 相应工段序（检验工段序）
    }

    // ==================== ComputeAttentionProcessSectionSequence 相应工段序测试 ====================

    private static HashSet<string> ColdRollOrDrawKeys() => new(
        new[]
        {
            ProcessKeys.ColdRoll60, ProcessKeys.ColdRoll50, ProcessKeys.ColdRoll30,
            ProcessKeys.ColdRoll20, ProcessKeys.ThreeRollColdRoll, ProcessKeys.ColdDraw
        },
        StringComparer.Ordinal);

    [Fact]
    public void ComputeAttentionProcessSectionSequence_生产收尾_最大工序组检验工段序减一()
    {
        var pgs = new List<ProcessGroup>
        {
            new() { ProcessName = ProcessKeys.RoughTubeProcessing, SequenceNumber = 1 },
            new() { ProcessName = ProcessKeys.ColdRoll60, SequenceNumber = 2, ColdRollDraw = 1 },
            new() { ProcessName = ProcessKeys.AdditionalFinalInspection, SequenceNumber = 3, Inspection = 3 },
        };

        var result = BatchPlanService.ComputeAttentionProcessSectionSequence(pgs, ProductionAttentionKeys.Finish, ColdRollOrDrawKeys());
        result.Should().Be(2);   // 最大工序组检验 3 - 1
    }

    [Fact]
    public void ComputeAttentionProcessSectionSequence_生产收尾_检验工段序一_取空()
    {
        var pgs = new List<ProcessGroup>
        {
            new() { ProcessName = ProcessKeys.AdditionalFinalInspection, SequenceNumber = 1, Inspection = 1 },
        };

        var result = BatchPlanService.ComputeAttentionProcessSectionSequence(pgs, ProductionAttentionKeys.Finish, ColdRollOrDrawKeys());
        result.Should().BeNull();   // Inspection==1 时取 null
    }

    [Fact]
    public void ComputeAttentionProcessSectionSequence_生产收尾_无检验工段_取空()
    {
        var pgs = new List<ProcessGroup>
        {
            new() { ProcessName = ProcessKeys.RoughTubeProcessing, SequenceNumber = 1 },
            new() { ProcessName = ProcessKeys.ColdRoll60, SequenceNumber = 2, ColdRollDraw = 1 },
        };

        var result = BatchPlanService.ComputeAttentionProcessSectionSequence(pgs, ProductionAttentionKeys.Finish, ColdRollOrDrawKeys());
        result.Should().BeNull();
    }

    [Fact]
    public void ComputeAttentionProcessSectionSequence_荒管处理_取检验工段序()
    {
        var pgs = new List<ProcessGroup>
        {
            new() { ProcessName = ProcessKeys.RoughTubeProcessing, SequenceNumber = 1, Inspection = 2 },
        };

        var result = BatchPlanService.ComputeAttentionProcessSectionSequence(pgs, ProcessKeys.RoughTubeProcessing, ColdRollOrDrawKeys());
        result.Should().Be(2);
    }

    [Fact]
    public void ComputeAttentionProcessSectionSequence_冷轧类_取冷轧拔工段序()
    {
        var pgs = new List<ProcessGroup>
        {
            new() { ProcessName = ProcessKeys.ColdRoll60, SequenceNumber = 1, ColdRollDraw = 1 },
        };

        var result = BatchPlanService.ComputeAttentionProcessSectionSequence(pgs, ProcessKeys.ColdRoll60, ColdRollOrDrawKeys());
        result.Should().Be(1);
    }

    // ========== 跨工段汇总 GetSummaryAsync ==========

    private static BatchPlanSummaryRowDto Row(List<BatchPlanSummaryRowDto> rows, string sectionName)
        => rows.Single(r => r.SectionName == sectionName);

    [Fact]
    public async Task GetSummaryAsync_按工段归桶_合计为全量唯一()
    {
        using var ctx = CreateDbContext();
        // 断切工段批次 + 油管断工段批次（"断切"→Cut 不得子串误匹配 OilPipeCut）
        CreateBatch(ctx, "B001", "WO001",
            currentSectionName: SectionKeys.Cut,
            currentSectionCompleted: false,
            currentValidWeight: 1000);
        CreateBatch(ctx, "B002", "WO002",
            currentSectionName: SectionKeys.OilPipeCut,
            currentSectionCompleted: false,
            currentValidWeight: 2000);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var rows = await svc.GetSummaryAsync();

        rows.Should().HaveCount(BatchPlanSectionTabs.All.Length + 1); // 17 工段 + 合计

        var cut = Row(rows, "断切");
        cut.BatchCount.Should().Be(1);
        cut.TotalWeight.Should().Be(1000m);
        cut.FlowBatchCount.Should().Be(0);
        cut.KeyBatchCount.Should().Be(0);
        cut.Level5Count.Should().Be(1); // 无批次计划薄表 → 等级=略(5)

        var oilPipe = Row(rows, "油管断");
        oilPipe.BatchCount.Should().Be(1);
        oilPipe.TotalWeight.Should().Be(2000m);

        // 合计 = 全量唯一批次（两批次各命中唯一工段，无重叠）
        var total = Row(rows, "合计");
        total.BatchCount.Should().Be(2);
        total.TotalWeight.Should().Be(3000m);
        total.Level5Count.Should().Be(2);
    }

    [Fact]
    public async Task GetSummaryAsync_流转与重点批次统计()
    {
        using var ctx = CreateDbContext();
        var b1 = CreateBatch(ctx, "B001", "WO001",
            currentSectionName: SectionKeys.Cut,
            currentSectionCompleted: false,
            currentValidWeight: 1000);
        var b2 = CreateBatch(ctx, "B002", "WO002",
            currentSectionName: SectionKeys.Cut,
            currentSectionCompleted: false,
            currentValidWeight: 2000);
        await ctx.SaveChangesAsync(); // 先保存拿到真实 Id

        ctx.Set<BatchPlanSchedule>().AddRange(
            new BatchPlanSchedule { BatchId = b1.Id, IsFlow = true, FlowLevel = 1 },  // 急+ = 重点
            new BatchPlanSchedule { BatchId = b2.Id, IsFlow = true, FlowLevel = 2 }); // 急
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var rows = await svc.GetSummaryAsync();

        var cut = Row(rows, "断切");
        cut.BatchCount.Should().Be(2);
        cut.FlowBatchCount.Should().Be(2);
        cut.FlowBatchWeight.Should().Be(3000m);
        cut.KeyBatchCount.Should().Be(1);                 // 重点 = PlanFlowLevel==1
        cut.KeyBatchWeight.Should().Be(1000m);
        cut.Level1Count.Should().Be(1);
        cut.Level2Count.Should().Be(1);
        cut.Level4Count.Should().Be(0);

        var total = Row(rows, "合计");
        total.FlowBatchCount.Should().Be(2);
        total.KeyBatchCount.Should().Be(1);
    }

    [Fact]
    public async Task GetSummaryAsync_检验类多Tab重叠_合计唯一()
    {
        using var ctx = CreateDbContext();
        var b = CreateBatch(ctx, "B001", "WO001",
            currentGroupName: ProcessKeys.RoughTubeProcessing,
            currentSectionName: SectionKeys.Inspection,
            currentSectionCompleted: false,
            currentValidWeight: 1000);
        await ctx.SaveChangesAsync(); // 先保存拿到真实 Id

        ctx.ProcessGroups.AddRange(
            new ProcessGroup { ProductionBatchId = b.Id, ProcessName = ProcessKeys.RoughTubeProcessing, SequenceNumber = 1 },
            new ProcessGroup { ProductionBatchId = b.Id, ProcessName = ProcessKeys.ColdRoll60, SequenceNumber = 2 });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var rows = await svc.GetSummaryAsync();

        // 荒管检：工段=检验 + 产类=荒管（工序=荒管处理）→ 命中
        Row(rows, "荒管检").BatchCount.Should().Be(1);
        // 在制检：产类=在制 → 不命中（此批次产类=荒管）
        Row(rows, "在制检").BatchCount.Should().Be(0);
        // 成品检概念已删除；内抛+内修磨：工段=检验 ≠ 内抛/内修磨 → 不命中
        Row(rows, "内抛+内修磨").BatchCount.Should().Be(0);
        // 过程检概念已删除，不出现于汇总行
        rows.Select(r => r.SectionName).Should().NotContain("过程检");
        rows.Select(r => r.SectionName).Should().NotContain("过程检验");

        // 合计 = 全量唯一批次
        var total = Row(rows, "合计");
        total.BatchCount.Should().Be(1);
        total.TotalWeight.Should().Be(1000m);
    }

    [Fact]
    public async Task GetSummaryAsync_在制修检检验批次_按产类归入在制检()
    {
        using var ctx = CreateDbContext();
        var b = CreateBatch(ctx, "B001", "WO001",
            currentGroupName: ProcessKeys.InProcessRepair,
            currentSectionName: SectionKeys.Inspection,
            currentSectionCompleted: false,
            currentValidWeight: 1000);
        await ctx.SaveChangesAsync(); // 先保存拿到真实 Id

        // 在制修检为中间工序（非末道，末道=冷轧60），批次含荒管处理工序组但荒管规格为空 → 产类=在制
        ctx.ProcessGroups.AddRange(
            new ProcessGroup { ProductionBatchId = b.Id, ProcessName = ProcessKeys.RoughTubeProcessing, SequenceNumber = 1 },
            new ProcessGroup { ProductionBatchId = b.Id, ProcessName = ProcessKeys.InProcessRepair, SequenceNumber = 2 },
            new ProcessGroup { ProductionBatchId = b.Id, ProcessName = ProcessKeys.ColdRoll60, SequenceNumber = 3 });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var rows = await svc.GetSummaryAsync();

        // 在制检：工段=检验 + 产类=在制（非末道工序、无荒管规格匹配）→ 命中
        Row(rows, "在制检").BatchCount.Should().Be(1);
        // 荒管检：产类=在制 ≠ 荒管 → 不命中
        Row(rows, "荒管检").BatchCount.Should().Be(0);
        // 内抛+内修磨：工段=检验 ≠ 内抛/内修磨 → 不命中
        Row(rows, "内抛+内修磨").BatchCount.Should().Be(0);
    }

    [Fact]
    public async Task GetSummaryAsync_内抛与内修磨批次_归入内抛内修磨Tab()
    {
        using var ctx = CreateDbContext();
        // 内抛批次（当前工段=内抛 未完工）
        CreateBatch(ctx, "B001", "WO001",
            currentSectionName: SectionKeys.InnerPolish,
            currentSectionCompleted: false,
            currentValidWeight: 1000);
        // 内修磨批次（当前已完工，下一工段=内修磨）
        CreateBatch(ctx, "B002", "WO002",
            currentSectionCompleted: true,
            nextSectionName: SectionKeys.InnerGrinding,
            currentValidWeight: 2000);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var rows = await svc.GetSummaryAsync();

        var combo = Row(rows, "内抛+内修磨");
        combo.BatchCount.Should().Be(2);
        combo.TotalWeight.Should().Be(3000m);

        // 合计 = 全量唯一批次（两批次各命中唯一 Tab，无重叠）
        var total = Row(rows, "合计");
        total.BatchCount.Should().Be(2);
        total.TotalWeight.Should().Be(3000m);
    }

    [Fact]
    public async Task GetAllAsync_暂停批次_读时覆盖为非流转_非流转字段保留()
    {
        using var ctx = CreateDbContext();
        var b = CreateBatch(ctx, "B001", "WO001");
        await ctx.SaveChangesAsync(); // 先保存拿到真实 Id

        ctx.Set<BatchPlanSchedule>().Add(new BatchPlanSchedule
        {
            BatchId = b.Id,
            IsFlow = true,
            FlowLevel = 1,
            FlowTarget = "F",
            FlowCRType = ProcessKeys.ColdRoll60,
            PlanOuterDiameterSpan = "span",
            FlowExecSpec = "spec",
            TargetSequence = 5,
            ExecutionSequence = 3,
            IsGrabOrder = true,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);

        // 未暂停：正常流转
        var normal = (await svc.GetAllAsync(null)).Single(x => x.BatchId == b.Id);
        normal.PlanIsPaused.Should().BeFalse();
        normal.PlanIsFlow.Should().BeTrue();
        normal.PlanFlowLevel.Should().Be(1);
        normal.PlanFlowTarget.Should().Be("F");
        normal.PlanFlowCRType.Should().Be(ProcessKeys.ColdRoll60);
        normal.PlanOuterDiameterSpan.Should().Be("span");
        normal.PlanFlowExecSpec.Should().Be("spec");
        normal.PlanExecutionSequence.Should().Be(3);
        normal.PlanTargetSequence.Should().Be(5);
        normal.IsGrabOrder.Should().BeTrue();

        // 暂停=是：读时覆盖为非流转（DB 原流转保留）
        ctx.Set<BatchPlanSchedule>().Single(x => x.BatchId == b.Id).IsPaused = true;
        await ctx.SaveChangesAsync();

        var paused = (await svc.GetAllAsync(null)).Single(x => x.BatchId == b.Id);
        paused.PlanIsPaused.Should().BeTrue();
        paused.PlanIsFlow.Should().BeFalse();
        paused.PlanFlowLevel.Should().Be(5);
        paused.PlanFlowTarget.Should().BeNull();
        paused.PlanFlowCRType.Should().BeNull();
        paused.PlanOuterDiameterSpan.Should().BeNull();
        paused.PlanFlowExecSpec.Should().BeNull();
        paused.PlanExecutionSequence.Should().BeNull();
        paused.PlanTargetSequence.Should().BeNull();
        paused.IsGrabOrder.Should().BeTrue(); // 非流转字段不受影响

        // DB 原流转仍在（读时覆盖语义）
        var db = ctx.Set<BatchPlanSchedule>().AsNoTracking().Single(x => x.BatchId == b.Id);
        db.IsPaused.Should().BeTrue();
        db.IsFlow.Should().BeTrue();
        db.FlowLevel.Should().Be(1);
        db.FlowTarget.Should().Be("F");
        db.PlanOuterDiameterSpan.Should().Be("span");
    }
}
