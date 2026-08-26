using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs.Configuration;
using MES.Core.Interfaces.WorkOrder;
using MES.Services.Configuration;
using MES.Tests.Tests;
using MES.Data;
using MES.Core.Enums;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using WorkOrderEntity = MES.Data.Entities.WorkOrder.WorkOrder;

namespace MES.Tests.Services;

/// <summary>
/// 日产估算服务测试：保存/删除后刷新执行读模型与用料计划总览读模型
/// </summary>
public class DailyOutputEstimateServiceTests : TestBase
{
    private DailyOutputEstimateService CreateService(AppDbContext ctx,
        Mock<IWorkOrderExecutionService>? woExecMock = null,
        Mock<IWorkOrderListSummaryRefreshService>? listSummaryMock = null)
    {
        woExecMock ??= new Mock<IWorkOrderExecutionService>();
        listSummaryMock ??= new Mock<IWorkOrderListSummaryRefreshService>();
        var services = new ServiceCollection();
        services.AddSingleton(woExecMock.Object);
        services.AddSingleton(listSummaryMock.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new DailyOutputEstimateService(ctx, scopeFactory);
    }

    private async Task<WorkOrderEntity> SeedWorkOrderAsync(AppDbContext ctx, string workOrderNo = "WO-DOE-001")
    {
        var wo = new WorkOrderEntity
        {
            WorkOrderNo = workOrderNo,
            SalesOrderNo = "SO-DOE",
            ProductionMainNo = "X01",
            ProductionSubNo = "01",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "测试业务员",
            DeliveryDate = DateTime.Today.AddMonths(1),
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            StandardCode = "TEST-STD-NO",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "Q345B",
            Specification = "219*8",
            LengthStatus = LengthStatus.Fixed,
            MinLength = 6000m,
            MaxLength = 6000m,
            TotalQuantity = 10,
            TotalMeters = 0m,
            TotalWeight = 2500m,
            TotalItemCount = 1,
            Status = WorkOrderStatus.Pending,
            MaterialPlanStatus = MaterialPlanStatus.NotPlanned
        };
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();
        return wo;
    }

    [Fact]
    public async Task SaveAsync_新增配置_全量刷新执行读模型并刷新用料总览()
    {
        var ctx = CreateDbContext();
        await SeedWorkOrderAsync(ctx);

        var woExecMock = new Mock<IWorkOrderExecutionService>();
        var listSummaryMock = new Mock<IWorkOrderListSummaryRefreshService>();
        var svc = CreateService(ctx, woExecMock, listSummaryMock);

        var saved = await svc.SaveAsync(new DailyOutputEstimateDto
        {
            MinOuterDiameter = 100,
            DailyOutputTons = 50,
            Remark = "测试"
        });

        saved.Should().BeTrue();
        woExecMock.Verify(x => x.RefreshAllAsync(), Times.Once);
        listSummaryMock.Verify(x => x.RefreshBySalesOrderAsync("SO-DOE"), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_删除配置_全量刷新执行读模型()
    {
        var ctx = CreateDbContext();
        await SeedWorkOrderAsync(ctx);

        var saved = await CreateService(ctx).SaveAsync(new DailyOutputEstimateDto
        {
            MinOuterDiameter = 100,
            DailyOutputTons = 50,
            Remark = "待删"
        });
        saved.Should().BeTrue();

        var woExecMock = new Mock<IWorkOrderExecutionService>();
        var listSummaryMock = new Mock<IWorkOrderListSummaryRefreshService>();
        var svc = CreateService(ctx, woExecMock, listSummaryMock);

        var id = await ctx.Set<MES.Data.Entities.Configuration.DailyOutputEstimate>()
            .Select(e => e.Id).FirstAsync();
        var deleted = await svc.DeleteAsync(id);

        deleted.Should().BeTrue();
        woExecMock.Verify(x => x.RefreshAllAsync(), Times.Once);
    }
}
