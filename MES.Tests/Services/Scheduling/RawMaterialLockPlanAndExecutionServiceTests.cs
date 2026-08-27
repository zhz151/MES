using FluentAssertions;
using MES.Core.Constants;
using MES.Core.DTOs.Scheduling;
using MES.Core.Interfaces.Configuration;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.WorkOrder;
using MES.Services.Scheduling;
using MES.Tests.Tests;
using Moq;

namespace MES.Tests.Services;

/// <summary>
/// 原锁计划服务测试：G3 计算列筛选 + 待投料量汇总端点（标量/矩阵/截日，口径 = 前端 RecalculateSummary）
/// </summary>
public class RawMaterialLockPlanAndExecutionServiceTests : TestBase
{
    private static RawMaterialLockPlanAndExecutionService CreateService(AppDbContext ctx)
    {
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        return new RawMaterialLockPlanAndExecutionService(ctx, configMock.Object);
    }

    private static RawMaterialLockPlanAndExecutionService CreateService(
        AppDbContext ctx, Dictionary<string, decimal> dateBucketMap, Dictionary<string, decimal> processingDiscountMap)
    {
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync("DateBucket")).ReturnsAsync(dateBucketMap);
        configMock.Setup(x => x.GetConfigMapAsync("ProcessingDiscount")).ReturnsAsync(processingDiscountMap);
        return new RawMaterialLockPlanAndExecutionService(ctx, configMock.Object);
    }

    private void SeedComputedSummary(AppDbContext ctx, string workOrderNo, Action<WorkOrderExecutionSummary> configure)
    {
        var e = new WorkOrderExecutionSummary
        {
            WorkOrderId = Math.Abs(workOrderNo.GetHashCode()),
            WorkOrderNo = workOrderNo,
            Salesman = "测试",
            CustomerName = "测试客户",
            SettlementMethod = "Theoretical",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SalesOrderNo = "SO" + workOrderNo,
            ProductionMainNo = "D01",
            MaterialName = "无缝管",
            DeliveryState = "SolutionAnnealedAndPickled",
            PlantGrade = "304",
            Specification = "219*8",
            LengthStatus = "Fixed",
            TotalQuantity = 100
        };
        configure(e);
        ctx.Set<WorkOrderExecutionSummary>().Add(e);
    }

    [Fact]
    public async Task GetPagedAsync_筛选到料实投一致性_仅返回匹配档位()
    {
        using var ctx = CreateDbContext();
        // 原锁页仅查 ScheduleStage=2（原料锁定），不走 5/6 阶段门控，走原五态
        SeedComputedSummary(ctx, "WO001", e => { e.ScheduleStage = 2; e.InputWeight = 100m; e.SemiInWeight = 100m; });                                       // 已投=现可 → 一致(0)
        SeedComputedSummary(ctx, "WO002", e => { e.ScheduleStage = 2; e.InputWeight = 120m; e.SemiInWeight = 100m; });                                       // 超投 120>103 → 疑问-到料超投(3)
        SeedComputedSummary(ctx, "WO003", e => { e.ScheduleStage = 2; e.InputWeight = 50m; e.SemiInWeight = 100m; e.CutoffArrivalDate = DateTime.Today.AddDays(-1); }); // 滞后且料已到位 → 疑问-到料少投(2)
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);

        // 单档筛选：仅 WO002
        var r3 = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SortBy = "WorkOrderNo",
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "PlanInputConsistency", Operator = "in", Values = new List<string> { "3" } }
            }
        });
        r3.TotalCount.Should().Be(1);
        r3.Items.Single().WorkOrderNo.Should().Be("WO002");

        // 多档筛选：WO002 + WO003
        var r23 = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SortBy = "WorkOrderNo",
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "PlanInputConsistency", Operator = "in", Values = new List<string> { "2", "3" } }
            }
        });
        r23.TotalCount.Should().Be(2);
        r23.Items.Should().Contain(x => x.WorkOrderNo == "WO002");
        r23.Items.Should().Contain(x => x.WorkOrderNo == "WO003");

        // 全选 in(0..6)：本夹具 3 条全在原料锁定（五态路径），应全部命中
        var rAll = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SortBy = "WorkOrderNo",
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "PlanInputConsistency", Operator = "in", Values = new List<string> { "0", "1", "2", "3", "4", "5", "6" } }
            }
        });
        rAll.TotalCount.Should().Be(3);
    }

    // ========== GetPendingSummaryAsync ==========

    [Fact]
    public async Task GetPendingSummaryAsync_空数据_标量矩阵截日全零()
    {
        using var ctx = CreateDbContext();
        await ctx.SaveChangesAsync();

        var r = await CreateService(ctx).GetPendingSummaryAsync();

        r.TotalOrderCount.Should().Be(0);
        r.TotalWeight.Should().Be(0m);
        r.PendingWeight.Should().Be(0m);
        r.PurchaseCount.Should().Be(0);
        r.PurchaseWeight.Should().Be(0m);
        r.HasPurchaseData.Should().BeFalse();
        r.MatrixRowLabels.Should().HaveCount(RawMaterialLockRemarkKeys.All.Length);
        r.MatrixColumnLabels.Should().HaveCount(UrgencyLevelKeys.All.Length - 1);   // 排除 EPaused
        r.MatrixRows.Should().HaveCount(RawMaterialLockRemarkKeys.All.Length);
        r.MatrixRows.Should().AllSatisfy(row =>
        {
            row.Cells.Should().HaveCount(UrgencyLevelKeys.All.Length - 1);
            row.RowCount.Should().Be(0);
        });
        r.MatrixGrandTotals.Count.Should().Be(0);
        r.CutoffBucketLabels.Should().HaveCount(7);
        r.CutoffRows.Should().HaveCount(4);   // 完善计划/执行计划/外购成品/合计
        r.CutoffRows[3].Category.Should().Be("合计");
        r.CutoffRows[3].Total.Should().Be(0m);
    }

    [Fact]
    public async Task GetPendingSummaryAsync_普通分支_倍率折算减已投料()
    {
        using var ctx = CreateDbContext();
        // Total=1000, 成购缺口=200-100=100, base=(1000-100)*1.1=990, pending=990-100(已投)=890
        // 注：PiercingPlanWeight>0 使其非「单一成品采购」工单（方案 B 待投料排除口径），否则不计入标量
        SeedComputedSummary(ctx, "WO001", e =>
        {
            e.ScheduleStage = 2;
            e.TotalWeight = 1000m;
            e.FinishPlanWeight = 200m;
            e.FinishInWeight = 100m;
            e.InputWeight = 100m;
            e.PiercingPlanWeight = 300m;
            e.RawMaterialLockRemark = RawMaterialLockRemarkKeys.ImprovePlan;
        });
        await ctx.SaveChangesAsync();

        var r = await CreateService(ctx).GetPendingSummaryAsync();

        r.TotalOrderCount.Should().Be(1);
        r.TotalWeight.Should().Be(1000m);
        r.PendingWeight.Should().Be(890m);
        r.PurchaseCount.Should().Be(1);
        r.PurchaseWeight.Should().Be(100m);
        r.HasPurchaseData.Should().BeTrue();
    }

    [Fact]
    public async Task GetPendingSummaryAsync_质量补料分支_按流转比缺口折算不减已投料()
    {
        using var ctx = CreateDbContext();
        // Total=1000, 无成购, FlowOutputRatio=50, base=1000*1.1=1100, pending=1100*(1-0.5)=550（不减 Input 100）
        SeedComputedSummary(ctx, "WO001", e =>
        {
            e.ScheduleStage = 2;
            e.TotalWeight = 1000m;
            e.FinishPlanWeight = 0m;
            e.FinishInWeight = 0m;
            e.InputWeight = 100m;
            e.FlowOutputRatio = 50m;
            e.RawMaterialLockRemark = RawMaterialLockRemarkKeys.QualityReplenish;
        });
        await ctx.SaveChangesAsync();

        var r = await CreateService(ctx).GetPendingSummaryAsync();

        r.PendingWeight.Should().Be(550m);
    }

    [Fact]
    public async Task GetPendingSummaryAsync_矩阵归桶_排除EPaused_行列全表合计()
    {
        using var ctx = CreateDbContext();
        SeedComputedSummary(ctx, "WO001", e => { e.ScheduleStage = 2; e.TotalWeight = 500m; e.RawMaterialLockRemark = RawMaterialLockRemarkKeys.QualityReplenish; e.UrgencyLevel = UrgencyLevelKeys.APlusUrgent; });
        SeedComputedSummary(ctx, "WO002", e => { e.ScheduleStage = 2; e.TotalWeight = 600m; e.RawMaterialLockRemark = RawMaterialLockRemarkKeys.ImprovePlan; e.UrgencyLevel = UrgencyLevelKeys.BOrder; });
        // 暂停工单：不进矩阵（列排除 EPaused），但仍进标量
        SeedComputedSummary(ctx, "WO003", e => { e.ScheduleStage = 2; e.TotalWeight = 300m; e.RawMaterialLockRemark = RawMaterialLockRemarkKeys.ImprovePlan; e.UrgencyLevel = UrgencyLevelKeys.EPaused; });
        await ctx.SaveChangesAsync();

        var r = await CreateService(ctx).GetPendingSummaryAsync();

        r.TotalOrderCount.Should().Be(3);                       // 标量含暂停工单
        var qrRow = r.MatrixRows[Array.IndexOf(RawMaterialLockRemarkKeys.All, RawMaterialLockRemarkKeys.QualityReplenish)];
        qrRow.Cells[0].Count.Should().Be(1);                    // A质量补料 × A+急
        qrRow.RowCount.Should().Be(1);
        var ipRow = r.MatrixRows[Array.IndexOf(RawMaterialLockRemarkKeys.All, RawMaterialLockRemarkKeys.ImprovePlan)];
        ipRow.Cells[Array.IndexOf(UrgencyLevelKeys.All, UrgencyLevelKeys.BOrder)].Count.Should().Be(1);   // D完善计划 × B顺
        ipRow.RowCount.Should().Be(1);                          // EPaused 工单不入矩阵
        r.MatrixGrandTotals.Count.Should().Be(2);               // 仅 2 个非暂停工单入矩阵
        r.MatrixColumnTotals[0].Count.Should().Be(1);
        r.MatrixColumnTotals[2].Count.Should().Be(1);
        r.MatrixRowLabels.Should().NotContain("E停");
        r.MatrixColumnLabels.Should().NotContain("E停");
    }

    [Fact]
    public async Task GetPendingSummaryAsync_截日归桶_空末桶_三类行加合计()
    {
        using var ctx = CreateDbContext();
        // 完善计划 + 截止=today → 桶0「投料截止-今日」
        // 注：PiercingPlanWeight>0 使其非「单一成品采购」工单（方案 B 待投料排除口径），否则完善计划行不计 pending
        SeedComputedSummary(ctx, "WO001", e =>
        {
            e.ScheduleStage = 2;
            e.TotalWeight = 1000m;
            e.FinishPlanWeight = 200m;
            e.FinishInWeight = 100m;
            e.InputWeight = 0m;
            e.PiercingPlanWeight = 300m;
            e.RawMaterialLockRemark = RawMaterialLockRemarkKeys.ImprovePlan;
            e.TheoreticalCutoffDate = DateTime.Today;
        });
        // 执行计划 + 截止=null → 末桶「远日量」
        SeedComputedSummary(ctx, "WO002", e =>
        {
            e.ScheduleStage = 2;
            e.TotalWeight = 1000m;
            e.FinishPlanWeight = 0m;
            e.FinishInWeight = 0m;
            e.InputWeight = 0m;
            e.RawMaterialLockRemark = RawMaterialLockRemarkKeys.ExecutePlan;
            e.TheoreticalCutoffDate = null;
        });
        await ctx.SaveChangesAsync();

        var r = await CreateService(ctx).GetPendingSummaryAsync();

        // WO001：pending=(1000-100)*1.1=990 → 完善计划 桶0
        var improve = r.CutoffRows[0];
        improve.Category.Should().Be("完善计划");
        improve.Total.Should().Be(990m);
        improve.Buckets[0].Should().Be(990m);
        // WO002：pending=1000*1.1=1100 → 执行计划 末桶
        var execute = r.CutoffRows[1];
        execute.Category.Should().Be("执行计划");
        execute.Total.Should().Be(1100m);
        execute.Buckets[6].Should().Be(1100m);
        // 外购成品 = 全部成购缺口 = 100 + 0
        var purchase = r.CutoffRows[2];
        purchase.Category.Should().Be("外购成品");
        purchase.Total.Should().Be(100m);
        // 合计行
        var total = r.CutoffRows[3];
        total.Category.Should().Be("合计");
        total.Total.Should().Be(990m + 1100m + 100m);
        total.Buckets[0].Should().Be(990m + 100m);      // 完善计划990 + 外购成品100
        total.Buckets[6].Should().Be(1100m);            // 执行计划(空截止末桶) + 外购成品0
    }

    [Fact]
    public async Task GetPendingSummaryAsync_桶配置自定义_倍率配置生效()
    {
        using var ctx = CreateDbContext();
        // 倍率=1.2 → pending=100*1.2=120；桶边界 3/5/10/20/30
        SeedComputedSummary(ctx, "WO001", e =>
        {
            e.ScheduleStage = 2;
            e.TotalWeight = 100m;
            e.FinishPlanWeight = 0m;
            e.FinishInWeight = 0m;
            e.InputWeight = 0m;
            e.RawMaterialLockRemark = RawMaterialLockRemarkKeys.ExecutePlan;
        });
        await ctx.SaveChangesAsync();

        var dateMap = new Dictionary<string, decimal>
        {
            ["Bucket1"] = 3m, ["Bucket2"] = 5m, ["Bucket3"] = 10m, ["Bucket4"] = 20m, ["Bucket5"] = 30m
        };
        var procMap = new Dictionary<string, decimal> { ["RawMaterialRatio"] = 1.2m };
        var r = await CreateService(ctx, dateMap, procMap).GetPendingSummaryAsync();

        r.PendingWeight.Should().Be(120m);
        var today = DateTime.Today;
        r.CutoffBucketLabels.Should().Equal(
            $"≤{today:yy/M/d}",
            $"{today.AddDays(1):yy/M/d}-{today.AddDays(3):yy/M/d}",
            $"{today.AddDays(4):yy/M/d}-{today.AddDays(5):yy/M/d}",
            $"{today.AddDays(6):yy/M/d}-{today.AddDays(10):yy/M/d}",
            $"{today.AddDays(11):yy/M/d}-{today.AddDays(20):yy/M/d}",
            $"{today.AddDays(21):yy/M/d}-{today.AddDays(30):yy/M/d}",
            $"≥{today.AddDays(31):yy/M/d}");
    }
}
