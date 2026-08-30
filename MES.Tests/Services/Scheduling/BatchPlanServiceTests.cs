using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Scheduling;
using MES.Core.Enums;
using MES.Core.Interfaces.Configuration;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Scheduling;
using MES.Tests.Tests;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Quality;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.WorkOrder;

namespace MES.Tests.Services.Scheduling;

/// <summary>
/// 在产明细计划服务测试：分页查询、工段筛选、关键词搜索、汇总
/// </summary>
public class BatchPlanServiceTests : TestBase
{
    private BatchPlanService CreateService(AppDbContext ctx) => new(ctx, CreateProcessDefinitionServiceMock(), CreateStandardWorkDayServiceMock());

    private BatchPlanService CreateService(AppDbContext ctx, IStandardWorkDayService standardWorkDayService) => new(ctx, CreateProcessDefinitionServiceMock(), standardWorkDayService);

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
    public async Task GetSectionTabOptionsAsync_配置驱动组装_冷轧含新增110_普通去重_固定检验()
    {
        using var ctx = CreateDbContext();
        // 冷轧工序含新增 110（extraKeys）；普通工段启用 冷轧拔/断切/内抛/检验/入库 → 剔除冷轧拔/检验/入库后仅剩断切/内抛
        var svc = new BatchPlanService(ctx,
            CreateProcessDefinitionServiceMock(new[] { "ColdRoll110" }),
            CreateStandardWorkDayServiceMock(
                new SectionInfoDto { SectionKey = SectionKeys.ColdRollDraw, SectionName = "冷轧拔", DisplayOrder = 1, IsEnabled = true },
                new SectionInfoDto { SectionKey = SectionKeys.Cut, SectionName = "断切", DisplayOrder = 2, IsEnabled = true },
                new SectionInfoDto { SectionKey = SectionKeys.InnerPolish, SectionName = "内抛", DisplayOrder = 3, IsEnabled = true },
                new SectionInfoDto { SectionKey = SectionKeys.Inspection, SectionName = "检验", DisplayOrder = 4, IsEnabled = true },
                new SectionInfoDto { SectionKey = SectionKeys.Warehouse, SectionName = "入库", DisplayOrder = 5, IsEnabled = true }));

        var tabs = await svc.GetSectionTabOptionsAsync();

        // 冷轧冷拔工序（含新增 110）：60/50/30/20/三辊/冷拔/110
        tabs.Take(7).Select(t => t.Key).Should().Equal(
            ProcessKeys.ColdRoll60, ProcessKeys.ColdRoll50, ProcessKeys.ColdRoll30,
            ProcessKeys.ColdRoll20, ProcessKeys.ThreeRollColdRoll, ProcessKeys.ColdDraw, "ColdRoll110");
        tabs.Take(7).Should().OnlyContain(t => t.Group == "cold");
        // 普通工段：剔除冷轧拔/检验/入库 → 仅 断切/内抛
        tabs.Skip(7).Take(2).Select(t => t.Key).Should().Equal(SectionKeys.Cut, SectionKeys.InnerPolish);
        tabs.Skip(7).Take(2).Should().OnlyContain(t => t.Group == "section");
        // 末尾固定检验
        tabs.Skip(9).Select(t => t.Key).Should().Equal(BatchPlanSectionTabs.RoughTubeInspection, BatchPlanSectionTabs.InProcessInspection);
    }

    [Fact]
    public async Task GetPagedAsync_工段筛选冷轧110_配置驱动新增工序()
    {
        using var ctx = CreateDbContext();
        // 110冷轧批次（CurrentGroupName 存英文 Key "ColdRoll110"），冷轧拔工段未完成
        var batch = CreateBatch(ctx, "B001", "WO001",
            currentGroupName: "ColdRoll110",
            currentSectionName: SectionKeys.ColdRollDraw,
            currentSectionCompleted: false);
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = "ColdRoll110",
            SequenceNumber = 1,
            ColdRollDraw = 1,
        });
        await ctx.SaveChangesAsync();

        // 前端配置驱动传英文 Key "ColdRoll110"（不在 ProcessKeys 常量内，靠配置驱动冷轧集合识别）
        var svc = new BatchPlanService(ctx,
            CreateProcessDefinitionServiceMock(new[] { "ColdRoll110" }),
            CreateStandardWorkDayServiceMock());
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "__SectionTab", Value = "ColdRoll110" }
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

    // ========== 近日生产量数据 GetSummaryAsync ==========

    private static BatchPlanSummaryRowDto Row(List<BatchPlanSummaryRowDto> rows, string sectionName)
        => rows.Single(r => r.SectionName == sectionName);

    private static BatchPlanMonthlySummaryRowDto Row(List<BatchPlanMonthlySummaryRowDto> rows, string sectionName)
        => rows.Single(r => r.SectionName == sectionName);

    // —— 夹具 helper：生产记录 / 委外回收 / 去油酸洗完工 / 过程检验（InMemory 不校验外键，FK 用占位 1） ——

    private static async Task AddRecord(AppDbContext ctx, string processName, string sectionName,
        DateTime execDate, decimal weight)
    {
        ctx.Set<ProductionRecord>().Add(new ProductionRecord
        {
            ProductionBatchId = 1,
            ProcessGroupId = 1,
            ProcessName = processName,
            SectionName = sectionName,
            ExecDate = execDate,
            Weight = weight,
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task AddRecovery(AppDbContext ctx, string processName, string sectionName,
        DateTime recoveryDate, decimal recoveryWeight, decimal? unprocessedWeight = null)
    {
        var so = new SectionOutsource
        {
            ProductionBatchId = 1,
            ProcessGroupId = 1,
            ProcessName = processName,
            SectionName = sectionName,
            OutsourceVendor = "委外厂",
            SendOutDate = recoveryDate,
        };
        ctx.Set<SectionOutsource>().Add(so);
        await ctx.SaveChangesAsync(); // 先保存拿真实 Id 供回收记录 FK

        ctx.Set<OutsourceRecovery>().Add(new OutsourceRecovery
        {
            SectionOutsourceId = so.Id,
            RecoveryDate = recoveryDate,
            RecoveryWeight = recoveryWeight,
            UnprocessedWeight = unprocessedWeight,
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task AddPicklingOut(AppDbContext ctx, string sectionName,
        DateTime completeDate, decimal weight)
    {
        var pin = new PicklingInRecord
        {
            ProductionBatchId = 1,
            ProcessGroupId = 1,
            ProcessName = ProcessKeys.RoughTubeProcessing,
            SectionName = sectionName,
            InDate = completeDate,
        };
        ctx.Set<PicklingInRecord>().Add(pin);
        await ctx.SaveChangesAsync(); // 先保存拿真实 Id 供完工记录 FK

        ctx.Set<PicklingOutRecord>().Add(new PicklingOutRecord
        {
            PicklingInRecordId = pin.Id,
            CompleteDate = completeDate,
            SectionName = sectionName,
            Weight = weight,
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task AddInspection(AppDbContext ctx, string productStatus,
        DateTime inspectionDate, decimal weight)
    {
        ctx.Set<ProcessInspection>().Add(new ProcessInspection
        {
            ProductionBatchId = 1,
            ProcessGroupId = 1,
            ProcessName = ProcessKeys.RoughTubeProcessing,
            SectionName = SectionKeys.Inspection,
            InspectionDate = inspectionDate,
            Weight = weight,
            ProductStatus = productStatus,
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task GetSummaryAsync_冷轧拔按工序分化_普通工段按工段名()
    {
        using var ctx = CreateDbContext();
        var today = DateTime.Today;
        await AddRecord(ctx, ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, today, 1000m); // 冷轧拔-60冷轧
        await AddRecord(ctx, ProcessKeys.ColdRoll50, SectionKeys.ColdRollDraw, today, 2000m); // 冷轧拔-50冷轧
        await AddRecord(ctx, ProcessKeys.RoughTubeProcessing, SectionKeys.Cut, today, 3000m); // 断切

        var svc = CreateService(ctx);
        var rows = await svc.GetSummaryAsync();

        Row(rows, "冷轧拔-60冷轧").TodayWeight.Should().Be(1000m);
        Row(rows, "冷轧拔-50冷轧").TodayWeight.Should().Be(2000m);
        Row(rows, "断切").TodayWeight.Should().Be(3000m);
        Row(rows, "合计").TodayWeight.Should().Be(6000m);
    }

    [Fact]
    public async Task GetSummaryAsync_冷轧拔按工序分化_90冷轧兜底_非冷轧工序丢弃()
    {
        using var ctx = CreateDbContext();
        var today = DateTime.Today;
        await AddRecord(ctx, ProcessKeys.ColdRoll30, SectionKeys.ColdRollDraw, today, 500m);   // 冷轧拔-30冷轧
        await AddRecord(ctx, "90冷轧", SectionKeys.ColdRollDraw, today, 700m);                  // 90冷轧（中文，暂未收录 ProcessKeys）
        await AddRecord(ctx, ProcessKeys.RoughTubeProcessing, SectionKeys.ColdRollDraw, today, 999m); // 非冷轧工序 → 丢弃

        var svc = CreateService(ctx);
        var rows = await svc.GetSummaryAsync();

        Row(rows, "冷轧拔-30冷轧").TodayWeight.Should().Be(500m);
        Row(rows, "冷轧拔-90冷轧").TodayWeight.Should().Be(700m);
        Row(rows, "合计").TodayWeight.Should().Be(1200m);   // 999 丢弃不计入合计
    }

    [Fact]
    public async Task GetSummaryAsync_日期窗口_今日实时_前3日前6日不含今日()
    {
        using var ctx = CreateDbContext();
        var today = DateTime.Today;
        await AddRecord(ctx, ProcessKeys.RoughTubeProcessing, SectionKeys.Cut, today, 100m);
        await AddRecord(ctx, ProcessKeys.RoughTubeProcessing, SectionKeys.Cut, today.AddDays(-2), 200m);
        await AddRecord(ctx, ProcessKeys.RoughTubeProcessing, SectionKeys.Cut, today.AddDays(-5), 300m);
        await AddRecord(ctx, ProcessKeys.RoughTubeProcessing, SectionKeys.Cut, today.AddDays(-7), 400m); // 窗口外

        var svc = CreateService(ctx);
        var rows = await svc.GetSummaryAsync();

        var cut = Row(rows, "断切");
        cut.TodayWeight.Should().Be(100m);       // 今日（实时），仅今日
        cut.Last3DaysWeight.Should().Be(200m);   // 前3日=[今天−3,今天)：仅前2天，不含今日
        cut.Last7DaysWeight.Should().Be(500m);   // 前6日=[今天−6,今天)：前2天+前5天，不含今日（前7天在窗口外）
    }

    [Fact]
    public async Task GetSummaryAsync_去油酸洗用完工记录_不含生产记录()
    {
        using var ctx = CreateDbContext();
        var today = DateTime.Today;
        // 去油/酸洗工段的生产记录不计入（走完工记录统计）
        await AddRecord(ctx, ProcessKeys.RoughTubeProcessing, SectionKeys.Degrease, today, 999m);
        await AddRecord(ctx, ProcessKeys.RoughTubeProcessing, SectionKeys.Pickle, today, 888m);
        await AddPicklingOut(ctx, SectionKeys.Degrease, today, 1000m);
        await AddPicklingOut(ctx, SectionKeys.Pickle, today, 2000m);

        var svc = CreateService(ctx);
        var rows = await svc.GetSummaryAsync();

        Row(rows, "去油").TodayWeight.Should().Be(1000m);
        Row(rows, "酸洗").TodayWeight.Should().Be(2000m);
        Row(rows, "合计").TodayWeight.Should().Be(3000m);
    }

    [Fact]
    public async Task GetSummaryAsync_荒管检在制检用过程检验重量_按产类区分()
    {
        using var ctx = CreateDbContext();
        var today = DateTime.Today;
        await AddInspection(ctx, ProductStatuses.RoughTube, today, 1000m);
        await AddInspection(ctx, ProductStatuses.InProgress, today.AddDays(-1), 2000m);
        await AddInspection(ctx, ProductStatuses.Finished, today, 5000m); // 成品不计入荒管检/在制检

        var svc = CreateService(ctx);
        var rows = await svc.GetSummaryAsync();

        Row(rows, "检验-荒管").TodayWeight.Should().Be(1000m);
        Row(rows, "检验-在制").TodayWeight.Should().Be(0m);          // 前1天不在今日
        Row(rows, "检验-在制").Last3DaysWeight.Should().Be(2000m);
        Row(rows, "合计").TodayWeight.Should().Be(1000m);
        Row(rows, "合计").Last3DaysWeight.Should().Be(2000m);     // 前3日不含今日：仅前1天在制检 2000
    }

    [Fact]
    public async Task GetSummaryAsync_委外回收仅回收量_冷轧委外归冷轧拔行()
    {
        using var ctx = CreateDbContext();
        var today = DateTime.Today;
        // 冷轧委外回收按工段归冷轧拔行；未加工量不计入
        await AddRecovery(ctx, ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, today, 1000m, unprocessedWeight: 500m);
        await AddRecovery(ctx, ProcessKeys.RoughTubeProcessing, SectionKeys.Cut, today, 2000m);

        var svc = CreateService(ctx);
        var rows = await svc.GetSummaryAsync();

        Row(rows, "冷轧拔-60冷轧").TodayWeight.Should().Be(1000m);
        Row(rows, "断切").TodayWeight.Should().Be(2000m);
        Row(rows, "合计").TodayWeight.Should().Be(3000m);
    }

    [Fact]
    public async Task GetSummaryAsync_内抛与内修磨合并归行()
    {
        using var ctx = CreateDbContext();
        var today = DateTime.Today;
        await AddRecord(ctx, ProcessKeys.RoughTubeProcessing, SectionKeys.InnerPolish, today, 1000m);
        await AddRecord(ctx, ProcessKeys.RoughTubeProcessing, SectionKeys.InnerGrinding, today, 2000m);

        var svc = CreateService(ctx);
        var rows = await svc.GetSummaryAsync();

        var combo = Row(rows, "内抛+内修磨");
        combo.TodayWeight.Should().Be(3000m);
        Row(rows, "合计").TodayWeight.Should().Be(3000m);
    }

    // ========== 月度生产量数据 GetMonthlySummaryAsync（口径与 GetSummaryAsync 一致，按本年 1月~12月） ==========

    [Fact]
    public async Task GetMonthlySummaryAsync_按本年月份聚合_冷轧归冷轧拔行_合计()
    {
        using var ctx = CreateDbContext();
        var year = DateTime.Today.Year;
        await AddRecord(ctx, ProcessKeys.RoughTubeProcessing, SectionKeys.Cut,
            new DateTime(year, 1, 15), 1000m);                 // 断切 1月
        await AddRecord(ctx, ProcessKeys.RoughTubeProcessing, SectionKeys.Cut,
            new DateTime(year, 6, 10), 2000m);                 // 断切 6月
        await AddRecord(ctx, ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw,
            new DateTime(year, 12, 5), 3000m);                 // 冷轧拔 12月（原60冷轧行）

        var svc = CreateService(ctx);
        var rows = await svc.GetMonthlySummaryAsync();

        var cut = Row(rows, "断切");
        cut.MonthlyWeights[0].Should().Be(1000m);              // 1月
        cut.MonthlyWeights[5].Should().Be(2000m);              // 6月
        cut.MonthlyWeights[11].Should().Be(0m);                // 12月无断切
        Row(rows, "冷轧拔-60冷轧").MonthlyWeights[11].Should().Be(3000m); // 12月
        Row(rows, "合计").MonthlyWeights[0].Should().Be(1000m);
        Row(rows, "合计").MonthlyWeights[5].Should().Be(2000m);
        Row(rows, "合计").MonthlyWeights[11].Should().Be(3000m);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_去油酸洗完工记录与过程检验按月归桶()
    {
        using var ctx = CreateDbContext();
        var year = DateTime.Today.Year;
        await AddPicklingOut(ctx, SectionKeys.Degrease, new DateTime(year, 2, 1), 1000m);
        await AddPicklingOut(ctx, SectionKeys.Pickle, new DateTime(year, 3, 1), 2000m);
        await AddInspection(ctx, ProductStatuses.RoughTube, new DateTime(year, 4, 1), 3000m);

        var svc = CreateService(ctx);
        var rows = await svc.GetMonthlySummaryAsync();

        Row(rows, "去油").MonthlyWeights[1].Should().Be(1000m);   // 2月
        Row(rows, "酸洗").MonthlyWeights[2].Should().Be(2000m);   // 3月
        Row(rows, "检验-荒管").MonthlyWeights[3].Should().Be(3000m); // 4月
        Row(rows, "合计").MonthlyWeights[1].Should().Be(1000m);
        Row(rows, "合计").MonthlyWeights[3].Should().Be(3000m);
    }

    [Fact]
    public async Task GetSummaryAsync_整行全0工段隐藏_仅保留合计()
    {
        using var ctx = CreateDbContext();
        // 无任何产量数据 → 全部工段行（含冷轧拔分化行/检验行）全 0，应默认隐藏，仅保留合计行
        var svc = CreateService(ctx);
        var rows = await svc.GetSummaryAsync();

        rows.Count.Should().Be(1);
        rows[0].SectionName.Should().Be("合计");
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_整行全0工段隐藏_仅保留合计()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var rows = await svc.GetMonthlySummaryAsync();

        rows.Count.Should().Be(1);
        rows[0].SectionName.Should().Be("合计");
    }

    // ========== 实时委外在产 GetOutsourcePendingAsync（批次信息口径） ==========

    private static OutsourcePendingRowDto OutsourceUnitRow(BatchPlanOutsourcePendingDto dto, string unit)
        => dto.Rows.Single(r => r.OutsourceUnit == unit);

    /// <summary>创建带当前委外单位+有效投料重量的批次（默认在产，需手动 SaveChanges）</summary>
    private ProductionBatch CreateOutsourceBatch(AppDbContext ctx, string batchNo, string unit,
        string? groupName, string? sectionName, int validWeight, BatchStatus status = BatchStatus.InProgress)
    {
        var b = CreateBatch(ctx, batchNo, batchNo, status: status,
            currentGroupName: groupName, currentSectionName: sectionName);
        b.CurrentOutsource = unit;
        b.CurrentValidWeight = validWeight;
        return b;
    }

    [Fact]
    public async Task GetOutsourcePendingAsync_按在产单位工段聚合有效投料_无委外不计入_未产也计入()
    {
        using var ctx = CreateDbContext();
        // 单位A·断切：两个在产批次有效投料 1000+2000
        CreateOutsourceBatch(ctx, "B1", "单位A", ProcessKeys.RoughTubeProcessing, SectionKeys.Cut, 1000);
        CreateOutsourceBatch(ctx, "B2", "单位A", ProcessKeys.RoughTubeProcessing, SectionKeys.Cut, 2000);
        // 单位A·酸洗：未产批次 500（未产也计入）
        CreateOutsourceBatch(ctx, "B3", "单位A", ProcessKeys.RoughTubeProcessing, SectionKeys.Pickle, 500,
            status: BatchStatus.None);
        // 单位B·断切：1500
        CreateOutsourceBatch(ctx, "B4", "单位B", ProcessKeys.RoughTubeProcessing, SectionKeys.Cut, 1500);
        // 无当前委外单位（CurrentOutsource=null）：不计入
        CreateBatch(ctx, "B5", "WO5", currentGroupName: ProcessKeys.RoughTubeProcessing, currentSectionName: SectionKeys.Cut);
        await ctx.SaveChangesAsync();

        // 断切/酸洗为普通工段，需配置工段工量天数启用工段才归列
        var svc = CreateService(ctx, CreateStandardWorkDayServiceMock(
            new SectionInfoDto { SectionKey = SectionKeys.Cut, SectionName = "断切", DisplayOrder = 1, IsEnabled = true },
            new SectionInfoDto { SectionKey = SectionKeys.Pickle, SectionName = "酸洗", DisplayOrder = 2, IsEnabled = true }));
        var dto = await svc.GetOutsourcePendingAsync();

        var rowA = OutsourceUnitRow(dto, "单位A");
        rowA.Cells["断切"].Total.Should().Be(3000m);             // 1000+2000
        rowA.Cells["酸洗"].Total.Should().Be(500m);              // 未产批次也计入
        rowA.TotalCell.Total.Should().Be(3500m);

        var rowB = OutsourceUnitRow(dto, "单位B");
        rowB.Cells["断切"].Total.Should().Be(1500m);
        rowB.TotalCell.Total.Should().Be(1500m);

        // 合计行
        var total = dto.Rows.Single(r => r.OutsourceUnit == "合计");
        total.Cells["断切"].Total.Should().Be(4500m);            // 3000+1500
        total.Cells["酸洗"].Total.Should().Be(500m);
        total.TotalCell.Total.Should().Be(5000m);
    }

    [Fact]
    public async Task GetOutsourcePendingAsync_冷轧按工序分化_内抛内修磨拆分_列配置驱动序()
    {
        using var ctx = CreateDbContext();
        // 60冷轧：车间一两个在产批次 3000+2000 → 列 60冷轧
        CreateOutsourceBatch(ctx, "B1", "车间一", ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, 3000);
        CreateOutsourceBatch(ctx, "B2", "车间一", ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, 2000);
        // 内抛/内修磨：车间二各一批 → 独立列（不再合并）
        CreateOutsourceBatch(ctx, "B3", "车间二", ProcessKeys.RoughTubeProcessing, SectionKeys.InnerPolish, 1000);
        CreateOutsourceBatch(ctx, "B4", "车间二", ProcessKeys.RoughTubeProcessing, SectionKeys.InnerGrinding, 500);
        // 断切：车间一 400
        CreateOutsourceBatch(ctx, "B5", "车间一", ProcessKeys.RoughTubeProcessing, SectionKeys.Cut, 400);
        await ctx.SaveChangesAsync();

        // 工段工量天数启用工段：断切/内抛/内修磨（配置驱动，普通工段列序按此返回序）
        var svc = CreateService(ctx, CreateStandardWorkDayServiceMock(
            new SectionInfoDto { SectionKey = SectionKeys.Cut, SectionName = "断切", DisplayOrder = 1, IsEnabled = true },
            new SectionInfoDto { SectionKey = SectionKeys.InnerPolish, SectionName = "内抛", DisplayOrder = 2, IsEnabled = true },
            new SectionInfoDto { SectionKey = SectionKeys.InnerGrinding, SectionName = "内修磨", DisplayOrder = 3, IsEnabled = true }));
        var dto = await svc.GetOutsourcePendingAsync();

        // 配置驱动 Tab 序：冷轧工序(60冷轧 index0) → 普通工段(断切/内抛/内修磨) → 固定检验(荒管检/在制检)
        dto.Sections.Should().Equal("60冷轧", "断切", "内抛", "内修磨");
        var r1 = OutsourceUnitRow(dto, "车间一");
        r1.Cells["60冷轧"].Total.Should().Be(5000m);             // 3000+2000
        r1.Cells["断切"].Total.Should().Be(400m);
        r1.TotalCell.Total.Should().Be(5400m);
        var r2 = OutsourceUnitRow(dto, "车间二");
        r2.Cells["内抛"].Total.Should().Be(1000m);               // 独立列
        r2.Cells["内修磨"].Total.Should().Be(500m);
        r2.TotalCell.Total.Should().Be(1500m);
    }

    [Fact]
    public async Task GetOutsourcePendingAsync_完成成检暂停批次不计入()
    {
        using var ctx = CreateDbContext();
        CreateOutsourceBatch(ctx, "B1", "单位A", ProcessKeys.RoughTubeProcessing, SectionKeys.Cut, 1000,
            status: BatchStatus.Completed);
        CreateOutsourceBatch(ctx, "B2", "单位A", ProcessKeys.RoughTubeProcessing, SectionKeys.Cut, 2000,
            status: BatchStatus.InFinalInspection);
        CreateOutsourceBatch(ctx, "B3", "单位A", ProcessKeys.RoughTubeProcessing, SectionKeys.Cut, 3000,
            status: BatchStatus.Suspended);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var dto = await svc.GetOutsourcePendingAsync();

        dto.Sections.Should().BeEmpty();
        dto.Rows.Single(r => r.OutsourceUnit == "合计").TotalCell.Total.Should().Be(0m);
    }

    [Fact]
    public async Task GetOutsourcePendingAsync_当前工段无对应Tab丢弃_合计恒追加()
    {
        using var ctx = CreateDbContext();
        // 检验工段无"检验"Tab（荒管检/在制检需产类，委外工段不出现）→ ResolveSummaryTabName 归列失败丢弃
        CreateOutsourceBatch(ctx, "B1", "单位A", ProcessKeys.RoughTubeProcessing, SectionKeys.Inspection, 1000);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var dto = await svc.GetOutsourcePendingAsync();

        dto.Sections.Should().BeEmpty();
        var total = dto.Rows.Single(r => r.OutsourceUnit == "合计");
        total.TotalCell.Total.Should().Be(0m);
    }

    [Fact]
    public async Task GetOutsourcePendingAsync_流转重量按实时IsFlow聚合_重点按等级急加聚合()
    {
        using var ctx = CreateDbContext();
        // 单位A·50冷轧：批次1 冷轧拔在轧 + 本层排程 All → 实时 IsFlow=true（无薄表 → PlanFlowLevel=5 非重点）
        var b1 = CreateOutsourceBatch(ctx, "B1", "单位A", ProcessKeys.ColdRoll50, SectionKeys.ColdRollDraw, 3000);
        b1.CurrentSectionCompleted = false; // 本层冷轧拔在轧（在轧要求分支匹配 CompletionType=All → 流转）
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = b1.Id,
            ProcessName = ProcessKeys.ColdRoll50,
            SequenceNumber = 1,
            ColdRollDraw = 1,
            ManufacturingSpec = "38*3.2",
        });
        ctx.ColdRollSpecSchedules.Add(new ColdRollSpecSchedule
        {
            ProcessType = ProcessKeys.ColdRoll50,
            BilletSpec = "",
            RollingSpec = "38*3.2",
            IsFinished = true, // 单工序组 → 批次 IsFinished=true，排程 key 需一致
            CompletionType = "All",
            RollType = "All",
        });
        SeedSummary(ctx, "WO1", scheduleStage: 3, urgencyLevel: UrgencyLevelKeys.CSlow);
        await ctx.SaveChangesAsync();
        var s1 = ctx.Set<WorkOrderExecutionSummary>().First(x => x.WorkOrderNo == "WO1");
        s1.MainNoAttentionProcess = ProcessKeys.ColdRoll50;
        s1.ProductionFlowProperty = ProductionFlowKeys.Normal;

        // 单位A·50冷轧：批次2 薄表 PlanFlowLevel=1（等级急+）→ 重点重量计入 2000（无排程/无工序组 → 实时 IsFlow=false）
        var b2 = CreateOutsourceBatch(ctx, "B2", "单位A", ProcessKeys.ColdRoll50, SectionKeys.ColdRollDraw, 2000);
        await ctx.SaveChangesAsync(); // 先保存拿到 Id
        ctx.Set<BatchPlanSchedule>().Add(new BatchPlanSchedule
        {
            BatchId = b2.Id,
            IsFlow = true,
            FlowLevel = 1,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var dto = await svc.GetOutsourcePendingAsync();

        var rowA = OutsourceUnitRow(dto, "单位A");
        rowA.Cells["50冷轧"].Total.Should().Be(5000m);           // 3000+2000
        rowA.Cells["50冷轧"].Flow.Should().Be(3000m);            // 仅批次1 IsFlow=true
        rowA.Cells["50冷轧"].Key.Should().Be(2000m);             // 仅批次2 等级急+
        rowA.TotalCell.Total.Should().Be(5000m);
        rowA.TotalCell.Flow.Should().Be(3000m);
        rowA.TotalCell.Key.Should().Be(2000m);

        // 合计行
        var total = dto.Rows.Single(r => r.OutsourceUnit == "合计");
        total.Cells["50冷轧"].Total.Should().Be(5000m);
        total.Cells["50冷轧"].Flow.Should().Be(3000m);
        total.Cells["50冷轧"].Key.Should().Be(2000m);
        total.TotalCell.Total.Should().Be(5000m);
        total.TotalCell.Flow.Should().Be(3000m);
        total.TotalCell.Key.Should().Be(2000m);
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
