using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Scheduling;
using MES.Core.Enums;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.Scheduling;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Batch;
using MES.Services.Scheduling;
using MES.Tests.Tests;
using Moq;

namespace MES.Tests.Services.Scheduling;

/// <summary>
/// 生产工段待产量现况服务测试：配置表驱动维度（启用工序组 × 启用工段全笛卡尔）与聚合计算
/// </summary>
public class SectionProductionStatusServiceTests : TestBase
{
    private static ProcessInfoDto Process(string key, int order = 1)
        => new() { ProcessKey = key, ProcessName = ProcessKeys.ToChinese(key) ?? key, DisplayOrder = order, IsEnabled = true };

    private static SectionInfoDto Section(string key, int order = 1)
        => new() { SectionKey = key, SectionName = SectionKeys.ToChinese(key) ?? key, DisplayOrder = order, IsEnabled = true };

    private SectionProductionStatusService CreateService(AppDbContext ctx,
        IEnumerable<ProcessInfoDto>? processes = null,
        IEnumerable<SectionInfoDto>? sections = null,
        List<BatchPlanDto>? planItems = null)
    {
        var processMock = new Mock<IProcessDefinitionService>();
        processMock.Setup(x => x.GetEnabledProcessesAsync())
            .ReturnsAsync(processes?.ToList() ?? new List<ProcessInfoDto>());
        var workDayMock = new Mock<IStandardWorkDayService>();
        workDayMock.Setup(x => x.GetEnabledSectionsAsync())
            .ReturnsAsync(sections?.ToList() ?? new List<SectionInfoDto>());
        var batchPlanMock = new Mock<IBatchPlanService>();
        batchPlanMock.Setup(x => x.GetAllAsync(null))
            .ReturnsAsync(planItems ?? new List<BatchPlanDto>());
        return new SectionProductionStatusService(ctx, processMock.Object, workDayMock.Object, batchPlanMock.Object);
    }

    private ProductionBatch CreateBatch(AppDbContext ctx, string batchNo,
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

    private void AddProcessGroups(AppDbContext ctx, ProductionBatch batch, params string[] processKeys)
    {
        var seq = 1;
        foreach (var key in processKeys)
        {
            ctx.ProcessGroups.Add(new ProcessGroup
            {
                ProductionBatchId = batch.Id,
                ProcessName = key,
                SequenceNumber = seq++
            });
        }
    }

    private void AddProcessGroup(AppDbContext ctx, ProductionBatch batch, string processKey, string? manufacturingSpec, int sequenceNumber)
    {
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            ProcessName = processKey,
            SequenceNumber = sequenceNumber,
            ManufacturingSpec = manufacturingSpec,
        });
    }

    // ==================== 配置表驱动维度 ====================

    [Fact]
    public async Task GetStatusAsync_维度由启用工序组乘启用工段全笛卡尔生成_排除入库()
    {
        using var ctx = CreateDbContext();
        var processes = new[] { Process(ProcessKeys.RoughTubeProcessing, 1), Process(ProcessKeys.ColdRoll60, 3) };
        var sections = new[]
        {
            Section(SectionKeys.Pickle, 12),
            Section(SectionKeys.Straighten, 9),
            Section(SectionKeys.Warehouse, 24), // 启用但应排除（非生产待产位置）
        };
        var svc = CreateService(ctx, processes, sections);

        var result = await svc.GetStatusAsync();

        result.Should().HaveCount(12); // 2 工序 × 2 工段（入库排除）× 3 产类行
        result.Select(r => r.ProcessGroupName).Distinct()
            .Should().BeEquivalentTo(new[] { ProcessKeys.RoughTubeProcessing, ProcessKeys.ColdRoll60 });
        result.Select(r => r.SectionName).Distinct()
            .Should().BeEquivalentTo(new[] { SectionKeys.Pickle, SectionKeys.Straighten });
        result.Should().NotContain(r => r.SectionName == SectionKeys.Warehouse);
        // 每(工序组,工段)固定输出 RoughTube/InProgress/Finished 三行
        foreach (var pg in new[] { ProcessKeys.RoughTubeProcessing, ProcessKeys.ColdRoll60 })
        {
            foreach (var sec in new[] { SectionKeys.Pickle, SectionKeys.Straighten })
            {
                result.Where(r => r.ProcessGroupName == pg && r.SectionName == sec).Select(r => r.ProductStatus)
                    .Should().BeEquivalentTo(new[]
                    {
                        ProductStatuses.RoughTube, ProductStatuses.InProgress,
                        ProductStatuses.Finished
                    });
            }
        }
        result.Should().OnlyContain(r => r.InProduction == null && r.PendingProduction == null
            && r.Total == null);
    }

    [Fact]
    public async Task GetStatusAsync_批次引用的配置外工序不出现()
    {
        using var ctx = CreateDbContext();
        // 批次在 (RoughTubeProcessing, Pickle) 生产中，但配置仅含 ColdRoll60 工序
        var batch = CreateBatch(ctx, "B001", BatchStatus.InProgress,
            currentGroupName: ProcessKeys.RoughTubeProcessing, currentSectionName: SectionKeys.Pickle,
            currentSectionCompleted: false, currentValidWeight: 2000);
        await ctx.SaveChangesAsync();
        AddProcessGroups(ctx, batch, ProcessKeys.RoughTubeProcessing);

        var svc = CreateService(ctx,
            processes: new[] { Process(ProcessKeys.ColdRoll60) },
            sections: new[] { Section(SectionKeys.Pickle) });

        var result = await svc.GetStatusAsync();

        result.Should().HaveCount(3); // ColdRoll60×Pickle 产类三行
        result.Select(r => r.ProcessGroupName).Distinct()
            .Should().BeEquivalentTo(new[] { ProcessKeys.ColdRoll60 });
        result.Should().OnlyContain(r => r.InProduction == null); // 批次权重落在 RoughTubeProcessing 维度，未落入配置维度
    }

    // ==================== 聚合 ====================

    [Fact]
    public async Task GetStatusAsync_生产中与待产量聚合到对应维度()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", BatchStatus.InProgress,
            currentGroupName: ProcessKeys.ColdRoll60, currentSectionName: SectionKeys.Pickle,
            currentSectionCompleted: false, currentValidWeight: 2000);
        CreateBatch(ctx, "B002", BatchStatus.InProgress,
            nextProcess: ProcessKeys.ColdRoll60, nextSectionName: SectionKeys.Straighten,
            currentSectionCompleted: true, currentValidWeight: 1500);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx,
            processes: new[] { Process(ProcessKeys.ColdRoll60) },
            sections: new[] { Section(SectionKeys.Pickle), Section(SectionKeys.Straighten), Section(SectionKeys.Warehouse) });

        var result = await svc.GetStatusAsync();

        // 两批次无工序组制造规格（spec=null），产类均判定为在制 InProgress
        var pickleInProg = result.Single(r => r.SectionName == SectionKeys.Pickle && r.ProductStatus == ProductStatuses.InProgress);
        pickleInProg.InProduction.Should().Be(2000m);
        pickleInProg.PendingProduction.Should().BeNull();
        pickleInProg.Total.Should().Be(2000m);

        var straightenInProg = result.Single(r => r.SectionName == SectionKeys.Straighten && r.ProductStatus == ProductStatuses.InProgress);
        straightenInProg.InProduction.Should().BeNull();
        straightenInProg.PendingProduction.Should().Be(1500m);
        straightenInProg.Total.Should().Be(1500m);

        // 同工段 RoughTube/Finished 行不落重量
        result.Where(r => r.SectionName == SectionKeys.Pickle && r.ProductStatus == ProductStatuses.RoughTube)
            .Should().OnlyContain(r => r.InProduction == null && r.PendingProduction == null);
        result.Where(r => r.SectionName == SectionKeys.Pickle && r.ProductStatus == ProductStatuses.Finished)
            .Should().OnlyContain(r => r.InProduction == null && r.PendingProduction == null);
    }

    [Fact]
    public async Task GetStatusAsync_未产批次无当前工序_按下一工序工段计入待产量()
    {
        using var ctx = CreateDbContext();
        // 未产批次（Status=None）：无当前工序组（CurrentGroupName 为空）→ CurrentSectionCompleted=null，
        // 但持有下一步待产动作（NextProcess/NextSectionName），须按下一工序/下一工段计入待产维度
        CreateBatch(ctx, "B001", BatchStatus.None,
            currentGroupName: null, currentSectionName: null,
            currentSectionCompleted: null,
            nextProcess: ProcessKeys.ColdRoll60, nextSectionName: SectionKeys.Straighten,
            currentValidWeight: 2000);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx,
            processes: new[] { Process(ProcessKeys.ColdRoll60) },
            sections: new[] { Section(SectionKeys.Straighten) });

        var result = await svc.GetStatusAsync();

        var straightenInProg = result.Single(r => r.SectionName == SectionKeys.Straighten && r.ProductStatus == ProductStatuses.InProgress);
        straightenInProg.InProduction.Should().BeNull();
        straightenInProg.PendingProduction.Should().Be(2000m);
        straightenInProg.Total.Should().Be(2000m);
    }

    [Fact]
    public async Task GetStatusAsync_成检批次不计入统计()
    {
        using var ctx = CreateDbContext();
        // B001 在产 → 计入；B002 成检（已完成生产、质量检验阶段）→ 排除
        CreateBatch(ctx, "B001", BatchStatus.InProgress,
            currentGroupName: ProcessKeys.ColdRoll60, currentSectionName: SectionKeys.Pickle,
            currentSectionCompleted: false, currentValidWeight: 1000);
        CreateBatch(ctx, "B002", BatchStatus.InFinalInspection,
            currentGroupName: ProcessKeys.ColdRoll60, currentSectionName: SectionKeys.Pickle,
            currentSectionCompleted: false, currentValidWeight: 2000);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx,
            processes: new[] { Process(ProcessKeys.ColdRoll60) },
            sections: new[] { Section(SectionKeys.Pickle) });

        var result = await svc.GetStatusAsync();

        var pickleInProg = result.Single(r => r.SectionName == SectionKeys.Pickle && r.ProductStatus == ProductStatuses.InProgress);
        pickleInProg.InProduction.Should().Be(1000m);
        pickleInProg.Total.Should().Be(1000m);
    }

    [Fact]
    public async Task GetStatusAsync_同工段不同产类批次分列_产类行重量正确()
    {
        using var ctx = CreateDbContext();
        // 荒管批次：当前工序组=荒管处理 → 产类 RoughTube（RoughTubeProcessing 工序组无成品规格）
        var b1 = CreateBatch(ctx, "B001", BatchStatus.InProgress,
            currentGroupName: ProcessKeys.RoughTubeProcessing, currentSectionName: SectionKeys.Pickle,
            currentSectionCompleted: false, currentValidWeight: 1000);
        // 成品批次：冷轧60 工序组制造规格==批次规格("219*8")，制造物品=OrderFinished → 产类 Finished
        var b2 = CreateBatch(ctx, "B002", BatchStatus.InProgress,
            currentGroupName: ProcessKeys.ColdRoll60, currentSectionName: SectionKeys.Pickle,
            currentSectionCompleted: false, currentValidWeight: 500);
        // 在制批次：冷轧60 工序组制造规格≠批次规格 → 产类 InProgress
        var b3 = CreateBatch(ctx, "B003", BatchStatus.InProgress,
            currentGroupName: ProcessKeys.ColdRoll60, currentSectionName: SectionKeys.Pickle,
            currentSectionCompleted: false, currentValidWeight: 300);
        await ctx.SaveChangesAsync();

        AddProcessGroups(ctx, b1, ProcessKeys.RoughTubeProcessing, ProcessKeys.ColdRoll60);
        AddProcessGroup(ctx, b2, ProcessKeys.RoughTubeProcessing, null, 1);
        AddProcessGroup(ctx, b2, ProcessKeys.ColdRoll60, "219*8", 2);
        AddProcessGroup(ctx, b3, ProcessKeys.RoughTubeProcessing, null, 1);
        AddProcessGroup(ctx, b3, ProcessKeys.ColdRoll60, "168*6", 2);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx,
            processes: new[] { Process(ProcessKeys.RoughTubeProcessing), Process(ProcessKeys.ColdRoll60) },
            sections: new[] { Section(SectionKeys.Pickle) });

        var result = await svc.GetStatusAsync();

        // 荒管批次落入 (RoughTubeProcessing, Pickle) 的 RoughTube 行
        var roughPickle = result.Single(r => r.ProcessGroupName == ProcessKeys.RoughTubeProcessing
            && r.SectionName == SectionKeys.Pickle && r.ProductStatus == ProductStatuses.RoughTube);
        roughPickle.InProduction.Should().Be(1000m);

        // 成品/在制批次落入 (ColdRoll60, Pickle) 对应产类行
        var coldFinished = result.Single(r => r.ProcessGroupName == ProcessKeys.ColdRoll60
            && r.SectionName == SectionKeys.Pickle && r.ProductStatus == ProductStatuses.Finished);
        coldFinished.InProduction.Should().Be(500m);
        var coldInProg = result.Single(r => r.ProcessGroupName == ProcessKeys.ColdRoll60
            && r.SectionName == SectionKeys.Pickle && r.ProductStatus == ProductStatuses.InProgress);
        coldInProg.InProduction.Should().Be(300m);
    }

    [Fact]
    public async Task GetStatusAsync_计划流转量与重点批重量_按批次计划档位聚合()
    {
        using var ctx = CreateDbContext();
        // 生产中批次：产类=在制（无制造规格）
        var b1 = CreateBatch(ctx, "B001", BatchStatus.InProgress,
            currentGroupName: ProcessKeys.ColdRoll60, currentSectionName: SectionKeys.Pickle,
            currentSectionCompleted: false, currentValidWeight: 2000);
        // 待产量批次：产类=在制
        var b2 = CreateBatch(ctx, "B002", BatchStatus.InProgress,
            nextProcess: ProcessKeys.ColdRoll60, nextSectionName: SectionKeys.Straighten,
            currentSectionCompleted: true, currentValidWeight: 1500);
        await ctx.SaveChangesAsync(); // 先保存拿到真实 Id

        // 批次计划：B001 流转=是 + 等级=急+（ScheduleTier=1）；B002 流转=是 + 等级=急（ScheduleTier=2，非急+）
        var planItems = new List<BatchPlanDto>
        {
            new()
            {
                BatchId = b1.Id,
                UrgencyLevel = UrgencyLevelKeys.APlusUrgent,
                ProductionFlowProperty = ProductionFlowKeys.Normal,
                AttentionMatchesCurrentCR = true,
                CR_CompletionType = "All", // 在轧要求=All → 流转=是
            },
            new()
            {
                BatchId = b2.Id,
                UrgencyLevel = UrgencyLevelKeys.APlusUrgent,
                ProductionFlowProperty = ProductionFlowKeys.Normal,
                AttentionMatchesCurrentCR = false,
                CR_CompletionType = "All",
            },
        };

        var svc = CreateService(ctx,
            processes: new[] { Process(ProcessKeys.ColdRoll60) },
            sections: new[] { Section(SectionKeys.Pickle), Section(SectionKeys.Straighten) },
            planItems: planItems);

        var result = await svc.GetStatusAsync();

        // B001（生产中）：计划流转量=2000、重点批重量=2000（急+）
        var pickleInProg = result.Single(r => r.SectionName == SectionKeys.Pickle && r.ProductStatus == ProductStatuses.InProgress);
        pickleInProg.InProduction.Should().Be(2000m);
        pickleInProg.PlanFlowQuantity.Should().Be(2000m);
        pickleInProg.PlanKeyWeight.Should().Be(2000m);

        // B002（待产量）：计划流转量=1500、重点批重量=null（急 非急+）
        var straightenInProg = result.Single(r => r.SectionName == SectionKeys.Straighten && r.ProductStatus == ProductStatuses.InProgress);
        straightenInProg.PendingProduction.Should().Be(1500m);
        straightenInProg.PlanFlowQuantity.Should().Be(1500m);
        straightenInProg.PlanKeyWeight.Should().BeNull();

        // 未在批次计划中的批次不计入计划流转/重点批（默认 planItems 为空场景由其他测试隐式覆盖）
        var roughPickle = result.Single(r => r.ProcessGroupName == ProcessKeys.ColdRoll60
            && r.SectionName == SectionKeys.Pickle && r.ProductStatus == ProductStatuses.RoughTube);
        roughPickle.PlanFlowQuantity.Should().BeNull();
        roughPickle.PlanKeyWeight.Should().BeNull();
    }
}
