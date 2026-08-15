using FluentAssertions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.WorkOrder;
using MES.Services.Scheduling;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 原锁计划服务测试：G3 计算列（到料实投一致性等）筛选已在 join 投影前接入 ApplyComputedFilters，
/// 此测试验证该字段筛选真正生效（此前仅通用 ApplyFilters 反射覆盖不到，筛选被静默忽略 → 恒全量）。
/// </summary>
public class RawMaterialLockPlanAndExecutionServiceTests : TestBase
{
    private static RawMaterialLockPlanAndExecutionService CreateService(AppDbContext ctx)
        => new(ctx);

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
}
