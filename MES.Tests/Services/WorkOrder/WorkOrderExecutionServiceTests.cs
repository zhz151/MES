using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services;
using MES.Tests.Tests;
using Moq;

namespace MES.Tests.Services;

/// <summary>
/// 工单执行状况服务测试：分页查询、关键字搜索、排序、全量刷新
/// </summary>
public class WorkOrderExecutionServiceTests : TestBase
{
    private WorkOrderExecutionService CreateService(AppDbContext ctx)
    {
        var loggerMock = new Mock<ILogger<WorkOrderExecutionService>>();
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        return new WorkOrderExecutionService(ctx, loggerMock.Object, configMock.Object);
    }

    // ==================== GetPagedAsync 测试 ====================

    [Fact]
    public async Task GetPagedAsync_无关键字_返回全部()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", "SO001", "D01");
        SeedSummary(ctx, "WO002", "SO002", "D02");
        SeedSummary(ctx, "WO003", "SO003", "D03");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SortBy = "WorkOrderNo",
            IsDescending = false
        });

        result.TotalCount.Should().Be(3);
        result.Items.Should().HaveCount(3);
        result.Items[0].WorkOrderNo.Should().Be("WO001");
    }

    [Fact]
    public async Task GetPagedAsync_关键字匹配工单号()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", "SO001", "D01");
        SeedSummary(ctx, "WO002", "SO002", "D02");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Keyword = "WO001"
        });

        result.TotalCount.Should().Be(1);
        result.Items.Single().WorkOrderNo.Should().Be("WO001");
    }

    [Fact]
    public async Task GetPagedAsync_关键字匹配客户名称()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", "SO001", "D01", customerName: "测试客户A");
        SeedSummary(ctx, "WO002", "SO002", "D02", customerName: "测试客户B");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Keyword = "客户A"
        });

        result.TotalCount.Should().Be(1);
        result.Items.Single().CustomerName.Should().Be("测试客户A");
    }

    [Fact]
    public async Task GetPagedAsync_关键字匹配规格()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", "SO001", "D01", specification: "219*8");
        SeedSummary(ctx, "WO002", "SO002", "D02", specification: "273*10");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Keyword = "219"
        });

        result.TotalCount.Should().Be(1);
        result.Items.Single().Specification.Should().Be("219*8");
    }

    [Fact]
    public async Task GetPagedAsync_关键字匹配次号()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", "SO001", "D01", subNo: "C01");
        SeedSummary(ctx, "WO002", "SO002", "D02", subNo: null);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Keyword = "C01"
        });

        result.TotalCount.Should().Be(1);
        result.Items.Single().ProductionSubNo.Should().Be("C01");
    }

    [Fact]
    public async Task GetPagedAsync_排序按工单号升序()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO003", "SO001", "D01");
        SeedSummary(ctx, "WO001", "SO002", "D02");
        SeedSummary(ctx, "WO002", "SO003", "D03");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SortBy = "WorkOrderNo",
            IsDescending = false
        });

        result.Items.Select(i => i.WorkOrderNo).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetPagedAsync_排序按工单号降序()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO003", "SO001", "D01");
        SeedSummary(ctx, "WO001", "SO002", "D02");
        SeedSummary(ctx, "WO002", "SO003", "D03");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SortBy = "WorkOrderNo",
            IsDescending = true
        });

        result.Items.Select(i => i.WorkOrderNo).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetPagedAsync_排序按总重量()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", "SO001", "D01", totalWeight: 1000m);
        SeedSummary(ctx, "WO002", "SO002", "D02", totalWeight: 3000m);
        SeedSummary(ctx, "WO003", "SO003", "D03", totalWeight: 2000m);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SortBy = "TotalWeight",
            IsDescending = false
        });

        result.Items.Select(i => i.TotalWeight).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetPagedAsync_分页正确()
    {
        using var ctx = CreateDbContext();
        for (int i = 1; i <= 10; i++)
            SeedSummary(ctx, $"WO{i:D3}", $"SO{i:D3}", $"D{i:D2}");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);

        var page1 = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 3,
            SortBy = "WorkOrderNo",
            IsDescending = false
        });

        page1.TotalCount.Should().Be(10);
        page1.Items.Should().HaveCount(3);
        page1.Items.Select(i => i.WorkOrderNo).Should().Equal("WO001", "WO002", "WO003");

        var page2 = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 2,
            PageSize = 3,
            SortBy = "WorkOrderNo",
            IsDescending = false
        });

        page2.Items.Should().HaveCount(3);
        page2.Items.Select(i => i.WorkOrderNo).Should().Equal("WO004", "WO005", "WO006");
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
    public async Task GetPagedAsync_关键字无匹配_返回空()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", "SO001", "D01");
        SeedSummary(ctx, "WO002", "SO002", "D02");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Keyword = "NONEXISTENT"
        });

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_排序按投料成品比()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", "SO001", "D01", ratio: 50m);
        SeedSummary(ctx, "WO002", "SO002", "D02", ratio: 30m);
        SeedSummary(ctx, "WO003", "SO003", "D03", ratio: 80m);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SortBy = "InputOutputRatio",
            IsDescending = true
        });

        result.Items.Select(i => i.InputOutputRatio).Should().BeInDescendingOrder();
    }

    // ==================== RefreshAllAsync 测试 ====================

    [Fact]
    public async Task RefreshAllAsync_无工单_返回零计数()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.RefreshAllAsync();

        result.TotalWorkOrders.Should().Be(0);
        result.RefreshedCount.Should().Be(0);
    }

    [Fact]
    public async Task RefreshAllAsync_跳过未生成和已取消工单()
    {
        using var ctx = CreateDbContext();
        // Status = NotGenerated → should be skipped
        ctx.WorkOrders.Add(CreateWorkOrder("WO001", "SO001", WorkOrderStatus.NotGenerated));
        // Status = Cancelled → should be skipped
        ctx.WorkOrders.Add(CreateWorkOrder("WO002", "SO002", WorkOrderStatus.Cancelled));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.RefreshAllAsync();

        result.TotalWorkOrders.Should().Be(0);
        result.RefreshedCount.Should().Be(0);
    }

    [Fact]
    public async Task RefreshAllAsync_基本刷新_单工单无批次()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder
        {
            OrderNumber = "SO001",
            SignDate = DateTime.Today,
            CustomerId = cust.Id,
            Status = SalesOrderStatus.Confirmed,
            RowVersion = new byte[8]
        };
        ctx.SalesOrders.Add(so);

        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed,
            salesman: "业务员A", mainNo: "D01");
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.RefreshAllAsync();

        result.TotalWorkOrders.Should().Be(1);
        result.RefreshedCount.Should().Be(1);

        var summaries = await ctx.Set<WorkOrderExecutionSummary>().ToListAsync();
        summaries.Should().HaveCount(1);
        var s = summaries[0];
        s.WorkOrderNo.Should().Be("WO001");
        s.CustomerName.Should().Be("测试客户");
        s.Salesman.Should().Be("测试业务员");
        s.TotalBatchCount.Should().Be(0);
        s.InputQuantity.Should().Be(0);
        s.InputWeight.Should().Be(0);
        s.TheoreticalOutputQty.Should().Be(0);
        s.TheoreticalOutputWeight.Should().Be(0);
        s.InputOutputRatio.Should().Be(0);
        s.InputStatus.Should().Be(0); // 未投料
        s.ValidBatchCount.Should().Be(0);
        s.LastRefreshTime.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshAllAsync_定尺投料比计算()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, CustomerId = cust.Id, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8] };
        ctx.SalesOrders.Add(so);

        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed,
            salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed,
            totalQty: 100, totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // Seed a batch with production ratio (定尺)
        var batch = new ProductionBatch
        {
            BatchNo = "B001",
            Status = BatchStatus.InProgress,
            WorkOrderNo = "WO001",
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "业务员A",
            DeliveryDate = DateTime.Today.AddMonths(1),
            MaterialName = "无缝管",
            SettlementMethod = "理算",
            StandardCode = "GB/T 8163",
            DeliveryState = "固溶酸洗",
            LengthStatus = "Fixed",
            ManufacturingItem = "OrderFinishedProduct",
            PlantGrade = "304",
            Specification = "219*8",
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = 2500m,
            TotalItemCount = 1,
            TechnicalRequirements = "NORMAL",
            InputQuantity = 50,
            InputWeight = 1250m,
            CurrentValidQty = 50,
            CurrentValidWeight = 1250m,
            ProductionRatio = 2,
            RowVersion = new byte[8],
            ProcessGroups = new List<ProcessGroup>
            {
                new() { ProcessName = "60冷轧", SequenceNumber = 1, ColdRollDraw = 1, Solution = 2 }
            }
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        // 定尺：理论成品支数 = 50 * 2 = 100，成品比 = 100/100 * 100 = 100%
        s.TotalBatchCount.Should().Be(1);
        s.InputQuantity.Should().Be(50);
        s.InputWeight.Should().Be(1250m);
        s.TheoreticalOutputQty.Should().Be(100); // 50 * 2
        // 有效工序段数 = 1（HasAnySection 按 ProcessGroup 计数），折扣 = 1 - 1*0.025 = 0.975
        // 理论成品重量 = 1250 * 0.975 = 1218.75
        s.TheoreticalOutputWeight.Should().Be(1218.75m);
        s.InputOutputRatio.Should().Be(100); // 100/100*100
        s.InputStatus.Should().Be(2); // 满足
    }

    [Fact]
    public async Task RefreshAllAsync_非定尺投料比按重量()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, CustomerId = cust.Id, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8] };
        ctx.SalesOrders.Add(so);

        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed,
            salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.NonFixed,
            totalQty: 100, totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        var batch = new ProductionBatch
        {
            BatchNo = "B001",
            Status = BatchStatus.InProgress,
            WorkOrderNo = "WO001",
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "业务员A",
            DeliveryDate = DateTime.Today.AddMonths(1),
            MaterialName = "无缝管",
            SettlementMethod = "理算",
            StandardCode = "GB/T 8163",
            DeliveryState = "固溶酸洗",
            LengthStatus = "Unlimited",
            ManufacturingItem = "OrderFinishedProduct",
            PlantGrade = "304",
            Specification = "219*8",
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = 2500m,
            TotalItemCount = 1,
            TechnicalRequirements = "NORMAL",
            InputQuantity = 50,
            InputWeight = 1250m,
            CurrentValidQty = 50,
            CurrentValidWeight = 1250m,
            RowVersion = new byte[8],
            ProcessGroups = new List<ProcessGroup>
            {
                new() { ProcessName = "60冷轧", SequenceNumber = 1, ColdRollDraw = 1, Solution = 2 }
            }
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        // 非定尺：理论成品重量 = 1250 * (1 - 1*0.025) = 1218.75
        // 成品比 = 1218.75 / 2500 * 100 = 48.75
        s.InputOutputRatio.Should().Be(48.75m);
        s.InputStatus.Should().Be(1); // 部分
    }

    [Fact]
    public async Task RefreshAllAsync_作废批次排除Group4()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, CustomerId = cust.Id, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8] };
        ctx.SalesOrders.Add(so);

        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed,
            salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed,
            totalQty: 100, totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // 有效批次（在产）
        var validBatch = new ProductionBatch
        {
            BatchNo = "B001", Status = BatchStatus.InProgress,
            WorkOrderNo = "WO001", SalesOrderNo = "SO001", ProductionMainNo = "D01",
            OrderItemIds = "1", SignDate = DateTime.Today, Salesman = "业务员A",
            DeliveryDate = DateTime.Today.AddMonths(1),
            MaterialName = "无缝管", SettlementMethod = "理算", StandardCode = "GB/T 8163",
            DeliveryState = "固溶酸洗", LengthStatus = "Fixed",
            ManufacturingItem = "OrderFinishedProduct",
            PlantGrade = "304", Specification = "219*8",
            TotalQuantity = 100, TotalMeters = 600, TotalWeight = 2500m,
            TotalItemCount = 1, TechnicalRequirements = "NORMAL",
            InputQuantity = 50, InputWeight = 1250m,
            CurrentValidQty = 50, CurrentValidWeight = 1250m,
            ProductionRatio = 1,
            RowVersion = new byte[8],
            ProcessGroups = new List<ProcessGroup>
            {
                new() { ProcessName = "60冷轧", SequenceNumber = 1, ColdRollDraw = 1 }
            }
        };
        // 作废批次
        var cancelledBatch = new ProductionBatch
        {
            BatchNo = "B002", Status = BatchStatus.Cancelled,
            WorkOrderNo = "WO001", SalesOrderNo = "SO001", ProductionMainNo = "D01",
            OrderItemIds = "1", SignDate = DateTime.Today, Salesman = "业务员A",
            DeliveryDate = DateTime.Today.AddMonths(1),
            MaterialName = "无缝管", SettlementMethod = "理算", StandardCode = "GB/T 8163",
            DeliveryState = "固溶酸洗", LengthStatus = "Fixed",
            ManufacturingItem = "OrderFinishedProduct",
            PlantGrade = "304", Specification = "219*8",
            TotalQuantity = 100, TotalMeters = 600, TotalWeight = 2500m,
            TotalItemCount = 1, TechnicalRequirements = "NORMAL",
            InputQuantity = 30, InputWeight = 750m,
            CurrentValidQty = 0, CurrentValidWeight = 0,
            ProductionRatio = 1,
            RowVersion = new byte[8],
            ProcessGroups = new List<ProcessGroup>
            {
                new() { ProcessName = "60冷轧", SequenceNumber = 1, ColdRollDraw = 1 }
            }
        };
        ctx.ProductionBatches.AddRange(validBatch, cancelledBatch);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        // Group 3: 包括所有批次
        s.TotalBatchCount.Should().Be(2);
        s.InputQuantity.Should().Be(80); // 50+30

        // Group 4: 排除作废批次
        s.ValidBatchCount.Should().Be(1);
        s.ValidInputQuantity.Should().Be(50); // CurrentValidQty of valid batch only
        s.ValidInputWeight.Should().Be(1250m); // CurrentValidWeight of valid batch only
    }

    [Fact]
    public async Task RefreshAllAsync_用料计划日期取最大值()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, CustomerId = cust.Id, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8] };
        ctx.SalesOrders.Add(so);

        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed,
            salesman: "业务员A", mainNo: "D01");
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // 多种计划日期
        ctx.Set<PurchaseSemiPlan>().Add(new PurchaseSemiPlan
        {
            WorkOrderId = wo.Id,
            PlanDate = new DateTime(2026, 6, 15),
            PlantGrade = "304",
            RawMaterialType = RawMaterialType.SemiFinished,
            RawMaterialSpec = "245*10",
            RequiredWeight = 1000,
            RequiredDate = new DateTime(2026, 6, 15),
            AdjustedWallThickness = 8m,
            YieldRate = 90m,
            InputMultiple = 1,
            QualifiedRate = 98m
        });
        ctx.Set<PurchaseFinishedPlan>().Add(new PurchaseFinishedPlan
        {
            WorkOrderId = wo.Id,
            PlanDate = new DateTime(2026, 7, 20),
            PlantGrade = "304",
            Specification = "219*8",
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m,
            WallThicknessPositive = 0.5m,
            LengthStatus = LengthStatus.Fixed,
            RequiredWeight = 2000,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        s.LatestPlanDate.Should().Be(new DateTime(2026, 7, 20)); // max of 6/15 and 7/20
    }

    [Fact]
    public async Task RefreshAllAsync_多工单分别创建汇总()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so1 = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, CustomerId = cust.Id, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8] };
        var so2 = new SalesOrder { OrderNumber = "SO002", SignDate = DateTime.Today, CustomerId = cust.Id, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8] };
        ctx.SalesOrders.AddRange(so1, so2);

        var wo1 = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01");
        var wo2 = CreateWorkOrder("WO002", "SO002", WorkOrderStatus.Confirmed, salesman: "业务员B", mainNo: "D02");
        ctx.WorkOrders.AddRange(wo1, wo2);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var summaries = await ctx.Set<WorkOrderExecutionSummary>().OrderBy(s => s.WorkOrderNo).ToListAsync();
        summaries.Should().HaveCount(2);
        summaries[0].WorkOrderNo.Should().Be("WO001");
        summaries[0].Salesman.Should().Be("测试业务员");
        summaries[1].WorkOrderNo.Should().Be("WO002");
        summaries[1].Salesman.Should().Be("测试业务员");
    }

    [Fact]
    public async Task RefreshAllAsync_Upsert更新已有记录()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, CustomerId = cust.Id, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8] };
        ctx.SalesOrders.Add(so);

        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01");
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // 创建已有汇总记录（模拟上一次刷新）
        var existing = new WorkOrderExecutionSummary
        {
            WorkOrderId = wo.Id,
            WorkOrderNo = "WO001",
            Salesman = "旧业务员",
            CustomerName = "",
            SettlementMethod = "理算",
            SignDate = DateTime.MinValue,
            DeliveryDate = DateTime.MinValue,
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            MaterialName = "",
            DeliveryState = "旧状态",
            PlantGrade = "",
            Specification = "",
            LengthStatus = "",
            TotalItemCount = 0,
            TotalQuantity = 0,
            TotalMeters = 0,
            TotalWeight = 0,
        };
        ctx.Set<WorkOrderExecutionSummary>().Add(existing);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        s.Salesman.Should().Be("测试业务员"); // 从 CustomerProfile 取最新值
    }

    [Fact]
    public async Task RefreshAllAsync_删除多余汇总记录()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, CustomerId = cust.Id, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8] };
        ctx.SalesOrders.Add(so);
        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01");
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // 创建一条不再对应任何工单的废弃汇总记录
        var stale = new WorkOrderExecutionSummary
        {
            WorkOrderId = 99999,
            WorkOrderNo = "STALE",
            Salesman = "",
            CustomerName = "",
            SettlementMethod = "理算",
            SignDate = DateTime.MinValue,
            DeliveryDate = DateTime.MinValue,
            SalesOrderNo = "",
            ProductionMainNo = "",
            MaterialName = "",
            DeliveryState = "",
            PlantGrade = "",
            Specification = "",
            LengthStatus = "",
        };
        ctx.Set<WorkOrderExecutionSummary>().Add(stale);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var summaries = await ctx.Set<WorkOrderExecutionSummary>().ToListAsync();
        summaries.Should().HaveCount(1);
        summaries[0].WorkOrderNo.Should().Be("WO001");
    }

    [Fact]
    public async Task RefreshAllAsync_MainNo聚合计算()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, CustomerId = cust.Id, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8] };
        ctx.SalesOrders.Add(so);

        // 同一主号(D01)下的两个工单
        var wo1 = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed,
            salesman: "业务员A", mainNo: "D01", subNo: "C01",
            lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m,
            planRate: 80m, planStatus: MaterialPlanStatus.Partial);
        var wo2 = CreateWorkOrder("WO002", "SO001", WorkOrderStatus.Confirmed,
            salesman: "业务员A", mainNo: "D01", subNo: "C02",
            lengthStatus: LengthStatus.Fixed, totalQty: 200, totalWeight: 5000m,
            planRate: 90m, planStatus: MaterialPlanStatus.Satisfied);
        ctx.WorkOrders.AddRange(wo1, wo2);
        await ctx.SaveChangesAsync();

        // 为工单创建用料计划（满足率从计划数据实时计算，不再依赖 WorkOrder 字段）
        // WO001 Fixed TotalQuantity=100 → 80%: RequiredPieces=40 × InputMultiple=2 = 80
        ctx.PurchaseSemiPlans.Add(new PurchaseSemiPlan
        {
            WorkOrderId = wo1.Id, PlanDate = DateTime.Today,
            RequiredPieces = 40, RequiredWeight = 1000m, InputMultiple = 2,
            PlantGrade = "304", RawMaterialSpec = "219*8", RequiredDate = DateTime.Today
        });
        // WO002 Fixed TotalQuantity=200 → 90%: RequiredPieces=60 × InputMultiple=3 = 180
        ctx.PurchaseSemiPlans.Add(new PurchaseSemiPlan
        {
            WorkOrderId = wo2.Id, PlanDate = DateTime.Today,
            RequiredPieces = 60, RequiredWeight = 1500m, InputMultiple = 3,
            PlantGrade = "304", RawMaterialSpec = "219*8", RequiredDate = DateTime.Today
        });
        await ctx.SaveChangesAsync();

        // 给每个工单加一个批次
        foreach (var wo in new[] { wo1, wo2 })
        {
            ctx.ProductionBatches.Add(new ProductionBatch
            {
                BatchNo = $"B-{wo.WorkOrderNo}",
                Status = BatchStatus.InProgress,
                WorkOrderNo = wo.WorkOrderNo,
                SalesOrderNo = "SO001",
                ProductionMainNo = "D01",
                OrderItemIds = "1",
                SignDate = DateTime.Today,
                Salesman = "业务员A",
                DeliveryDate = DateTime.Today.AddMonths(1),
                MaterialName = "无缝管",
                SettlementMethod = "理算",
                StandardCode = "GB/T 8163",
                DeliveryState = "固溶酸洗",
                LengthStatus = "Fixed",
                ManufacturingItem = "OrderFinishedProduct",
                PlantGrade = "304",
                Specification = "219*8",
                TotalQuantity = wo.TotalQuantity,
                TotalMeters = wo.TotalMeters,
                TotalWeight = wo.TotalWeight,
                TotalItemCount = 1,
                TechnicalRequirements = "NORMAL",
                InputQuantity = 50,
                InputWeight = 1250m,
                CurrentValidQty = 50,
                CurrentValidWeight = 1250m,
                ProductionRatio = 2,
                RowVersion = new byte[8],
                InboundDate = DateTime.Today,
                ProcessGroups = new List<ProcessGroup>
                {
                    new() { ProcessName = "60冷轧", SequenceNumber = 1, ColdRollDraw = 1, Solution = 2 }
                }
            });
        }
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var summaries = await ctx.Set<WorkOrderExecutionSummary>()
            .OrderBy(s => s.WorkOrderNo).ToListAsync();

        summaries.Should().HaveCount(2);

        // 两个同主号工单应有相同的 MainNo 聚合值
        foreach (var s in summaries)
        {
            // MainNo 用料计划：加权满足率 = (80*100 + 90*200)/(100+200) = 26000/300 = 86.67
            // Fixed 定尺 < 102% 为 Partial(1)
            s.MainNoMaterialPlanRate.Should().Be(86.67m);
            s.MainNoMaterialPlanStatus.Should().Be(1); // Partial

            // MainNo 投料聚合
            // 理论成品：两个工单各 50*2=100，合计 200
            // 合计需求：100+200=300
            // MainNo 比（定尺按支数）：(100+100)/(100+200)*100 = 200/300*100 = 66.67
            s.MainNoInputOutputRatio.Should().Be(66.67m);
            s.MainNoInputStatus.Should().Be(1); // 部分
        }
    }

    [Fact]
    public async Task RefreshAllAsync_过程组有效工序折扣计算()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, CustomerId = cust.Id, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8] };
        ctx.SalesOrders.Add(so);

        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed,
            salesman: "业务员A", mainNo: "D01",
            lengthStatus: LengthStatus.NonFixed, totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // 3个工序组，各有1个有效工序段 → effectiveGroupCount = 3
        // 折扣 = 1 - 3*0.025 = 0.925
        // 理论成品重量 = 2500 * 0.925 = 2312.5
        var batch = new ProductionBatch
        {
            BatchNo = "B001",
            Status = BatchStatus.InProgress,
            WorkOrderNo = "WO001",
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "业务员A",
            DeliveryDate = DateTime.Today.AddMonths(1),
            MaterialName = "无缝管",
            SettlementMethod = "理算",
            StandardCode = "GB/T 8163",
            DeliveryState = "固溶酸洗",
            LengthStatus = "Unlimited",
            ManufacturingItem = "OrderFinishedProduct",
            PlantGrade = "304",
            Specification = "219*8",
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = 2500m,
            TotalItemCount = 1,
            TechnicalRequirements = "NORMAL",
            InputQuantity = 100,
            InputWeight = 2500m,
            CurrentValidQty = 100,
            CurrentValidWeight = 2500m,
            RowVersion = new byte[8],
            InboundDate = DateTime.Today,
            ProcessGroups = new List<ProcessGroup>
            {
                new()
                {
                    ProcessName = "60冷轧1", SequenceNumber = 1,
                    ColdRollDraw = 1
                },
                new()
                {
                    ProcessName = "60冷轧2", SequenceNumber = 2,
                    Solution = 2
                },
                new()
                {
                    ProcessName = "60冷轧3", SequenceNumber = 3,
                    Straighten = 3
                }
            }
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        // 有效工序段数 = 3，折扣 = 1 - 3*0.025 = 0.925
        // 理论成品重量 = 2500 * 0.925 = 2312.5
        s.TheoreticalOutputWeight.Should().Be(2312.5m);
    }

    // ========== 筛选上下文 ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", "SO001", "D01", salesman: "张三", materialName: "无缝管", deliveryState: "固溶酸洗", plantGrade: "304", specification: "219*8", lengthStatus: "Fixed");
        SeedSummary(ctx, "WO002", "SO002", "D02", salesman: "李四", materialName: "焊管", deliveryState: "退火", plantGrade: "Q345B", specification: "273*10", lengthStatus: "Unlimited");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetFilterContextsAsync();

        result.Should().ContainKeys("WorkOrderNo", "Salesman", "CustomerName", "SalesOrderNo", "ProductionMainNo", "PlantGrade", "Specification");
        result["WorkOrderNo"].Should().BeEquivalentTo(new[] { "WO001", "WO002" }, options => options.WithStrictOrdering());
        result["Salesman"].Should().BeEquivalentTo(new[] { "张三", "李四" });
        result["ProductionSubNo"].Should().BeEmpty(); // SeedSummary 不设 subNo
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_各字段返回空列表()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        result.Should().ContainKeys("WorkOrderNo", "Salesman", "CustomerName", "SalesOrderNo", "ProductionMainNo", "ProductionSubNo", "PlantGrade", "Specification");
        foreach (var kvp in result)
            kvp.Value.Should().BeEmpty($"字段 {kvp.Key} 应返回空列表");
    }

    // ==================== 辅助方法 ====================

    private void SeedSummary(AppDbContext ctx,
        string workOrderNo,
        string salesOrderNo,
        string mainNo,
        string? subNo = null,
        string customerName = "",
        string specification = "",
        decimal totalWeight = 0,
        decimal ratio = 0,
        string salesman = "",
        string materialName = "",
        string deliveryState = "",
        string plantGrade = "",
        string lengthStatus = "Fixed")
    {
        ctx.Set<WorkOrderExecutionSummary>().Add(new WorkOrderExecutionSummary
        {
            WorkOrderId = Math.Abs(workOrderNo.GetHashCode()),
            WorkOrderNo = workOrderNo,
            Salesman = salesman,
            CustomerName = customerName,
            SettlementMethod = "理算",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            SalesOrderNo = salesOrderNo,
            ProductionMainNo = mainNo,
            ProductionSubNo = subNo,
            MaterialName = materialName,
            DeliveryState = deliveryState,
            PlantGrade = plantGrade,
            Specification = specification,
            LengthStatus = lengthStatus,
            TotalQuantity = 100,
            TotalMeters = 600,
            TotalWeight = totalWeight,
            InputOutputRatio = ratio
        });
    }

    private WorkOrder CreateWorkOrder(
        string workOrderNo,
        string salesOrderNo,
        WorkOrderStatus status,
        string salesman = "",
        string mainNo = "D01",
        string? subNo = null,
        LengthStatus lengthStatus = LengthStatus.Fixed,
        int totalQty = 100,
        decimal totalWeight = 2500m,
        decimal planRate = 0,
        MaterialPlanStatus planStatus = MaterialPlanStatus.NotPlanned)
    {
        return new WorkOrder
        {
            WorkOrderNo = workOrderNo,
            SalesOrderNo = salesOrderNo,
            ProductionMainNo = mainNo,
            ProductionSubNo = subNo,
            OrderItemIds = "1",
            Status = status,
            RowVersion = new byte[8],
            SignDate = DateTime.Today,
            Salesman = salesman,
            DeliveryDate = DateTime.Today.AddMonths(1),
            MaterialName = MaterialName.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            StandardCode = "GB/T 8163",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "304",
            Specification = "219*8",
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m,
            WallThicknessPositive = 0.5m,
            LengthStatus = lengthStatus,
            TotalQuantity = totalQty,
            TotalMeters = totalQty * 6,
            TotalWeight = totalWeight,
            TotalItemCount = 1,
            MaterialPlanStatus = planStatus,
            MaterialPlanRate = planRate
        };
    }
}
