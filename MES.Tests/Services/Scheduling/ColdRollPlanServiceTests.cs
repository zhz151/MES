using FluentAssertions;
using MES.Core.Enums;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Scheduling;
using MES.Tests.Tests;
using MES.Data.Entities.Batch;
using MES.Data.Entities.WorkOrder;

namespace MES.Tests.Services.Scheduling;

/// <summary>
/// 冷轧计划看板服务测试：按规格维度的生产批次时间桶重量分布聚合
/// </summary>
public class ColdRollPlanServiceTests : TestBase
{
    private ColdRollPlanService CreateService(AppDbContext ctx) => new(ctx);

    private ProductionBatch CreateBatch(AppDbContext ctx, string batchNo, string workOrderNo,
        string processName, int seqNumber, bool isFinished,
        decimal weight = 1000m,
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

    // ==================== GetPlanAsync 测试 ====================

    [Fact]
    public async Task GetPlanAsync_冷轧批次聚合为正确时间桶()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001", "60冷轧", 1, isFinished: false,
            currentGroupName: null, currentSectionName: null, weight: 2000m);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPlanAsync(null);

        result.Should().HaveCount(1);
        var row = result[0];
        row.ProcessType.Should().Be("60冷轧");
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
            CurrentValidWeight = 1000m,
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
        var batch = CreateBatch(ctx, "B001", "WO001", "60冷轧", 1, isFinished: false,
            currentGroupName: "60冷轧",
            currentSectionName: "冷轧拔",
            currentSectionCompleted: false,
            weight: 3000m);
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
        CreateBatch(ctx, "B001", "WO001", "60冷轧", 1, isFinished: false, weight: 1000m);
        CreateBatch(ctx, "B002", "WO002", "50冷轧", 1, isFinished: false, weight: 2000m);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPlanAsync("60冷轧");

        result.Should().HaveCount(1);
        result[0].ProcessType.Should().Be("60冷轧");
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
        CreateBatch(ctx, "B001", "WO001", "60冷轧", 1, isFinished: false, status: BatchStatus.Completed);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPlanAsync(null);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPlanAsync_急件标记()
    {
        using var ctx = CreateDbContext();
        CreateBatch(ctx, "B001", "WO001", "60冷轧", 1, isFinished: false,
            currentGroupName: null, currentSectionName: null, weight: 2000m);
        SeedSummary(ctx, "WO001", scheduleStage: 2, urgencyLevel: "A急",
            productionFlowProperty: "正常", mainNoAttentionProcess: "60冷轧");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPlanAsync(null);

        var row = result.Single();
        row.KeyBatchCount.Should().Be(1);
        row.WeightWaitNearUrgent.Should().Be(2000m);
    }
}
