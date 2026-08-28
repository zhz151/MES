using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.Constants;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Core.Interfaces.Batch;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.DataExchange;
using MES.Core.Interfaces.Equipment;
using MES.Core.Interfaces.Infrastructure;
using MES.Core.Interfaces.Materials;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.StandardRegister;
using MES.Core.Interfaces.Quality;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Interfaces.Warehouse;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Models;
using MES.Services.WorkOrder;
using MES.Tests.Tests;
using Moq;


using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Order;
using MES.Data.Entities.Quality;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.WorkOrder;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace MES.Tests.Services;

/// <summary>
/// 工单执行状况服务测试：分页查询、关键字搜索、排序、全量刷新
/// </summary>
public class WorkOrderExecutionServiceTests : TestBase
{
    private WorkOrderExecutionService CreateService(AppDbContext ctx)
    {
        return CreateService(ctx, new List<DailyOutputEstimateDto>());
    }

    private WorkOrderExecutionService CreateService(AppDbContext ctx, List<DailyOutputEstimateDto> dailyEstimates)
    {
        var loggerMock = new Mock<ILogger<WorkOrderExecutionService>>();
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        var dailyOutputMock = new Mock<IDailyOutputEstimateService>();
        dailyOutputMock.Setup(x => x.GetAllAsync())
            .ReturnsAsync(dailyEstimates);
        // 配置 IServiceScopeFactory：CreateScope 返回可解析 IOrderService 的 scope（全量刷新末尾会刷新订单读模型）
        var orderServiceMock = new Mock<IOrderService>();
        orderServiceMock.Setup(x => x.RefreshByOrderIdAsync(It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        var providerMock = new Mock<IServiceProvider>();
        providerMock.Setup(x => x.GetService(typeof(IOrderService))).Returns(orderServiceMock.Object);
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(x => x.ServiceProvider).Returns(providerMock.Object);
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(x => x.CreateScope()).Returns(scopeMock.Object);
        // 档3（成品检验）复用成检计划看板：mock 返回空看板，避免影响既有用例
        var finalInspectionMock = new Mock<IFinalInspectionPlanService>();
        finalInspectionMock.Setup(x => x.GetKanbanAsync())
            .ReturnsAsync(new List<FinalInspectionPlanDto>());
        return new WorkOrderExecutionService(ctx, loggerMock.Object, configMock.Object, dailyOutputMock.Object, new MemoryCache(new MemoryCacheOptions()), scopeFactoryMock.Object, finalInspectionMock.Object);
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
    public async Task GetPagedAsync_关键字匹配用料占比()
    {
        using var ctx = CreateDbContext();
        SeedSummary(ctx, "WO001", "SO001", "D01", materialPlanProportion: "穿105% 荒60% 成20% 库40%");
        SeedSummary(ctx, "WO002", "SO002", "D02");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Keyword = "成20"
        });

        result.TotalCount.Should().Be(1);
        result.Items.Single().WorkOrderNo.Should().Be("WO001");
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

    // ==================== G3 计算列筛选（ApplyComputedFilters） ====================

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
    public async Task GetPagedAsync_筛选计划投料总重()
    {
        using var ctx = CreateDbContext();
        SeedComputedSummary(ctx, "WO001", e => { e.PiercingPlanWeight = 100m; e.SemiPlanWeight = 50m; });
        SeedComputedSummary(ctx, "WO002", e => { e.PiercingPlanWeight = 200m; e.SemiPlanWeight = 50m; });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SortBy = "WorkOrderNo",
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "TotalPlanWeight", Operator = "in", Values = new List<string> { "150" } }
            }
        });

        result.TotalCount.Should().Be(1);
        result.Items.Single().WorkOrderNo.Should().Be("WO001"); // 100+50=150
    }

    [Fact]
    public async Task GetPagedAsync_筛选现可投料总重()
    {
        using var ctx = CreateDbContext();
        SeedComputedSummary(ctx, "WO001", e => { e.PiercingSubInWeight = 100m; e.SemiInWeight = 50m; });
        SeedComputedSummary(ctx, "WO002", e => { e.PiercingSubInWeight = 200m; });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SortBy = "WorkOrderNo",
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "TotalAvailableWeight", Operator = "in", Values = new List<string> { "200" } }
            }
        });

        result.TotalCount.Should().Be(1);
        result.Items.Single().WorkOrderNo.Should().Be("WO002"); // 200+0=200
    }

    [Fact]
    public async Task GetPagedAsync_筛选理论缺失总料重()
    {
        using var ctx = CreateDbContext();
        // WO001：计划=150(穿孔100+荒管50)、可投(到货量)=100 → 缺失 50
        SeedComputedSummary(ctx, "WO001", e => { e.PiercingPlanWeight = 100m; e.SemiPlanWeight = 50m; e.SemiInWeight = 100m; });
        // WO002：计划=100、可投(到货量)=100 → 缺失 Max(0,0)=0
        SeedComputedSummary(ctx, "WO002", e => { e.PiercingPlanWeight = 100m; e.PiercingSubInWeight = 100m; });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SortBy = "WorkOrderNo",
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "TotalMissingWeight", Operator = "in", Values = new List<string> { "50" } }
            }
        });

        result.TotalCount.Should().Be(1);
        result.Items.Single().WorkOrderNo.Should().Be("WO001"); // Max(0, 150-100)=50
    }

    [Fact]
    public async Task GetPagedAsync_筛选计划实投一致性_错误档()
    {
        using var ctx = CreateDbContext();
        // WO001：已投=50 > 0 且 现可=0（无任何执行动作）→ 错误-无料已投(4)
        SeedComputedSummary(ctx, "WO001", e => { e.InputWeight = 50m; });
        // WO002：已投=100 = 现可(到货量) → 一致(0)
        SeedComputedSummary(ctx, "WO002", e => { e.InputWeight = 100m; e.SemiInWeight = 100m; });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SortBy = "WorkOrderNo",
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "PlanInputConsistency", Operator = "in", Values = new List<string> { "4" } }
            }
        });

        result.TotalCount.Should().Be(1);
        result.Items.Single().WorkOrderNo.Should().Be("WO001");
    }

    [Fact]
    public async Task GetPagedAsync_筛选计划实投一致性_疑问档()
    {
        using var ctx = CreateDbContext();
        // WO001：现可(到货量)=100，已投=120 > 100*1.03=103 → 超投 → 疑问-到料超投(3)
        SeedComputedSummary(ctx, "WO001", e => { e.SemiInWeight = 100m; e.InputWeight = 120m; });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SortBy = "WorkOrderNo",
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "PlanInputConsistency", Operator = "in", Values = new List<string> { "3" } }
            }
        });

        result.TotalCount.Should().Be(1);
        result.Items.Single().WorkOrderNo.Should().Be("WO001");
    }

    [Fact]
    public async Task GetPagedAsync_筛选计划实投一致性_待投档按截止到料日细分()
    {
        using var ctx = CreateDbContext();
        var today = DateTime.Today;
        // WO001：已投=50 < 现可=100×0.97（滞后）且 截止到料日=今天 → 待投(1)
        SeedComputedSummary(ctx, "WO001", e => { e.SemiInWeight = 100m; e.InputWeight = 50m; e.CutoffArrivalDate = today; });
        // WO002：同样滞后但 截止到料日空 → 一致(0)（料未到位）
        SeedComputedSummary(ctx, "WO002", e => { e.SemiInWeight = 100m; e.InputWeight = 50m; });
        // WO003：同样滞后且 截止到料日<今天 → 疑问-到料少投(2)（料已到位需投未投）
        SeedComputedSummary(ctx, "WO003", e => { e.SemiInWeight = 100m; e.InputWeight = 50m; e.CutoffArrivalDate = today.AddDays(-1); });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SortBy = "WorkOrderNo",
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "PlanInputConsistency", Operator = "in", Values = new List<string> { "1" } }
            }
        });

        result.TotalCount.Should().Be(1);
        result.Items.Single().WorkOrderNo.Should().Be("WO001");
    }

    [Fact]
    public async Task GetPagedAsync_筛选计划实投一致性_阶段门控档5档6()
    {
        using var ctx = CreateDbContext();
        // 阶段门控：主号关注=生产执行(3)/成品检验(4)/主号完成(1) → 已过投料期，仅按缺失量判定
        // WO001：生产执行 + 计划(穿孔100)=100 > 现可=0（缺失>0）→ 错误-无需投料(5)
        SeedComputedSummary(ctx, "WO001", e => { e.ScheduleStage = 3; e.PiercingPlanWeight = 100m; });
        // WO002：生产执行 + 计划=现可（缺失=0）→ 略(6)
        SeedComputedSummary(ctx, "WO002", e => { e.ScheduleStage = 3; e.PiercingPlanWeight = 100m; e.PiercingSubInWeight = 100m; });
        // WO003：主号完成 + 计划(荒管50)=50 < 现可=100（缺失=0）→ 略(6)
        SeedComputedSummary(ctx, "WO003", e => { e.ScheduleStage = 1; e.SemiPlanWeight = 50m; e.SemiInWeight = 100m; });
        // WO004：原料锁定(2) 不走门控 → 已投=现可 → 一致(0)
        SeedComputedSummary(ctx, "WO004", e => { e.ScheduleStage = 2; e.InputWeight = 100m; e.SemiInWeight = 100m; });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);

        // 筛选 in (5) → 仅 WO001
        var r5 = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SortBy = "WorkOrderNo",
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "PlanInputConsistency", Operator = "in", Values = new List<string> { "5" } }
            }
        });
        r5.TotalCount.Should().Be(1);
        r5.Items.Single().WorkOrderNo.Should().Be("WO001");

        // 筛选 in (6) → WO002 + WO003
        var r6 = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SortBy = "WorkOrderNo",
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "PlanInputConsistency", Operator = "in", Values = new List<string> { "6" } }
            }
        });
        r6.TotalCount.Should().Be(2);
        r6.Items.Should().Contain(x => x.WorkOrderNo == "WO002");
        r6.Items.Should().Contain(x => x.WorkOrderNo == "WO003");

        // 全选 in (0..6)（与前端"全选"发送一致）→ 应匹配所有行（WO001~WO004）
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
        rAll.TotalCount.Should().Be(4);
        rAll.Items.Should().Contain(x => x.WorkOrderNo == "WO001");
        rAll.Items.Should().Contain(x => x.WorkOrderNo == "WO002");
        rAll.Items.Should().Contain(x => x.WorkOrderNo == "WO003");
        rAll.Items.Should().Contain(x => x.WorkOrderNo == "WO004");
    }

    [Fact]
    public void PlanInputConsistency_阶段门控表达式_SQL可翻译_全选匹配所有行()
    {
        // 实证验证 EF 对 ApplyComputedFilters 的 planinputconsistency 内联表达式（含阶段门控嵌套三元）
        // 在真实 SQL Server provider 下可翻译（ToQueryString 编译表达式，不连库）。
        // 若翻译失败（InvalidOperationException）说明筛选查询在 SQL 场景会整体报错 → 前端空列表。
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=.;Database=__TranslationCheck;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;
        using var ctx = new AppDbContext(options);

        // 全选（与前端"全选"发送一致：in 0..6）
        var allVals = new List<int> { 0, 1, 2, 3, 4, 5, 6 };
        var q = ctx.Set<WorkOrderExecutionSummary>().Where(x => allVals.Contains(
            (x.ScheduleStage == 1 || x.ScheduleStage == 3 || x.ScheduleStage == 4)
                ? ((x.PiercingPlanWeight + x.SemiPlanWeight + x.FinishPlanWeight
                        + x.InventoryPlanWeight + x.ReworkPlanWeight + x.InProcessReworkPlanWeight + x.InMainPlanWeight)
                    - (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                        + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight)
                    > (x.PiercingPlanWeight + x.SemiPlanWeight + x.FinishPlanWeight
                        + x.InventoryPlanWeight + x.ReworkPlanWeight + x.InProcessReworkPlanWeight + x.InMainPlanWeight) * 0.03m
                    ? 5 : 6)
                : x.InputWeight > 0 && (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                        + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight) <= 0
                    ? 4
                    : (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                        + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight) <= 0
                        ? 0
                        : x.InputWeight > (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                            + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight) * 1.03m
                            ? 3
                            : x.InputWeight < (x.PiercingSubInWeight + x.SemiInWeight + x.FinishInWeight
                                + x.InventoryOutWeight + x.ReworkPlanInputWeight + x.InProcessReworkInputWeight + x.InMainInputWeight) * 0.97m
                                ? (x.CutoffArrivalDate == null
                                    ? 0
                                    : x.CutoffArrivalDate.Value.Date < DateTime.Today
                                        ? 2
                                        : x.CutoffArrivalDate.Value.Date == DateTime.Today
                                            ? 1
                                            : 0)
                                : 0));
        var sql = q.ToQueryString();
        sql.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TotalMissingWeight筛选排序_SQL可翻译()
    {
        // 回归：G3TotalMissingWeightExpr（排序）与 ApplyComputedFilters 的 totalmissingweight（筛选）
        // 曾用 Math.Max —— EF Core 无法翻译 System.Math.Max，SQL 执行时 500（用户实测：排序/筛选"理论缺失总料重"即报错）。
        // 修复为 SQL 可翻译的内联三元后，组合查询必须能翻译（ToQueryString 编译表达式，不连库）。
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=.;Database=__TranslationCheck;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;
        using var ctx = new AppDbContext(options);

        var filters = new List<FilterDescriptor>
        {
            new() { Field = "TotalMissingWeight", Operator = "in", Values = new List<string> { "100" } }
        };

        // 实际代码 ApplyComputedFilters（private static）→ 反射调用，验证 totalmissingweight WHERE 可翻译
        var applyMethod = typeof(WorkOrderExecutionService)
            .GetMethod("ApplyComputedFilters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var filtered = (IQueryable<WorkOrderExecutionSummary>)applyMethod
            .Invoke(null, new object[] { ctx.Set<WorkOrderExecutionSummary>().AsNoTracking(), filters })!;

        // 实际代码 G3TotalMissingWeightExpr（private static）→ 反射读取，验证 TotalMissingWeight ORDER BY 可翻译
        var exprField = typeof(WorkOrderExecutionService)
            .GetField("G3TotalMissingWeightExpr", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var missingExpr = (System.Linq.Expressions.Expression<Func<WorkOrderExecutionSummary, decimal>>)exprField.GetValue(null)!;

        var ordered = filtered.OrderBy(missingExpr);
        var sql = ordered.ToQueryString();
        sql.Should().NotBeNullOrWhiteSpace();
        sql.Should().Contain("CASE");
    }

    [Fact]
    public void PlanInputConsistency_DTO七档判定矩阵()
    {
        // 七档模型：实际已投料量(InputWeight) vs 现可投料总重(TotalAvailableWeight=到货量口径 G4~G10 到货/出库/投料动作量之和，下单未到货的量不视为现可)
        // 阶段门控优先：主号关注=生产执行(3)/成品检验(4)/主号完成(1) → 已过投料期，仅按缺失量判定——
        //   理论缺失总料重>计划投料总重×3% → 错误-无需投料(5)（缺口率>3% 计划严重未落实需修正）；其余（含缺口≤3% 容差内）→ 略(6)
        // 错误-无料已投(4)：已投>0 且 现可=0（无任何执行动作却投料，计划外投料，最异常）
        // 疑问-到料超投(3)：超投（已投 > 现可×1.03）
        // 疑问-到料少投(2)：投料滞后且截止到料日<今天（料已到位需投未投，存在问题）
        // 待投(1)：投料滞后且截止到料日=今天（操作时间差，正常）
        // 一致(0)：已投≈现可（±3% 内）或双零；投料滞后且截止到料日空/晚于今天（料未到位，投料滞后正常）
        var today = DateTime.Today;
        // 阶段门控用例
        AssertConsistency(new WorkOrderExecutionSummaryDto { ScheduleStage = 1, PiercingPlanWeight = 100m }, 5); // 主号完成 + 计划>现可 → 错误-无需投料
        AssertConsistency(new WorkOrderExecutionSummaryDto { ScheduleStage = 3, PiercingPlanWeight = 100m, PiercingSubInWeight = 100m }, 6); // 生产执行 + 计划=现可 → 略
        AssertConsistency(new WorkOrderExecutionSummaryDto { ScheduleStage = 4, SemiPlanWeight = 50m, SemiInWeight = 100m }, 6); // 成品检验 + 计划<现可（缺失=0）→ 略
        // 阶段门控阈值（缺口率>计划×3% 才标错，容差内归略）：计划=100 → 阈值 3
        AssertConsistency(new WorkOrderExecutionSummaryDto { ScheduleStage = 3, PiercingPlanWeight = 100m, PiercingSubInWeight = 96m }, 5); // 缺口4 > 3（4%）→ 错误-无需投料
        AssertConsistency(new WorkOrderExecutionSummaryDto { ScheduleStage = 3, PiercingPlanWeight = 100m, PiercingSubInWeight = 97m }, 6); // 缺口3 = 3 边界（=阈值不>）→ 略
        AssertConsistency(new WorkOrderExecutionSummaryDto { ScheduleStage = 3, PiercingPlanWeight = 100m, PiercingSubInWeight = 98m }, 6); // 缺口2 < 3（2%）→ 略
        // 原料锁定(2)/主号暂停(0)：不走门控，走原五态
        AssertConsistency(new WorkOrderExecutionSummaryDto { ScheduleStage = 2, InputWeight = 50m }, 4); // 原料锁定 + 已投>0 现可=0 → 错误-无料已投
        AssertConsistency(new WorkOrderExecutionSummaryDto { ScheduleStage = 0, InputWeight = 120m, SemiInWeight = 100m }, 3); // 主号暂停 + 超投 → 疑问-到料超投
        // 原五态用例（默认 ScheduleStage=0 不触发门控）
        AssertConsistency(new WorkOrderExecutionSummaryDto { InputWeight = 50m }, 4); // 已投>0 且 现可=0 → 错误-无料已投
        AssertConsistency(new WorkOrderExecutionSummaryDto { InputWeight = 120m, SemiInWeight = 100m }, 3); // 超投 120>103 → 疑问-到料超投
        AssertConsistency(new WorkOrderExecutionSummaryDto { InputWeight = 50m, SemiInWeight = 100m, CutoffArrivalDate = today }, 1); // 滞后 50<97 且 截止到料日=今天 → 待投
        AssertConsistency(new WorkOrderExecutionSummaryDto { InputWeight = 50m, SemiInWeight = 100m, CutoffArrivalDate = today.AddDays(-1) }, 2); // 滞后且截止到料日<今天 → 疑问-到料少投
        AssertConsistency(new WorkOrderExecutionSummaryDto { InputWeight = 50m, SemiInWeight = 100m, CutoffArrivalDate = today.AddDays(1) }, 0); // 滞后且截止到料日>今天 → 一致
        AssertConsistency(new WorkOrderExecutionSummaryDto { InputWeight = 50m, SemiInWeight = 100m }, 0); // 滞后且截止到料日空 → 一致（料未到位）
        AssertConsistency(new WorkOrderExecutionSummaryDto { InputWeight = 0m, SemiInWeight = 100m }, 0); // 到货未投且截止到料日空 → 一致（料未到位）
        AssertConsistency(new WorkOrderExecutionSummaryDto { InputWeight = 100m, SemiInWeight = 100m }, 0); // 已投=现可
        AssertConsistency(new WorkOrderExecutionSummaryDto { InputWeight = 103m, SemiInWeight = 100m }, 0); // 边界内 ±3%
        AssertConsistency(new WorkOrderExecutionSummaryDto { InputWeight = 97m, SemiInWeight = 100m }, 0); // 边界内 ±3%
        AssertConsistency(new WorkOrderExecutionSummaryDto { InputWeight = 0m }, 0); // 双零
    }

    [Fact]
    public void TotalMissingWeight_缺口超过计划3pct才取值否则为0()
    {
        // 新口径：理论缺失总料重 = 计划投料总重 − 现可投料总重，仅当缺口 > 计划投料总重×3%（InputConsistencyTolerance）才取值，否则为 0（小缺口降噪）
        var original = MaterialPlanToleranceProvider.InputConsistencyTolerance;
        try
        {
            MaterialPlanToleranceProvider.Apply(0.03m);
            // 计划=100 → 阈值=3
            new WorkOrderExecutionSummaryDto { PiercingPlanWeight = 100m, PiercingSubInWeight = 95m }
                .TotalMissingWeight.Should().Be(5m); // 缺口5 > 3 → 取值
            new WorkOrderExecutionSummaryDto { PiercingPlanWeight = 100m, PiercingSubInWeight = 96m }
                .TotalMissingWeight.Should().Be(4m); // 缺口4 > 3 → 取值
            new WorkOrderExecutionSummaryDto { PiercingPlanWeight = 100m, PiercingSubInWeight = 97m }
                .TotalMissingWeight.Should().Be(0m); // 缺口3 = 阈值（不>）→ 0
            new WorkOrderExecutionSummaryDto { PiercingPlanWeight = 100m, PiercingSubInWeight = 98m }
                .TotalMissingWeight.Should().Be(0m); // 缺口2 < 3 → 0
            new WorkOrderExecutionSummaryDto { PiercingPlanWeight = 100m, PiercingSubInWeight = 100m }
                .TotalMissingWeight.Should().Be(0m); // 无缺口 → 0
            new WorkOrderExecutionSummaryDto { PiercingPlanWeight = 100m, PiercingSubInWeight = 120m }
                .TotalMissingWeight.Should().Be(0m); // 可投超计划（缺口为负）→ 0
        }
        finally
        {
            MaterialPlanToleranceProvider.Apply(original);
        }
    }

    private static void AssertConsistency(WorkOrderExecutionSummaryDto dto, int expected)
    {
        dto.PlanInputConsistency.Should().Be(expected);
    }

    [Fact]
    public void PlanInputConsistency_Provider容差调整_排序表达式与DTO档位一致()
    {
        // 第三期：±3% 容差可配置（ConfigParameter.MaterialPlanTolerance.InputConsistencyTolerance 键）。
        // 三处消费（排序表达式 BuildPlanInputConsistencyExpr / 筛选 ApplyComputedFilters / DTO 计算属性 PlanInputConsistency）
        // 读 MaterialPlanToleranceProvider 统一快照，改配置表保存即生效。
        // 此测试验证：Provider 容差变更后，排序表达式求值与 DTO 判定结果一致且按新容差。
        var original = MaterialPlanToleranceProvider.InputConsistencyTolerance;
        try
        {
            // 默认容差 0.03：InputWeight=105 vs SemiInWeight=100 → 105 > 103 → 档 3（疑问-到料超投）
            MaterialPlanToleranceProvider.Apply(0.03m);
            var dto1 = new WorkOrderExecutionSummaryDto { ScheduleStage = 0, InputWeight = 105m, SemiInWeight = 100m };
            dto1.PlanInputConsistency.Should().Be(3);
            BuildPlanInputConsistencyValue(new WorkOrderExecutionSummary { ScheduleStage = 0, InputWeight = 105m, SemiInWeight = 100m })
                .Should().Be(3);

            // 放宽容差到 0.10：105 < 110 → 档 0（一致）；表达式与 DTO 同步变化（同快照值）
            MaterialPlanToleranceProvider.Apply(0.10m);
            var dto2 = new WorkOrderExecutionSummaryDto { ScheduleStage = 0, InputWeight = 105m, SemiInWeight = 100m };
            dto2.PlanInputConsistency.Should().Be(0);
            BuildPlanInputConsistencyValue(new WorkOrderExecutionSummary { ScheduleStage = 0, InputWeight = 105m, SemiInWeight = 100m })
                .Should().Be(0);
        }
        finally
        {
            MaterialPlanToleranceProvider.Apply(original);
        }
    }

    private static int BuildPlanInputConsistencyValue(WorkOrderExecutionSummary entity)
    {
        var method = typeof(WorkOrderExecutionService)
            .GetMethod("BuildPlanInputConsistencyExpr", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var expr = (System.Linq.Expressions.Expression<Func<WorkOrderExecutionSummary, int>>)method.Invoke(null, null)!;
        return expr.Compile()(entity);
    }

    [Fact]
    public async Task GetPagedAsync_主号计划执行状态_排序与筛选()
    {
        using var ctx = CreateDbContext();
        SeedComputedSummary(ctx, "WO001", e => { e.MainNoPlanExecutionStatus = 3; });
        SeedComputedSummary(ctx, "WO002", e => { e.MainNoPlanExecutionStatus = 0; });
        SeedComputedSummary(ctx, "WO003", e => { e.MainNoPlanExecutionStatus = 2; });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);

        // 筛选 in (3) → 仅 WO001
        var filtered = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SortBy = "WorkOrderNo",
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "MainNoPlanExecutionStatus", Operator = "in", Values = new List<string> { "3" } }
            }
        });
        filtered.TotalCount.Should().Be(1);
        filtered.Items.Single().WorkOrderNo.Should().Be("WO001");

        // 排序升序 → 0,2,3（WO002, WO003, WO001）
        var asc = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, SortBy = "MainNoPlanExecutionStatus", IsDescending = false });
        asc.Items.Select(i => i.WorkOrderNo).Should().Equal("WO002", "WO003", "WO001");

        // 排序降序 → 3,2,0（WO001, WO003, WO002）
        var desc = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, SortBy = "MainNoPlanExecutionStatus", IsDescending = true });
        desc.Items.Select(i => i.WorkOrderNo).Should().Equal("WO001", "WO003", "WO002");
    }

    [Fact]
    public async Task GetPagedAsync_G5至G11列_排序正常()
    {
        using var ctx = CreateDbContext();
        SeedComputedSummary(ctx, "WO001", e => { e.PiercingPlanWeight = 100m; e.PiercingSubStatus = 0; });
        SeedComputedSummary(ctx, "WO002", e => { e.PiercingPlanWeight = 300m; e.PiercingSubStatus = 4; });
        SeedComputedSummary(ctx, "WO003", e => { e.PiercingPlanWeight = 200m; e.PiercingSubStatus = 1; });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);

        // G5 数值列排序（穿孔计划量）
        var asc = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, SortBy = "PiercingPlanWeight", IsDescending = false });
        asc.Items.Select(i => i.WorkOrderNo).Should().Equal("WO001", "WO003", "WO002");
        var desc = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, SortBy = "PiercingPlanWeight", IsDescending = true });
        desc.Items.Select(i => i.WorkOrderNo).Should().Equal("WO002", "WO003", "WO001");

        // G5 状态列排序（穿孔委外状态）
        var ascStatus = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, SortBy = "PiercingSubStatus", IsDescending = false });
        ascStatus.Items.Select(i => i.WorkOrderNo).Should().Equal("WO001", "WO003", "WO002");
        var descStatus = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, SortBy = "PiercingSubStatus", IsDescending = true });
        descStatus.Items.Select(i => i.WorkOrderNo).Should().Equal("WO002", "WO003", "WO001");
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
    public async Task RefreshAllAsync_跳过未生成工单()
    {
        using var ctx = CreateDbContext();
        // Status = NotGenerated → should be skipped
        ctx.WorkOrders.Add(CreateWorkOrder("WO001", "SO001", WorkOrderStatus.NotGenerated));
        // Status = NotGenerated → should be skipped
        ctx.WorkOrders.Add(CreateWorkOrder("WO002", "SO002", WorkOrderStatus.NotGenerated));
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
            Status = SalesOrderStatus.Confirmed,
            RowVersion = new byte[8],
            CustomerName = "测试客户",
            Salesman = "测试业务员"
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
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
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
            InputQuantity = 50,
            InputWeight = 1250m,
            CurrentValidQty = 50,
            CurrentValidWeight = 1250,
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
        // 定尺：批次生产类型为空（非库存/外购）→ 合格率 ×0.98
        // 理论成品支数 = 50 * 2 * 0.98 = 98，成品比 = 98/100 * 100 = 98%
        s.TotalBatchCount.Should().Be(1);
        s.InputQuantity.Should().Be(50);
        s.InputWeight.Should().Be(1250m);
        s.TheoreticalOutputQty.Should().Be(98); // 50 * 2 * 0.98
        // 有效工序段数 = 1（HasAnySection 按 ProcessGroup 计数），折扣 = 1 - 1*0.025 = 0.975
        // 理论成品重量 = 1250 * 0.975 = 1218.75
        s.TheoreticalOutputWeight.Should().Be(1218.75m);
        s.InputOutputRatio.Should().Be(98); // 98/100*100
        s.InputStatus.Should().Be(1); // 部分
        // G12 合格流转与 G11 同逻辑，基准为有效投料：50 * 2 * 0.98 = 98
        s.ValidOutputQty.Should().Be(98);
        s.ValidOutputWeight.Should().Be(1218.75m); // 1250 * 0.975
    }

    [Fact]
    public async Task RefreshAllAsync_定尺库存批次按100合格率()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);

        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed,
            salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed,
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
            InputQuantity = 50,
            InputWeight = 1250m,
            CurrentValidQty = 50,
            CurrentValidWeight = 1250,
            ProductionRatio = 2,
            ProductionType = "Inventory",
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
        // 库存批次定尺 ×100% 合格率：理论成品支 = 50 * 2 * 1.0 = 100，比值 = 100/100*100 = 100%
        s.TheoreticalOutputQty.Should().Be(100);
        s.InputOutputRatio.Should().Be(100);
        s.InputStatus.Should().Be(2); // 满足
        // G12 合格流转同逻辑：库存 ×100%，有效投料 50 * 2 * 1.0 = 100
        s.ValidOutputQty.Should().Be(100);
    }

    [Fact]
    public async Task RefreshAllAsync_定尺其它类型理论成品四舍五入取整()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);

        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed,
            salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed,
            totalQty: 100, totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // 非库存/外购批次：51 * 2 * 0.98 = 99.96 → 四舍五入取整 100
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
            InputQuantity = 51,
            InputWeight = 1250m,
            CurrentValidQty = 51,
            CurrentValidWeight = 1250,
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
        // 51 * 2 * 0.98 = 99.96 → 四舍五入 100；比值 = 100%
        s.TheoreticalOutputQty.Should().Be(100);
        s.InputOutputRatio.Should().Be(100);
        s.InputStatus.Should().Be(2); // 满足
        // G12 合格流转同逻辑：有效投料 51 * 2 * 0.98 = 99.96 → 100
        s.ValidOutputQty.Should().Be(100);
    }

    [Fact]
    public async Task RefreshAllAsync_G12合格流转基准为有效投料与原始投料不同()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);

        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed,
            salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed,
            totalQty: 100, totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // 领料 51 支、有效投料 50 支（在产有损耗），非库存/外购 → 合格率 ×0.98
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
            InputQuantity = 51,
            InputWeight = 1250m,
            CurrentValidQty = 50,
            CurrentValidWeight = 1200,
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
        // G11 原始投料：领料 51 * 2 * 0.98 = 99.96 → 100；重量 1250 * 0.975 = 1218.75
        s.TheoreticalOutputQty.Should().Be(100);
        s.TheoreticalOutputWeight.Should().Be(1218.75m);
        // G12 合格流转：有效投料 50 * 2 * 0.98 = 98；重量 1200 * 0.975 = 1170
        s.ValidOutputQty.Should().Be(98);
        s.ValidOutputWeight.Should().Be(1170m);
    }

    [Fact]
    public async Task RefreshAllAsync_G12含成检完成批次与G11范围一致()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);

        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed,
            salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed,
            totalQty: 300, totalWeight: 7500m);
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // 在产批次：领料 51 / 有效 50
        var b1 = new ProductionBatch
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
            InputQuantity = 51,
            InputWeight = 1250m,
            CurrentValidQty = 50,
            CurrentValidWeight = 1200,
            ProductionRatio = 2,
            RowVersion = new byte[8],
            ProcessGroups = new List<ProcessGroup>
            {
                new() { ProcessName = "60冷轧", SequenceNumber = 1, ColdRollDraw = 1, Solution = 2 }
            }
        };
        // 已完成批次：领料 50 / 有效 48
        var b2 = new ProductionBatch
        {
            BatchNo = "B002",
            Status = BatchStatus.Completed,
            WorkOrderNo = "WO001",
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "业务员A",
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
            InputQuantity = 50,
            InputWeight = 1200m,
            CurrentValidQty = 48,
            CurrentValidWeight = 1100,
            ProductionRatio = 2,
            RowVersion = new byte[8],
            ProcessGroups = new List<ProcessGroup>
            {
                new() { ProcessName = "60冷轧", SequenceNumber = 1, ColdRollDraw = 1, Solution = 2 }
            }
        };
        ctx.ProductionBatches.AddRange(b1, b2);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        // G12 批次范围与 G11 一致：含完成批次
        s.TotalBatchCount.Should().Be(2);
        s.ValidBatchCount.Should().Be(2);
        s.ValidInputQuantity.Should().Be(98); // 50 + 48
        // G11 = (51+50) * 2 * 0.98 = 197.96 → 198
        s.TheoreticalOutputQty.Should().Be(198);
        // G12 = (50+48) * 2 * 0.98 = 192.08 → 192
        s.ValidOutputQty.Should().Be(192);
        // G12 重量 = 1200*0.975 + 1100*0.975 = 1170 + 1072.5 = 2242.5
        s.ValidOutputWeight.Should().Be(2242.5m);
    }

    [Fact]
    public async Task RefreshAllAsync_非定尺投料比按重量()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
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
            SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163",
            DeliveryState = "SolutionAnnealedAndPickled",
            LengthStatus = "Unlimited",
            ManufacturingItem = "OrderFinished",
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
            CurrentValidWeight = 1250,
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
    public async Task RefreshAllAsync_G12含完成批次作废批次不计量()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);

        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed,
            salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed,
            totalQty: 100, totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // 有效批次（在产）
        var validBatch = new ProductionBatch
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
            InputQuantity = 50,
            InputWeight = 1250m,
            CurrentValidQty = 50,
            CurrentValidWeight = 1250,
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
            BatchNo = "B002",
            Status = BatchStatus.Completed,
            WorkOrderNo = "WO001",
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "业务员A",
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
            InputQuantity = 30,
            InputWeight = 750m,
            CurrentValidQty = 0,
            CurrentValidWeight = 0,
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

        // Group 12: 批次范围与 G11 一致（非返整+全部，含完成），作废批次有效投料为 0 不贡献量值
        s.ValidBatchCount.Should().Be(2);
        s.ValidInputQuantity.Should().Be(50); // 有效批次 50 + 作废批次 0
        s.ValidInputWeight.Should().Be(1250m); // 有效批次 1250 + 作废批次 0
    }

    [Fact]
    public async Task RefreshAllAsync_用料计划日期取最大值()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
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
            RawMaterialType = MaterialType.RoughTube,
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

        // Seed WorkOrderListSummary 读模型（RefreshAllAsync 从此读取 G2 字段）
        ctx.Set<WorkOrderListSummary>().Add(new WorkOrderListSummary
        {
            WorkOrderId = wo.Id,
            WorkOrderNo = wo.WorkOrderNo,
            SalesOrderNo = wo.SalesOrderNo,
            ProductionMainNo = wo.ProductionMainNo,
            SignDate = wo.SignDate,
            Salesman = wo.Salesman ?? "",
            DeliveryDate = wo.DeliveryDate,
            SettlementMethod = wo.SettlementMethod.ToString(),
            MaterialName = wo.PipeManufacturingType.ToString(),
            DeliveryState = wo.DeliveryState.ToString(),
            PlantGrade = wo.PlantGrade,
            Specification = wo.Specification,
            LengthStatus = wo.LengthStatus.ToString(),
            TotalQuantity = wo.TotalQuantity,
            TotalMeters = wo.TotalMeters,
            TotalWeight = wo.TotalWeight,
            TotalItemCount = wo.TotalItemCount,
            TechnicalRequirements = wo.TechnicalRequirements.ToString(),
            Status = (int)wo.Status,
            CreatedTime = wo.CreatedTime,
            LatestPlanDate = new DateTime(2026, 7, 20),
            MaterialPlanRate = 80m,
            MaterialPlanStatus = 1,
            RowVersion = new byte[8]
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        s.MaterialPlanStatus.Should().Be(1); // Partial
    }

    [Fact]
    public async Task RefreshAllAsync_G6成品采购_订成非交付态采购单计入FinishOrderWeight()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO-SDS", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);

        var wo = CreateWorkOrder("WO-SDS", "SO-SDS", WorkOrderStatus.Confirmed,
            salesman: "业务员A", mainNo: "D01");
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // 订成-非交付态成品采购计划
        ctx.Set<PurchaseFinishedPlan>().Add(new PurchaseFinishedPlan
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            PlantGrade = "304",
            Specification = "219*8",
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.5m,
            WallThicknessPositive = 0.5m,
            LengthStatus = LengthStatus.Fixed,
            ProductType = FinishedProductType.SpecialDeliveryStatus,
            RequiredWeight = 2000
        });

        // 订成-非交付态采购单（应计入 G6 成品采购量，不被过滤）
        ctx.PurchaseOrders.Add(new PurchaseOrder
        {
            OrderNo = $"CG-SDS-{Guid.NewGuid():N}"[..15],
            SupplierId = 1,
            SupplierName = "测试供应商",
            OrderDate = DateTime.Today,
            Status = PurchaseOrderStatus.Open,
            MaterialCategory = "SpecialDeliveryStatus",
            PlantGrade = "304",
            Specification = "219*8",
            Quantity = 10,
            Weight = 1500m,
            RequiredDate = DateTime.Today.AddDays(30),
            SourceWorkOrderNo = wo.WorkOrderNo
        });
        await ctx.SaveChangesAsync();

        // Seed WorkOrderListSummary 读模型（RefreshAllAsync 从此读取 G2 字段）
        ctx.Set<WorkOrderListSummary>().Add(new WorkOrderListSummary
        {
            WorkOrderId = wo.Id,
            WorkOrderNo = wo.WorkOrderNo,
            SalesOrderNo = wo.SalesOrderNo,
            ProductionMainNo = wo.ProductionMainNo,
            SignDate = wo.SignDate,
            Salesman = wo.Salesman ?? "",
            DeliveryDate = wo.DeliveryDate,
            SettlementMethod = wo.SettlementMethod.ToString(),
            MaterialName = wo.PipeManufacturingType.ToString(),
            DeliveryState = wo.DeliveryState.ToString(),
            PlantGrade = wo.PlantGrade,
            Specification = wo.Specification,
            LengthStatus = wo.LengthStatus.ToString(),
            TotalQuantity = wo.TotalQuantity,
            TotalMeters = wo.TotalMeters,
            TotalWeight = wo.TotalWeight,
            TotalItemCount = wo.TotalItemCount,
            TechnicalRequirements = wo.TechnicalRequirements.ToString(),
            Status = (int)wo.Status,
            CreatedTime = wo.CreatedTime,
            LatestPlanDate = DateTime.Today,
            MaterialPlanRate = 80m,
            MaterialPlanStatus = 1,
            RowVersion = new byte[8]
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        s.FinishPlanWeight.Should().Be(2000m);
        s.FinishOrderWeight.Should().Be(1500m);
    }

    [Fact]
    public async Task RefreshAllAsync_多工单分别创建汇总()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so1 = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        var so2 = new SalesOrder { OrderNumber = "SO002", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
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
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
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
            SettlementMethod = "Theoretical",
            SignDate = DateTime.MinValue,
            DeliveryDate = DateTime.MinValue,
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            MaterialName = "",
            DeliveryState = "SolutionAnnealedAndPickled",
            PlantGrade = "",
            Specification = "",
            LengthStatus = "Fixed",
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
        s.Salesman.Should().Be("测试业务员"); // 从 SalesOrder 快照字段读取
    }

    [Fact]
    public async Task RefreshAllAsync_删除多余汇总记录()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
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
            SettlementMethod = "Theoretical",
            SignDate = DateTime.MinValue,
            DeliveryDate = DateTime.MinValue,
            SalesOrderNo = "",
            ProductionMainNo = "",
            MaterialName = "",
            DeliveryState = "",
            PlantGrade = "",
            Specification = "",
            LengthStatus = "Fixed",
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
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
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
            WorkOrderId = wo1.Id,
            PlanDate = DateTime.Today,
            RequiredPieces = 40,
            RequiredWeight = 1000m,
            InputMultiple = 2,
            PlantGrade = "304",
            RawMaterialSpec = "219*8",
            RequiredDate = DateTime.Today
        });
        // WO002 Fixed TotalQuantity=200 → 90%: RequiredPieces=60 × InputMultiple=3 = 180
        ctx.PurchaseSemiPlans.Add(new PurchaseSemiPlan
        {
            WorkOrderId = wo2.Id,
            PlanDate = DateTime.Today,
            RequiredPieces = 60,
            RequiredWeight = 1500m,
            InputMultiple = 3,
            PlantGrade = "304",
            RawMaterialSpec = "219*8",
            RequiredDate = DateTime.Today
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
                SettlementMethod = "Theoretical",
                StandardCode = "GB/T 8163",
                DeliveryState = "SolutionAnnealedAndPickled",
                LengthStatus = "Fixed",
                ManufacturingItem = "OrderFinished",
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
                CurrentValidWeight = 1250,
                ProductionRatio = 2,
                RowVersion = new byte[8],
                ProcessGroups = new List<ProcessGroup>
                {
                    new() { ProcessName = "60冷轧", SequenceNumber = 1, ColdRollDraw = 1, Solution = 2 }
                }
            });
        }
        await ctx.SaveChangesAsync();

        // Seed WorkOrderListSummary 读模型（RefreshAllAsync 从此读取 G2 字段）
        foreach (var (w, rate) in new[] { (wo1, 80m), (wo2, 90m) })
        {
            ctx.Set<WorkOrderListSummary>().Add(new WorkOrderListSummary
            {
                WorkOrderId = w.Id,
                WorkOrderNo = w.WorkOrderNo,
                SalesOrderNo = w.SalesOrderNo,
                ProductionMainNo = w.ProductionMainNo,
                ProductionSubNo = w.ProductionSubNo,
                SignDate = w.SignDate,
                Salesman = w.Salesman ?? "",
                DeliveryDate = w.DeliveryDate,
                SettlementMethod = w.SettlementMethod.ToString(),
                MaterialName = w.PipeManufacturingType.ToString(),
                DeliveryState = w.DeliveryState.ToString(),
                PlantGrade = w.PlantGrade,
                Specification = w.Specification,
                LengthStatus = w.LengthStatus.ToString(),
                TotalQuantity = w.TotalQuantity,
                TotalMeters = w.TotalMeters,
                TotalWeight = w.TotalWeight,
                TotalItemCount = w.TotalItemCount,
                TechnicalRequirements = w.TechnicalRequirements.ToString(),
                Status = (int)w.Status,
                CreatedTime = w.CreatedTime,
                LatestPlanDate = DateTime.Today,
                MaterialPlanRate = rate,
                MaterialPlanStatus = rate >= 100 ? 3 : 1,
                // MainNo 聚合值由 WorkOrderListSummaryRefreshService 写入，测试中直接预置
                MainNoMaterialPlanRate = 86.67m,
                MainNoMaterialPlanStatus = 1,
                RowVersion = new byte[8]
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
            // 理论成品：两个工单各 50*2*0.98=98（批次非库存/外购 → ×98%），合计 196
            // 合计需求：100+200=300
            // MainNo 比（定尺按支数）：196/300*100 = 65.33
            s.MainNoInputOutputRatio.Should().Be(65.33m);
            s.MainNoInputStatus.Should().Be(1); // 部分

            // MainNo 计划执行状态（4 档求和判定）：两工单合计计划量>0 但现可量(动作量)=0（无采购下单等执行动作）→ 未执行(1)
            s.MainNoPlanExecutionStatus.Should().Be(1);
        }
    }

    [Fact]
    public async Task RefreshAllAsync_过程组有效工序折扣计算()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
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
            SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163",
            DeliveryState = "SolutionAnnealedAndPickled",
            LengthStatus = "Unlimited",
            ManufacturingItem = "OrderFinished",
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
            CurrentValidWeight = 2500,
            RowVersion = new byte[8],
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
        {
            if (kvp.Key == "ProductionFlowProperty")
                kvp.Value.Should().BeEquivalentTo(new[] { ProductionFlowKeys.Paused, ProductionFlowKeys.Normal, ProductionFlowKeys.Waiting, ProductionFlowKeys.Doubt, ProductionFlowKeys.Skip });
            else if (kvp.Key == "ProductionAttentionProcess")
                kvp.Value.Should().BeEmpty();
            else
                kvp.Value.Should().BeEmpty($"字段 {kvp.Key} 应返回空列表");
        }
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
        string lengthStatus = "Fixed",
        string materialPlanProportion = "")
    {
        ctx.Set<WorkOrderExecutionSummary>().Add(new WorkOrderExecutionSummary
        {
            WorkOrderId = Math.Abs(workOrderNo.GetHashCode()),
            WorkOrderNo = workOrderNo,
            Salesman = salesman,
            CustomerName = customerName,
            SettlementMethod = "Theoretical",
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
            InputOutputRatio = ratio,
            MaterialPlanProportion = string.IsNullOrEmpty(materialPlanProportion) ? null : materialPlanProportion
        });
    }

    private MES.Data.Entities.WorkOrder.WorkOrder CreateWorkOrder(
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
        return new MES.Data.Entities.WorkOrder.WorkOrder
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
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
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

    // ==================== G14 返整执行（4 新字段） ====================

    [Fact]
    public async Task RefreshAllAsync_G14返整执行_全工单返整量与理论可产支与一致()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // 正常批次（非返整）：其过程检返整量也计入（全工单范围）
        var normalBatch = new ProductionBatch
        {
            BatchNo = "B001", Status = BatchStatus.InProgress, WorkOrderNo = "WO001", SalesOrderNo = "SO001",
            ProductionMainNo = "D01", OrderItemIds = "1", SignDate = DateTime.Today, Salesman = "业务员A",
            DeliveryDate = DateTime.Today.AddMonths(1), MaterialName = "无缝管", SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163", DeliveryState = "SolutionAnnealedAndPickled", LengthStatus = "Fixed",
            ManufacturingItem = "OrderFinished", ProductionType = "InProcess", PlantGrade = "304",
            Specification = "219*8", TotalQuantity = 100, TotalMeters = 600, TotalWeight = 2500m,
            TotalItemCount = 1, TechnicalRequirements = "NORMAL", CurrentValidQty = 50, CurrentValidWeight = 1250,
            ProductionRatio = 2, RowVersion = new byte[8]
        };
        // 返整批次：理论单支重=25，投料重量=200；返整量合计=200 → 偏差 0 → 一致
        var reworkBatch = new ProductionBatch
        {
            BatchNo = "B002", Status = BatchStatus.InProgress, WorkOrderNo = "WO001", SalesOrderNo = "SO001",
            ProductionMainNo = "D01", OrderItemIds = "1", SignDate = DateTime.Today, Salesman = "业务员A",
            DeliveryDate = DateTime.Today.AddMonths(1), MaterialName = "无缝管", SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163", DeliveryState = "SolutionAnnealedAndPickled", LengthStatus = "Fixed",
            ManufacturingItem = "OrderFinished", ProductionType = "Rework", PlantGrade = "304",
            Specification = "219*8", TotalQuantity = 100, TotalMeters = 600, TotalWeight = 200m,
            TotalItemCount = 1, TechnicalRequirements = "NORMAL", CurrentValidQty = 10, CurrentValidWeight = 200,
            TheoreticalUnitWeight = 25m, RowVersion = new byte[8]
        };
        ctx.ProductionBatches.AddRange(normalBatch, reworkBatch);
        await ctx.SaveChangesAsync();

        // 过程检验：正常批次 50 + 返整批次 50 → 过程检返整量 = 100
        ctx.ProcessInspections.Add(new ProcessInspection
        {
            ProductionBatchId = normalBatch.Id, ProcessGroupId = 1, ProcessName = "在制修检",
            SectionName = SectionKeys.Inspection, SequenceNumber = 1, InspectionDate = DateTime.Today, TheoreticalReworkWeight = 50
        });
        ctx.ProcessInspections.Add(new ProcessInspection
        {
            ProductionBatchId = reworkBatch.Id, ProcessGroupId = 1, ProcessName = "在制修检",
            SectionName = SectionKeys.Inspection, SequenceNumber = 1, InspectionDate = DateTime.Today, TheoreticalReworkWeight = 50
        });
        // 成品检验：返整批次 100 → 成品检返整量 = 100
        ctx.FinalInspections.Add(new FinalInspection
        {
            ProductionBatchId = reworkBatch.Id, BatchNo = "B002", InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today, InspectionType = nameof(InspectionType.FormalInspection),
            Quantity = 10, QualifiedQuantity = 10, DefectReworkWeight = 100
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().SingleAsync();
        s.ProcessInspectionReworkWeight.Should().Be(100); // 全工单（含正常批次）
        s.FinalInspectionReworkWeight.Should().Be(100);
        s.ReworkInputWeight.Should().Be(200); // 仅返整批次投料重量
        // 可产成支按返整记录的原批次单支重折算：
        // normalBatch 过程检 50（原批次无单支重）不贡献；reworkBatch 过程检 50 + 成检 100 = 150 / 单支重25 = 6
        s.ReworkTheoreticalProduceQty.Should().Be(6);
        // 理论返整可产成重 = 过程检返整量100×0.92 + 成检返整量100×0.96 = 188
        s.ReworkTheoreticalProduceWeight.Should().Be(188m);
        // 待返整成支 = 可产成支6 − 返整理论成品支0 = 6
        s.PendingReworkOutputQty.Should().Be(6m);
        // 待返整成重 = 188 − 返整理论成品重200 = -12 → 负值归0
        s.PendingReworkOutputWeight.Should().Be(0m);
        // 附返整主号状态：有效流转(98) + 待返整(6) = 104% ≥ 100 → 满足(2)；有效主号 98% → 部分(1) → 必返整"是"
        s.ReworkMainNoStatus.Should().Be(2);
        s.ReworkInputConsistency.Should().Be("是");
    }

    [Fact]
    public async Task RefreshAllAsync_G14返整执行_投料但无返整量判疑问()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01");
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // 返整批次：投料 100，但无任何过程检/成品检返整记录
        var reworkBatch = new ProductionBatch
        {
            BatchNo = "B002", Status = BatchStatus.InProgress, WorkOrderNo = "WO001", SalesOrderNo = "SO001",
            ProductionMainNo = "D01", OrderItemIds = "1", SignDate = DateTime.Today, Salesman = "业务员A",
            DeliveryDate = DateTime.Today.AddMonths(1), MaterialName = "无缝管", SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163", DeliveryState = "SolutionAnnealedAndPickled", LengthStatus = "Fixed",
            ManufacturingItem = "OrderFinished", ProductionType = "Rework", PlantGrade = "304",
            Specification = "219*8", TotalQuantity = 100, TotalMeters = 600, TotalWeight = 100m,
            TotalItemCount = 1, TechnicalRequirements = "NORMAL", CurrentValidQty = 5, CurrentValidWeight = 100,
            ProductUnitWeight = 25m, RowVersion = new byte[8]
        };
        ctx.ProductionBatches.Add(reworkBatch);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().SingleAsync();
        s.ProcessInspectionReworkWeight.Should().BeNull();
        s.FinalInspectionReworkWeight.Should().BeNull();
        s.ReworkTheoreticalProduceQty.Should().BeNull();
        s.ReworkInputWeight.Should().Be(100);
        // 无返整量 → 理论返整可产成重/待返整均空；附返整=有效流转=0 → 未投料 → 不必返整"否"
        s.ReworkTheoreticalProduceWeight.Should().BeNull();
        s.PendingReworkOutputQty.Should().BeNull();
        s.PendingReworkOutputWeight.Should().BeNull();
        s.ReworkMainNoStatus.Should().Be(0);
        s.ReworkInputConsistency.Should().Be("否");
    }

    [Fact]
    public async Task RefreshAllAsync_G14返整执行_全返整批次无单支重_兜底领料重量除领料支数除制成倍数()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // 返整批次：无产品单支量/理论单支重（全返整），兜底按 领料重量÷领料支数÷制成倍数 = 500/(10*2)=25
        var reworkBatch = new ProductionBatch
        {
            BatchNo = "B002", Status = BatchStatus.InProgress, WorkOrderNo = "WO001", SalesOrderNo = "SO001",
            ProductionMainNo = "D01", OrderItemIds = "1", SignDate = DateTime.Today, Salesman = "业务员A",
            DeliveryDate = DateTime.Today.AddMonths(1), MaterialName = "无缝管", SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163", DeliveryState = "SolutionAnnealedAndPickled", LengthStatus = "Fixed",
            ManufacturingItem = "OrderFinished", ProductionType = "Rework", PlantGrade = "304",
            Specification = "219*8", TotalQuantity = 100, TotalMeters = 600, TotalWeight = 500m,
            TotalItemCount = 1, TechnicalRequirements = "NORMAL", CurrentValidQty = 10, CurrentValidWeight = 200,
            InputQuantity = 10, InputWeight = 500m, ProductionRatio = 2, RowVersion = new byte[8]
        };
        ctx.ProductionBatches.Add(reworkBatch);
        await ctx.SaveChangesAsync();

        // 成品检验：返整批次 200 → 成品检返整量 = 200
        ctx.FinalInspections.Add(new FinalInspection
        {
            ProductionBatchId = reworkBatch.Id, BatchNo = "B002", InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today, InspectionType = nameof(InspectionType.FormalInspection),
            Quantity = 10, QualifiedQuantity = 10, DefectReworkWeight = 200
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().SingleAsync();
        s.FinalInspectionReworkWeight.Should().Be(200);
        s.ReworkInputWeight.Should().Be(200);
        // 兜底单支重 = 500/(10*2)=25 → 200/25 = 8
        s.ReworkTheoreticalProduceQty.Should().Be(8);
        // 理论返整可产成重 = 成检返整量200×0.96 = 192
        s.ReworkTheoreticalProduceWeight.Should().Be(192m);
        // 待返整成支 = 8 − 19.6 = -11.6 → 负值归0
        s.PendingReworkOutputQty.Should().Be(0m);
        // 待返整成重 = 192 − 返整理论成品重200 = -8 → 负值归0
        s.PendingReworkOutputWeight.Should().Be(0m);
        // 附返整 = (0 + 可产成支8)/100 = 8% → 部分(1) → 不必返整"否"
        s.ReworkMainNoStatus.Should().Be(1);
        s.ReworkInputConsistency.Should().Be("否");
        // 返整理论成品支已按合格率折算（与合格流转对齐）：10×2×98% = 19.6
        s.ReworkTheoreticalOutputQty.Should().Be(19.6m);
    }

    [Fact]
    public async Task RefreshAllAsync_G14返整执行_返整量按原批次单支重折算_工单无返整批次()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // 普通批次（InProcess，非返整）：理论单支重=22.4，产生成品检返整量 224
        var sourceBatch = new ProductionBatch
        {
            BatchNo = "2601-574", Status = BatchStatus.InProgress, WorkOrderNo = "WO001", SalesOrderNo = "SO001",
            ProductionMainNo = "D01", OrderItemIds = "1", SignDate = DateTime.Today, Salesman = "业务员A",
            DeliveryDate = DateTime.Today.AddMonths(1), MaterialName = "无缝管", SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163", DeliveryState = "SolutionAnnealedAndPickled", LengthStatus = "Fixed",
            ManufacturingItem = "OrderFinished", ProductionType = "InProcess", PlantGrade = "304",
            Specification = "219*8", TotalQuantity = 100, TotalMeters = 600, TotalWeight = 2500m,
            TotalItemCount = 1, TechnicalRequirements = "NORMAL", CurrentValidQty = 50, CurrentValidWeight = 1081,
            TheoreticalUnitWeight = 22.4m, RowVersion = new byte[8]
        };
        ctx.ProductionBatches.Add(sourceBatch);
        await ctx.SaveChangesAsync();

        // 成品检验：原批次 224 → 成品检返整量 = 224
        ctx.FinalInspections.Add(new FinalInspection
        {
            ProductionBatchId = sourceBatch.Id, BatchNo = "2601-574", InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today, InspectionType = nameof(InspectionType.FormalInspection),
            Quantity = 10, QualifiedQuantity = 10, DefectReworkWeight = 224
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().SingleAsync();
        s.FinalInspectionReworkWeight.Should().Be(224);
        s.ReworkInputWeight.Should().Be(0); // 工单无返整批次 → 投料 0
        // 可产成支 = 返整量 / 原批次单支重 = 224 / 22.4 = 10
        s.ReworkTheoreticalProduceQty.Should().Be(10);
        // 理论返整可产成重 = 成检返整量224×0.96 = 215.04
        s.ReworkTheoreticalProduceWeight.Should().Be(215.04m);
        // 待返整成支 = 10 − 0 = 10；待返整成重 = 215.04 − 0 = 215.04（无返整批次 → 返整理论成品为0）
        s.PendingReworkOutputQty.Should().Be(10m);
        s.PendingReworkOutputWeight.Should().Be(215.04m);
        // 附返整 = (0 + 10)/100 = 10% → 部分(1) → 不必返整"否"
        s.ReworkMainNoStatus.Should().Be(1);
        s.ReworkInputConsistency.Should().Be("否");
    }

    [Fact]
    public async Task RefreshAllAsync_G14返整执行_无返整批次判略()
    {
        using var ctx = CreateDbContext();
        var cust = await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01");
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().SingleAsync();
        s.ReworkBatchCount.Should().Be(0);
        s.ReworkInputWeight.Should().Be(0);
        // 无返整 → 可产成重/待返整为空；附返整=有效=0 → 未投料(0) → 不必返整"否"
        s.ReworkTheoreticalProduceWeight.Should().BeNull();
        s.PendingReworkOutputQty.Should().BeNull();
        s.PendingReworkOutputWeight.Should().BeNull();
        s.ReworkMainNoStatus.Should().Be(0);
        s.ReworkInputConsistency.Should().Be("否");
    }

    [Fact]
    public async Task RefreshAllAsync_附返整主号状态_主号聚合满足且有效未满足判必返整()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        // 主号 D01 下两个工单：WO001 需求50（正常产出 49）、WO002 需求50（返整产出 0.98、可产成支 80）
        var wo1 = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 50, totalWeight: 1250m);
        var wo2 = CreateWorkOrder("WO002", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 50, totalWeight: 1250m);
        ctx.WorkOrders.AddRange(wo1, wo2);
        await ctx.SaveChangesAsync();

        var normalBatch = new ProductionBatch
        {
            BatchNo = "B001", Status = BatchStatus.InProgress, WorkOrderNo = "WO001", SalesOrderNo = "SO001",
            ProductionMainNo = "D01", OrderItemIds = "1", SignDate = DateTime.Today, Salesman = "业务员A",
            DeliveryDate = DateTime.Today.AddMonths(1), MaterialName = "无缝管", SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163", DeliveryState = "SolutionAnnealedAndPickled", LengthStatus = "Fixed",
            ManufacturingItem = "OrderFinished", ProductionType = "InProcess", PlantGrade = "304",
            Specification = "219*8", TotalQuantity = 50, TotalMeters = 300, TotalWeight = 1250m,
            TotalItemCount = 1, TechnicalRequirements = "NORMAL", CurrentValidQty = 50, CurrentValidWeight = 1250,
            ProductionRatio = 1, RowVersion = new byte[8]
        };
        // 返整批次：CurrentValidQty=1 → 返整理论成品支 = 1×1×98% = 0.98；单支重25
        var reworkBatch = new ProductionBatch
        {
            BatchNo = "B002", Status = BatchStatus.InProgress, WorkOrderNo = "WO002", SalesOrderNo = "SO001",
            ProductionMainNo = "D01", OrderItemIds = "1", SignDate = DateTime.Today, Salesman = "业务员A",
            DeliveryDate = DateTime.Today.AddMonths(1), MaterialName = "无缝管", SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163", DeliveryState = "SolutionAnnealedAndPickled", LengthStatus = "Fixed",
            ManufacturingItem = "OrderFinished", ProductionType = "Rework", PlantGrade = "304",
            Specification = "219*8", TotalQuantity = 50, TotalMeters = 300, TotalWeight = 2000m,
            TotalItemCount = 1, TechnicalRequirements = "NORMAL", CurrentValidQty = 1, CurrentValidWeight = 2000,
            ProductionRatio = 1, TheoreticalUnitWeight = 25m, RowVersion = new byte[8]
        };
        ctx.ProductionBatches.AddRange(normalBatch, reworkBatch);
        await ctx.SaveChangesAsync();

        // 成品检验：返整批次 2000 → 成品检返整量 = 2000，可产成支 = 2000/25 = 80
        ctx.FinalInspections.Add(new FinalInspection
        {
            ProductionBatchId = reworkBatch.Id, BatchNo = "B002", InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today, InspectionType = nameof(InspectionType.FormalInspection),
            Quantity = 1, QualifiedQuantity = 1, DefectReworkWeight = 2000
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var summaries = await ctx.Set<WorkOrderExecutionSummary>().ToListAsync();
        summaries.Should().HaveCount(2);

        // WO002 工单级：可产成重 = 2000×0.96 = 1920；待返整成支 = 80 − 0.98 = 79.02；待返整成重 = 1920−2000 归0
        var s2 = summaries.Single(x => x.WorkOrderNo == "WO002");
        s2.ReworkTheoreticalProduceQty.Should().Be(80);
        s2.ReworkTheoreticalProduceWeight.Should().Be(1920m);
        s2.PendingReworkOutputQty.Should().Be(79.02m);
        s2.PendingReworkOutputWeight.Should().Be(0m);

        // 主号级聚合：有效流转 = (49 + 0.98)/100 = 49.98% → 部分(1)；附返整 = (49 + 80)/100 = 129% > 110%(定尺超量) → 超量(3) → 必返整"是"
        foreach (var s in summaries)
        {
            s.ReworkMainNoStatus.Should().Be(3);
            s.ReworkInputConsistency.Should().Be("是");
        }
    }

    // ==================== G21 次品总量 ====================

    [Fact]
    public async Task RefreshAllAsync_G21次品总量_过程检与成检次品聚合且排除非订单成品批次()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        // 订单成品批次：过程检返整 30 + 入库 20 + 报废 10 = 次品总重 60；成检返整 40 + 入库 25 + 报废 15 = 次品总重 80
        var batch = new ProductionBatch
        {
            BatchNo = "B001", Status = BatchStatus.InProgress, WorkOrderNo = "WO001", SalesOrderNo = "SO001",
            ProductionMainNo = "D01", OrderItemIds = "1", SignDate = DateTime.Today, Salesman = "业务员A",
            DeliveryDate = DateTime.Today.AddMonths(1), MaterialName = "无缝管", SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163", DeliveryState = "SolutionAnnealedAndPickled", LengthStatus = "Fixed",
            ManufacturingItem = "OrderFinished", ProductionType = "InProcess", PlantGrade = "304",
            Specification = "219*8", TotalQuantity = 100, TotalMeters = 600, TotalWeight = 2500m,
            TotalItemCount = 1, TechnicalRequirements = "NORMAL", CurrentValidQty = 50, CurrentValidWeight = 1250,
            ProductionRatio = 2, RowVersion = new byte[8]
        };
        // 非订单成品批次（Surplus）：其过程检验不应计入
        var surplusBatch = new ProductionBatch
        {
            BatchNo = "B002", Status = BatchStatus.InProgress, WorkOrderNo = "WO001", SalesOrderNo = "SO001",
            ProductionMainNo = "D01", OrderItemIds = "1", SignDate = DateTime.Today, Salesman = "业务员A",
            DeliveryDate = DateTime.Today.AddMonths(1), MaterialName = "无缝管", SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163", DeliveryState = "SolutionAnnealedAndPickled", LengthStatus = "Fixed",
            ManufacturingItem = "Surplus", ProductionType = "InProcess", PlantGrade = "304",
            Specification = "219*8", TotalQuantity = 100, TotalMeters = 600, TotalWeight = 2500m,
            TotalItemCount = 1, TechnicalRequirements = "NORMAL", CurrentValidQty = 50, CurrentValidWeight = 1250,
            ProductionRatio = 2, RowVersion = new byte[8]
        };
        ctx.ProductionBatches.AddRange(batch, surplusBatch);
        await ctx.SaveChangesAsync();

        // 过程检验：订单成品批次 返整30/入库20/报废10；非订单成品批次 999（不应计入）
        ctx.ProcessInspections.Add(new ProcessInspection
        {
            ProductionBatchId = batch.Id, ProcessGroupId = 1, ProcessName = "在制修检",
            SectionName = SectionKeys.Inspection, SequenceNumber = 1, InspectionDate = DateTime.Today,
            TheoreticalReworkWeight = 30, TheoreticalWarehouseWeight = 20, TheoreticalScrapWeight = 10
        });
        ctx.ProcessInspections.Add(new ProcessInspection
        {
            ProductionBatchId = surplusBatch.Id, ProcessGroupId = 1, ProcessName = "在制修检",
            SectionName = SectionKeys.Inspection, SequenceNumber = 1, InspectionDate = DateTime.Today,
            TheoreticalReworkWeight = 999
        });
        // 成品检验：返整重40/入库25/报废15；支数 返整4 + 入库2 + 报废1 = 7
        ctx.FinalInspections.Add(new FinalInspection
        {
            ProductionBatchId = batch.Id, BatchNo = "B001", InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today, InspectionType = nameof(InspectionType.FormalInspection),
            Quantity = 10, QualifiedQuantity = 10,
            DefectReworkWeight = 40, DefectWarehouseWeight = 25, DefectScrapWeight = 15,
            DefectReworkQuantity = 4, DefectWarehouseQuantity = 2, DefectScrapQuantity = 1
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().SingleAsync();
        // 过程检侧：次品总重 = 返整 + 入库 + 报废
        s.ProcessInspectionReworkWeight.Should().Be(30);
        s.ProcessInspectionWarehouseWeight.Should().Be(20);
        s.ProcessInspectionScrapWeight.Should().Be(10);
        s.ProcessInspectionDefectWeight.Should().Be(60);
        // 成检侧：次品总支 = 支数之和；次品总重 = 返整 + 入库 + 报废
        s.FinalInspectionReworkWeight.Should().Be(40);
        s.FinalInspectionWarehouseWeight.Should().Be(25);
        s.FinalInspectionScrapWeight.Should().Be(15);
        s.FinalInspectionDefectQty.Should().Be(7);
        s.FinalInspectionDefectWeight.Should().Be(80);
    }

    [Fact]
    public async Task RefreshAllAsync_G21次品总量_无任何检验记录判空()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01");
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().SingleAsync();
        s.ProcessInspectionDefectWeight.Should().BeNull();
        s.ProcessInspectionReworkWeight.Should().BeNull();
        s.ProcessInspectionWarehouseWeight.Should().BeNull();
        s.ProcessInspectionScrapWeight.Should().BeNull();
        s.FinalInspectionDefectQty.Should().BeNull();
        s.FinalInspectionDefectWeight.Should().BeNull();
        s.FinalInspectionReworkWeight.Should().BeNull();
        s.FinalInspectionWarehouseWeight.Should().BeNull();
        s.FinalInspectionScrapWeight.Should().BeNull();
    }

    // ==================== 主号/订单入库状态（Group 15 聚合改造） ====================

    [Fact]
    public async Task RefreshAllAsync_主号入库状态按主号聚合量判定四档()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        var warehouse = await SeedWarehouseAsync(ctx, "成品仓库");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);

        // D01~D04 定尺（需求支数100）、D05~D06 非定尺（需求重量2500）
        ctx.WorkOrders.Add(CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m));
        ctx.WorkOrders.Add(CreateWorkOrder("WO002", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D02", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m));
        ctx.WorkOrders.Add(CreateWorkOrder("WO003", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D03", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m));
        ctx.WorkOrders.Add(CreateWorkOrder("WO004", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D04", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m));
        ctx.WorkOrders.Add(CreateWorkOrder("WO005", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D05", lengthStatus: LengthStatus.NonFixed, totalQty: 100, totalWeight: 2500m));
        ctx.WorkOrders.Add(CreateWorkOrder("WO006", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D06", lengthStatus: LengthStatus.NonFixed, totalQty: 100, totalWeight: 2500m));

        // D01 入库100=需求 → 完结(2)；D02 入库120>需求 → 超额(3)；D03 入库50<需求 → 部分(1)；D04 无入库 → 0
        ctx.InventoryBatches.Add(AddInbound("CK001", "WO001", "SO001", warehouse, 100, 2500m));
        ctx.InventoryBatches.Add(AddInbound("CK002", "WO002", "SO001", warehouse, 120, 3000m));
        ctx.InventoryBatches.Add(AddInbound("CK003", "WO003", "SO001", warehouse, 50, 1250m));
        // D05 非定尺入库重2450（≥2500×0.95=2375 且 ≥2500−100=2400）→ 完结(2)；D06 入库重2000（<2375）→ 部分(1)
        ctx.InventoryBatches.Add(AddInbound("CK005", "WO005", "SO001", warehouse, 98, 2450m));
        ctx.InventoryBatches.Add(AddInbound("CK006", "WO006", "SO001", warehouse, 80, 2000m));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var summaries = await ctx.Set<WorkOrderExecutionSummary>().ToListAsync();
        summaries.Should().HaveCount(6);

        summaries.Single(s => s.ProductionMainNo == "D01").MainNoWarehousingStatus.Should().Be(2);
        summaries.Single(s => s.ProductionMainNo == "D02").MainNoWarehousingStatus.Should().Be(3);
        summaries.Single(s => s.ProductionMainNo == "D03").MainNoWarehousingStatus.Should().Be(1);
        summaries.Single(s => s.ProductionMainNo == "D04").MainNoWarehousingStatus.Should().Be(0);
        summaries.Single(s => s.ProductionMainNo == "D05").MainNoWarehousingStatus.Should().Be(2);
        summaries.Single(s => s.ProductionMainNo == "D06").MainNoWarehousingStatus.Should().Be(1);

        // 工单级独立判定不受主号聚合影响：D02 定尺入库120>100 → 工单超额(3)（与主号一致）
        summaries.Single(s => s.WorkOrderNo == "WO002").WoWarehousingStatus.Should().Be(3);
    }

    [Fact]
    public async Task RefreshAllAsync_重量口径入库超额按需求105判定()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        var warehouse = await SeedWarehouseAsync(ctx, "成品仓库");
        var so = new SalesOrder { OrderNumber = "SO105", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);

        // 非定尺，需求重 2000：WO010 入库2050（102.5%，在 95%~105% 区间内）→ 完结(2)；WO011 入库2120（106% > 105%）→ 超额(3)
        ctx.WorkOrders.Add(CreateWorkOrder("WO010", "SO105", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D10", lengthStatus: LengthStatus.NonFixed, totalQty: 100, totalWeight: 2000m));
        ctx.WorkOrders.Add(CreateWorkOrder("WO011", "SO105", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D11", lengthStatus: LengthStatus.NonFixed, totalQty: 100, totalWeight: 2000m));
        ctx.InventoryBatches.Add(AddInbound("CK501", "WO010", "SO105", warehouse, 100, 2050m));
        ctx.InventoryBatches.Add(AddInbound("CK502", "WO011", "SO105", warehouse, 100, 2120m));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var summaries = await ctx.Set<WorkOrderExecutionSummary>().ToListAsync();
        summaries.Should().HaveCount(2);

        // 工单级：2050 ≤ 2000×1.05=2100 → 完结；2120 > 2100 → 超额
        summaries.Single(s => s.WorkOrderNo == "WO010").WoWarehousingStatus.Should().Be(2);
        summaries.Single(s => s.WorkOrderNo == "WO011").WoWarehousingStatus.Should().Be(3);
        // 主号级：同样按 105% 判定（工单与主号标准一致）
        summaries.Single(s => s.ProductionMainNo == "D10").MainNoWarehousingStatus.Should().Be(2);
        summaries.Single(s => s.ProductionMainNo == "D11").MainNoWarehousingStatus.Should().Be(3);
    }

    [Fact]
    public async Task RefreshAllAsync_订单入库状态从主号状态上卷()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        var warehouse = await SeedWarehouseAsync(ctx, "成品仓库");
        var so100 = new SalesOrder { OrderNumber = "SO100", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        var so200 = new SalesOrder { OrderNumber = "SO200", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        var so300 = new SalesOrder { OrderNumber = "SO300", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.AddRange(so100, so200, so300);

        // SO100：D01 完结(2) + D02 超额(3) → 订单2
        ctx.WorkOrders.Add(CreateWorkOrder("WO100A", "SO100", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", totalQty: 100, totalWeight: 2500m));
        ctx.WorkOrders.Add(CreateWorkOrder("WO100B", "SO100", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D02", totalQty: 100, totalWeight: 2500m));
        // SO200：D01 完结(2) + D02 部分(1) → 订单1
        ctx.WorkOrders.Add(CreateWorkOrder("WO200A", "SO200", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", totalQty: 100, totalWeight: 2500m));
        ctx.WorkOrders.Add(CreateWorkOrder("WO200B", "SO200", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D02", totalQty: 100, totalWeight: 2500m));
        // SO300：两主号均无入库 → 订单0
        ctx.WorkOrders.Add(CreateWorkOrder("WO300A", "SO300", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", totalQty: 100, totalWeight: 2500m));
        ctx.WorkOrders.Add(CreateWorkOrder("WO300B", "SO300", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D02", totalQty: 100, totalWeight: 2500m));

        ctx.InventoryBatches.Add(AddInbound("CK101", "WO100A", "SO100", warehouse, 100, 2500m)); // D01 完结
        ctx.InventoryBatches.Add(AddInbound("CK102", "WO100B", "SO100", warehouse, 120, 3000m)); // D02 超额
        ctx.InventoryBatches.Add(AddInbound("CK201", "WO200A", "SO200", warehouse, 100, 2500m)); // D01 完结
        ctx.InventoryBatches.Add(AddInbound("CK202", "WO200B", "SO200", warehouse, 50, 1250m));   // D02 部分
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var summaries = await ctx.Set<WorkOrderExecutionSummary>().ToListAsync();
        summaries.Should().HaveCount(6);

        summaries.Where(s => s.SalesOrderNo == "SO100")
            .Should().OnlyContain(s => s.OrderWarehousingStatus == 2);
        summaries.Where(s => s.SalesOrderNo == "SO200")
            .Should().OnlyContain(s => s.OrderWarehousingStatus == 1);
        summaries.Where(s => s.SalesOrderNo == "SO300")
            .Should().OnlyContain(s => s.OrderWarehousingStatus == 0);
    }

    private InventoryBatch AddInbound(string batchNo, string workOrderNo, string salesOrderNo, Warehouse warehouse, int qty, decimal weight)
    {
        return new InventoryBatch
        {
            BatchNo = batchNo,
            WarehouseId = warehouse.Id,
            Warehouse = warehouse,
            MaterialType = MES.Core.Constants.InventoryMaterialTypes.OrderFinished,
            PlantGrade = "304",
            Specification = "219*8",
            InboundSource = "OrderFinished",
            SourceName = "成品入库",
            InboundDate = DateTime.Today,
            WorkOrderNo = workOrderNo,
            SalesOrderNo = salesOrderNo,
            InitialQuantity = qty,
            InitialWeight = weight,
            RemainingQuantity = qty,
            RemainingWeight = weight,
            RowVersion = new byte[8]
        };
    }

    /// <summary>构造"满足"批次：InputQuantity×ProductionRatio×合格率 0.98 = 100（=工单需求），主号有效流转=100 → MainNoFlowStatus=2</summary>
    private ProductionBatch CreateSatisfiedBatch(string batchNo, string workOrderNo, string salesOrderNo, string mainNo, BatchStatus status, int inputQty = 51)
    {
        return new ProductionBatch
        {
            BatchNo = batchNo,
            Status = status,
            WorkOrderNo = workOrderNo,
            SalesOrderNo = salesOrderNo,
            ProductionMainNo = mainNo,
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "业务员A",
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
            InputQuantity = inputQty,
            InputWeight = 1250m,
            CurrentValidQty = inputQty,
            CurrentValidWeight = 1250,
            ProductionRatio = 2,
            RowVersion = new byte[8],
            ProcessGroups = new List<ProcessGroup>
            {
                new() { ProcessName = "60冷轧", SequenceNumber = 1, ColdRollDraw = 1, Solution = 2 }
            }
        };
    }

    // ==================== G16 关注状态 5 档（0=主号暂停 1=主号完成 2=原料锁定 3=生产执行 4=成品检验） ====================

    [Fact]
    public async Task RefreshAllAsync_ScheduleStage五档_主号暂停档0()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        ctx.ProductionBatches.Add(CreateSatisfiedBatch("B001", "WO001", "SO001", "D01", BatchStatus.InProgress));
        // 工单需求调整：主号暂停（联动连带保证同主号未完结工单一致）
        ctx.Set<OrderDemandAdjustment>().Add(new OrderDemandAdjustment { WorkOrderId = wo.Id, IsUrging = false, IsBatchDelivery = false, IsPaused = true });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        s.MainNoFlowStatus.Should().Be(2); // 前提：有效流转满足
        s.ScheduleStage.Should().Be(0);    // 主号暂停优先于一切
    }

    [Fact]
    public async Task RefreshAllAsync_ScheduleStage五档_主号完成档1()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        await SeedWarehouseAsync(ctx, "成品仓库");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        ctx.WorkOrders.Add(CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m));
        ctx.ProductionBatches.Add(CreateSatisfiedBatch("B001", "WO001", "SO001", "D01", BatchStatus.InProgress));
        // 完整入库 100 支 → 主号入库=完结(2)，真正闭环
        var warehouse = await ctx.Warehouses.FirstAsync();
        ctx.InventoryBatches.Add(AddInbound("CK001", "WO001", "SO001", warehouse, 100, 2500m));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        s.MainNoWarehousingStatus.Should().Be(2);
        s.ScheduleStage.Should().Be(1);    // 主号完成
    }

    [Fact]
    public async Task RefreshAllAsync_ScheduleStage_强制完成档1()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        // 批次在产：无强制完成时应判档3生产执行，强制完成 → 档1主号完成
        ctx.ProductionBatches.Add(CreateSatisfiedBatch("B001", "WO001", "SO001", "D01", BatchStatus.InProgress));
        // 工单需求调整：强制完成（主号级，与暂停互斥）
        ctx.Set<OrderDemandAdjustment>().Add(new OrderDemandAdjustment { WorkOrderId = wo.Id, IsForceCompleted = true });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        s.IsForceCompleted.Should().BeTrue();
        s.ScheduleStage.Should().Be(1);    // 强制完成 → 主号完成
    }

    [Fact]
    public async Task RefreshAllAsync_ScheduleStage五档_原料锁定档2()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        ctx.WorkOrders.Add(CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m));
        // 投料不足：10*2*0.98=19.6→20 → 有效流转=20% → 主号状态不满足
        ctx.ProductionBatches.Add(CreateSatisfiedBatch("B001", "WO001", "SO001", "D01", BatchStatus.InProgress, inputQty: 10));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        s.MainNoFlowStatus.Should().NotBe(2);
        s.MainNoWarehousingStatus.Should().Be(0);
        s.ScheduleStage.Should().Be(2);    // 原料锁定（待料）
    }

    [Fact]
    public async Task RefreshAllAsync_ScheduleStage五档_生产执行档3()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        ctx.WorkOrders.Add(CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m));
        ctx.ProductionBatches.Add(CreateSatisfiedBatch("B001", "WO001", "SO001", "D01", BatchStatus.InProgress));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        s.MainNoFlowStatus.Should().Be(2);
        s.ScheduleStage.Should().Be(3);    // 生产执行（存在未产/在产/暂停批次）
    }

    [Fact]
    public async Task RefreshAllAsync_ScheduleStage五档_成品检验档4()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        ctx.WorkOrders.Add(CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m));
        // 批次已完成（无未产/在产/暂停批次）→ 成品检验档
        ctx.ProductionBatches.Add(CreateSatisfiedBatch("B001", "WO001", "SO001", "D01", BatchStatus.Completed));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        s.MainNoFlowStatus.Should().Be(2);
        s.ScheduleStage.Should().Be(4);    // 成品检验（主号已满足、无在产批次）
    }

    [Fact]
    public async Task RefreshAllAsync_ScheduleStage五档_同主号混合批次主号级档3()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        // 同主号 D01 下两个工单：WO001 在产、WO002 已完成
        ctx.WorkOrders.Add(CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m));
        ctx.WorkOrders.Add(CreateWorkOrder("WO002", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m));
        ctx.ProductionBatches.Add(CreateSatisfiedBatch("B001", "WO001", "SO001", "D01", BatchStatus.InProgress));
        ctx.ProductionBatches.Add(CreateSatisfiedBatch("B002", "WO002", "SO001", "D01", BatchStatus.Completed));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var summaries = await ctx.Set<WorkOrderExecutionSummary>().OrderBy(x => x.WorkOrderNo).ToListAsync();
        summaries.Should().HaveCount(2);
        // 主号下任一工单有活动批次 → 整主号档3（生产执行），WO002 也随之档3
        summaries[0].ScheduleStage.Should().Be(3);
        summaries[1].ScheduleStage.Should().Be(3);
    }

    [Fact]
    public async Task RefreshAllAsync_ScheduleStage五档_同主号全部完成主号级档4()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        // 同主号 D01 下两个工单，批次均已完成 → 整主号档4（成品检验）
        ctx.WorkOrders.Add(CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m));
        ctx.WorkOrders.Add(CreateWorkOrder("WO002", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m));
        ctx.ProductionBatches.Add(CreateSatisfiedBatch("B001", "WO001", "SO001", "D01", BatchStatus.Completed));
        ctx.ProductionBatches.Add(CreateSatisfiedBatch("B002", "WO002", "SO001", "D01", BatchStatus.Completed));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var summaries = await ctx.Set<WorkOrderExecutionSummary>().OrderBy(x => x.WorkOrderNo).ToListAsync();
        summaries.Should().HaveCount(2);
        // 主号下全部工单无活动批次 → 整主号档4（成品检验）
        summaries[0].ScheduleStage.Should().Be(4);
        summaries[1].ScheduleStage.Should().Be(4);
    }

    [Fact]
    public async Task RefreshAllAsync_档4成检中批次_流转性为Normal非疑问()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        ctx.WorkOrders.Add(CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m));
        // 批次处于成检中（InFinalInspection）→ 档4，流转性应为 Normal（正常成检流程，非疑问）
        ctx.ProductionBatches.Add(CreateSatisfiedBatch("B001", "WO001", "SO001", "D01", BatchStatus.InFinalInspection));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        s.ScheduleStage.Should().Be(4);    // 成品检验（无活动批次）
        s.ProductionFlowProperty.Should().Be(ProductionFlowKeys.Normal);  // 成检中 = 正常流程
    }

    [Fact]
    public async Task RefreshAllAsync_档4全完成批次_流转性为Skip()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        ctx.WorkOrders.Add(CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", lengthStatus: LengthStatus.Fixed, totalQty: 100, totalWeight: 2500m));
        // 批次全部完成 → 档4 且无未完成 → 流转性 Skip（无关注）
        ctx.ProductionBatches.Add(CreateSatisfiedBatch("B001", "WO001", "SO001", "D01", BatchStatus.Completed));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        s.ScheduleStage.Should().Be(4);
        s.ProductionFlowProperty.Should().Be(ProductionFlowKeys.Skip);
    }

    // ==================== G7/G8 出库量按出库工单号匹配（同第4/5类完成口径） ====================

    [Fact]
    public async Task RefreshAllAsync_G7G8出库量_同仓库批多工单按出库工单号区分()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        var warehouse = await SeedWarehouseAsync(ctx, "原料仓库");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        ctx.WorkOrders.Add(CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01"));
        ctx.WorkOrders.Add(CreateWorkOrder("WO002", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D02"));
        await ctx.SaveChangesAsync();
        var wo1 = await ctx.WorkOrders.FirstAsync(w => w.WorkOrderNo == "WO001");
        var wo2 = await ctx.WorkOrders.FirstAsync(w => w.WorkOrderNo == "WO002");

        // 同一仓库批 CK001 被两个工单计划引用（余料共享）
        var ib = AddInbound("CK001", "WO001", "SO001", warehouse, 100, 2500m);
        ctx.InventoryBatches.Add(ib);
        await ctx.SaveChangesAsync();
        ctx.Set<InventoryPlan>().Add(new InventoryPlan
        {
            WorkOrderId = wo1.Id, PlanDate = DateTime.Today, InventoryBatchNo = "CK001", BatchNo = "CK001",
            MaterialType = "OrderFinished", PlantGrade = "304", Specification = "219*8", UsedWeight = 1000m
        });
        ctx.Set<InventoryPlan>().Add(new InventoryPlan
        {
            WorkOrderId = wo2.Id, PlanDate = DateTime.Today, InventoryBatchNo = "CK001", BatchNo = "CK001",
            MaterialType = "OrderFinished", PlantGrade = "304", Specification = "219*8", UsedWeight = 800m,
            ReworkType = ReworkType.EmptyDrawing
        });
        // 出库：WO001 出 100kg、WO002 出 80kg（同一仓库批，按出库工单号区分）
        ctx.Set<OutboundRecord>().Add(new OutboundRecord
        {
            InventoryBatchId = ib.Id, BatchNo = "CK001", OutboundType = OutboundType.ProductionPick,
            WorkOrderNo = "WO001", OutboundQuantity = 4, OutboundWeight = 100m,
            OutboundDate = DateTime.Today.AddDays(-1), CreatedBy = "t", UpdatedBy = "t"
        });
        ctx.Set<OutboundRecord>().Add(new OutboundRecord
        {
            InventoryBatchId = ib.Id, BatchNo = "CK001", OutboundType = OutboundType.ProductionPick,
            WorkOrderNo = "WO002", OutboundQuantity = 3, OutboundWeight = 80m,
            OutboundDate = DateTime.Today, CreatedBy = "t", UpdatedBy = "t"
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s1 = await ctx.Set<WorkOrderExecutionSummary>().SingleAsync(s => s.WorkOrderNo == "WO001");
        var s2 = await ctx.Set<WorkOrderExecutionSummary>().SingleAsync(s => s.WorkOrderNo == "WO002");
        // G7 库存使用：WO001 只算自己出库工单号下的 100，不含 WO002 的 80
        s1.InventoryPlanWeight.Should().Be(1000m);
        s1.InventoryOutWeight.Should().Be(100m);
        // G8 库料改制：WO002 只算自己出库工单号下的 80，不含 WO001 的 100
        s2.ReworkPlanWeight.Should().Be(800m);
        s2.ReworkPlanInputWeight.Should().Be(80m);
        // 截止到料日出库侧：各工单取自己出库记录的最大日期
        s1.CutoffArrivalDate.Should().Be(DateTime.Today.AddDays(-1));
        s2.CutoffArrivalDate.Should().Be(DateTime.Today);
    }

    [Fact]
    public async Task RefreshAllAsync_G7G8出库量_出库工单号为空不计入()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        var warehouse = await SeedWarehouseAsync(ctx, "原料仓库");
        var so = new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" };
        ctx.SalesOrders.Add(so);
        ctx.WorkOrders.Add(CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01"));
        await ctx.SaveChangesAsync();
        var wo1 = await ctx.WorkOrders.FirstAsync(w => w.WorkOrderNo == "WO001");

        var ib = AddInbound("CK001", "WO001", "SO001", warehouse, 100, 2500m);
        ctx.InventoryBatches.Add(ib);
        await ctx.SaveChangesAsync();
        ctx.Set<InventoryPlan>().Add(new InventoryPlan
        {
            WorkOrderId = wo1.Id, PlanDate = DateTime.Today, InventoryBatchNo = "CK001", BatchNo = "CK001",
            MaterialType = "OrderFinished", PlantGrade = "304", Specification = "219*8", UsedWeight = 1000m
        });
        // 出库但未填出库工单号 → 与完成匹配同口径：不计入执行量
        ctx.Set<OutboundRecord>().Add(new OutboundRecord
        {
            InventoryBatchId = ib.Id, BatchNo = "CK001", OutboundType = OutboundType.ProductionPick,
            OutboundQuantity = 8, OutboundWeight = 200m,
            OutboundDate = DateTime.Today, CreatedBy = "t", UpdatedBy = "t"
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s1 = await ctx.Set<WorkOrderExecutionSummary>().SingleAsync(s => s.WorkOrderNo == "WO001");
        s1.InventoryOutWeight.Should().Be(0m);
        s1.CutoffArrivalDate.Should().BeNull();
    }

    // ==================== 变形工序完成三档 + 生产关注工序生产收尾 ====================

    [Fact]
    public async Task RefreshAllAsync_变形工序完成三档_无批次判略()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        ctx.SalesOrders.Add(new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" });
        ctx.WorkOrders.Add(CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01"));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().SingleAsync(x => x.WorkOrderNo == "WO001");
        // 没投料（无批次）→ 无在产批次 → 略
        s.DeformedProcessCompleted.Should().BeNull();
        s.ProductionAttentionProcess.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAllAsync_变形工序完成三档_批次全成检判略()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        ctx.SalesOrders.Add(new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" });
        ctx.WorkOrders.Add(CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01"));
        // 生产编号既不在产也未产（全成检）→ 无在产批次 → 略
        ctx.ProductionBatches.Add(CreateDeformedBatch("B001", "WO001", "D01", BatchStatus.InFinalInspection, "ColdRoll60", "ColdRollDraw", 3, null));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().SingleAsync(x => x.WorkOrderNo == "WO001");
        s.DeformedProcessCompleted.Should().BeNull();
        s.ProductionAttentionProcess.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAllAsync_变形工序完成三档_无冷轧待量判是_关注生产收尾()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        ctx.SalesOrders.Add(new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" });
        ctx.WorkOrders.Add(CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01"));
        // 在产批次：当前在 60冷轧·光亮退火（非目标工段冷轧拔）→ 冷轧待量=0 → 变形完成=是 → 关注生产收尾
        ctx.ProductionBatches.Add(CreateDeformedBatch("B001", "WO001", "D01", BatchStatus.InProgress, "ColdRoll60", "Solution", 3, null));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().SingleAsync(x => x.WorkOrderNo == "WO001");
        s.DeformedProcessCompleted.Should().BeTrue();
        s.ProductionAttentionProcess.Should().Be(ProductionAttentionKeys.Finish);
    }

    [Fact]
    public async Task RefreshAllAsync_变形工序完成三档_有冷轧待量判否_关注原逻辑()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        ctx.SalesOrders.Add(new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" });
        ctx.WorkOrders.Add(CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01"));
        // 在产批次：当前在 60冷轧·冷轧拔（目标工段）且未完成 → 冷轧待量>0 → 变形完成=否 → 关注=最靠前工序（60冷轧）
        ctx.ProductionBatches.Add(CreateDeformedBatch("B001", "WO001", "D01", BatchStatus.InProgress, "ColdRoll60", "ColdRollDraw", 3, null));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().SingleAsync(x => x.WorkOrderNo == "WO001");
        s.DeformedProcessCompleted.Should().BeFalse();
        s.ProductionAttentionProcess.Should().Be(ProcessKeys.ColdRoll60);
    }

    [Fact]
    public async Task RefreshAllAsync_主号关注工序_生产收尾上卷()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        ctx.SalesOrders.Add(new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" });
        ctx.WorkOrders.Add(CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01"));
        ctx.WorkOrders.Add(CreateWorkOrder("WO002", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01"));
        // WO001 生产收尾（是），剩余工量 5 更大；WO002 否（关注 60冷轧），剩余工量 2 → 主号关注=生产收尾
        ctx.ProductionBatches.Add(CreateDeformedBatch("B001", "WO001", "D01", BatchStatus.InProgress, "ColdRoll60", "Solution", 5, null));
        ctx.ProductionBatches.Add(CreateDeformedBatch("B002", "WO002", "D01", BatchStatus.InProgress, "ColdRoll60", "ColdRollDraw", 2, null));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshAllAsync();

        var s1 = await ctx.Set<WorkOrderExecutionSummary>().SingleAsync(x => x.WorkOrderNo == "WO001");
        var s2 = await ctx.Set<WorkOrderExecutionSummary>().SingleAsync(x => x.WorkOrderNo == "WO002");
        s1.ProductionAttentionProcess.Should().Be(ProductionAttentionKeys.Finish);
        s2.ProductionAttentionProcess.Should().Be(ProcessKeys.ColdRoll60);
        // 主号关注工序取剩余工量最大所在工单（WO001）→ 生产收尾，两个工单都上卷为生产收尾
        s1.MainNoAttentionProcess.Should().Be(ProductionAttentionKeys.Finish);
        s2.MainNoAttentionProcess.Should().Be(ProductionAttentionKeys.Finish);
    }

    // ==================== 产能工量（外购/库存剔除） ====================

    [Fact]
    public async Task RefreshAllAsync_产能工量_外购成品采购覆盖全部重量_为0()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        ctx.SalesOrders.Add(new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" });
        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        // 外购：成品采购计划覆盖全部重量
        ctx.PurchaseFinishedPlans.Add(new PurchaseFinishedPlan
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            PlantGrade = "304",
            Specification = "219*8",
            LengthStatus = LengthStatus.Fixed,
            ProductType = FinishedProductType.Order,
            RequiredWeight = 2500m
        });
        ctx.ProductionBatches.Add(CreateSatisfiedBatch("B001", "WO001", "SO001", "D01", BatchStatus.InProgress));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, new List<DailyOutputEstimateDto>
        {
            new() { MinOuterDiameter = 18, DailyOutputTons = 4m }
        });
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        s.FinishPlanWeight.Should().Be(2500m);
        s.CapacityWorkDays.Should().Be(0);   // 外购覆盖全部重量 → 内部剩余产能=0
    }

    [Fact]
    public async Task RefreshAllAsync_产能工量_非外购按内部剩余计算()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        ctx.SalesOrders.Add(new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" });
        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        // 内部生产：荒管采购计划
        ctx.PurchaseSemiPlans.Add(new PurchaseSemiPlan
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            RequiredPieces = 100,
            RequiredWeight = 2500m,
            InputMultiple = 1,
            PlantGrade = "304",
            RawMaterialSpec = "219*8",
            RequiredDate = DateTime.Today
        });
        ctx.ProductionBatches.Add(CreateSatisfiedBatch("B001", "WO001", "SO001", "D01", BatchStatus.InProgress));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, new List<DailyOutputEstimateDto>
        {
            new() { MinOuterDiameter = 18, DailyOutputTons = 4m }
        });
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        s.SemiPlanWeight.Should().Be(2500m);
        s.CapacityWorkDays.Should().Be(1);   // ceil(2500kg/1000 / 4吨每天) = ceil(0.625) = 1
    }

    [Fact]
    public async Task RefreshAllAsync_产能工量_库存使用覆盖部分_剔除库存()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        ctx.SalesOrders.Add(new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" });
        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        // 库存使用计划覆盖 2000kg，剩余 500kg 需内部生产
        ctx.InventoryPlans.Add(new InventoryPlan
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            InventoryBatchNo = "CK001",
            BatchNo = "CK001",
            MaterialType = "OrderFinished",
            PlantGrade = "304",
            Specification = "219*8",
            UsedWeight = 2000m
        });
        ctx.ProductionBatches.Add(CreateSatisfiedBatch("B001", "WO001", "SO001", "D01", BatchStatus.InProgress));
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, new List<DailyOutputEstimateDto>
        {
            new() { MinOuterDiameter = 18, DailyOutputTons = 4m }
        });
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        s.InventoryPlanWeight.Should().Be(2000m);
        s.CapacityWorkDays.Should().Be(1);   // ceil((2500-2000)/1000 / 4) = ceil(0.125) = 1
    }

    [Fact]
    public async Task RefreshAllAsync_产能工量_外购批次完成_不重复扣减()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        ctx.SalesOrders.Add(new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" });
        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        // 外购成品采购计划覆盖一半重量，剩余一半需内部生产
        ctx.PurchaseFinishedPlans.Add(new PurchaseFinishedPlan
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            PlantGrade = "304",
            Specification = "219*8",
            LengthStatus = LengthStatus.Fixed,
            ProductType = FinishedProductType.Order,
            RequiredWeight = 1250m
        });
        // 内部在制批次（未完成，不计入 completedOutput，仅保证主号有活动批次）
        ctx.ProductionBatches.Add(CreateSatisfiedBatch("B001", "WO001", "SO001", "D01", BatchStatus.InProgress));
        // 外购批次已完成（OutsourcedPurchased）：其重量已由成品采购计划覆盖，不得再经 completedOutput 重复扣减
        var outsourced = CreateSatisfiedBatch("B002", "WO001", "SO001", "D01", BatchStatus.Completed);
        outsourced.ProductionType = "OutsourcedPurchased";
        outsourced.CurrentValidWeight = 1500;
        outsourced.InputWeight = 1500;
        outsourced.ProcessGroups = new List<ProcessGroup>();
        ctx.ProductionBatches.Add(outsourced);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, new List<DailyOutputEstimateDto>
        {
            new() { MinOuterDiameter = 18, DailyOutputTons = 4m }
        });
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        s.FinishPlanWeight.Should().Be(1250m);
        // 外购批次不得重复扣减：capacityWeight = 2500 - 1250(外购) - 0(库存) - 0(completedOutput 排除外购批次) = 1250 → ceil(1250/1000/4) = 1
        // 若不排除外购批次：2500 - 1250 - 1500(外购批次被重复扣) = -250 → 被钳为 0（错误低估）
        s.CapacityWorkDays.Should().Be(1);
    }

    [Fact]
    public async Task RefreshAllAsync_产能工量_库存批次完成_不重复扣减()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        ctx.SalesOrders.Add(new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" });
        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        // 库存使用计划覆盖一半重量，剩余一半需内部生产
        ctx.InventoryPlans.Add(new InventoryPlan
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            InventoryBatchNo = "CK001",
            BatchNo = "CK001",
            MaterialType = "OrderFinished",
            PlantGrade = "304",
            Specification = "219*8",
            UsedWeight = 1250m
        });
        // 内部在制批次（未完成，不计入 completedOutput，仅保证主号有活动批次）
        ctx.ProductionBatches.Add(CreateSatisfiedBatch("B001", "WO001", "SO001", "D01", BatchStatus.InProgress));
        // 库存投料批次已完成（Inventory）：其重量已由库存使用计划覆盖，不得再经 completedOutput 重复扣减
        var stock = CreateSatisfiedBatch("B002", "WO001", "SO001", "D01", BatchStatus.Completed);
        stock.ProductionType = "Inventory";
        stock.CurrentValidWeight = 1500;
        stock.InputWeight = 1500;
        stock.ProcessGroups = new List<ProcessGroup>();
        ctx.ProductionBatches.Add(stock);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, new List<DailyOutputEstimateDto>
        {
            new() { MinOuterDiameter = 18, DailyOutputTons = 4m }
        });
        await svc.RefreshAllAsync();

        var s = await ctx.Set<WorkOrderExecutionSummary>().FirstAsync();
        s.InventoryPlanWeight.Should().Be(1250m);
        // 库存批次不得重复扣减：capacityWeight = 2500 - 0(外购) - 1250(库存) - 0(completedOutput 排除库存批次) = 1250 → ceil(1250/1000/4) = 1
        // 若不排除库存批次：2500 - 1250 - 1500(库存批次被重复扣) = -250 → 被钳为 0（错误低估）
        s.CapacityWorkDays.Should().Be(1);
    }

    private WorkOrderListSummaryRefreshService CreateListSummaryService(AppDbContext ctx, List<DailyOutputEstimateDto> dailyEstimates)
    {
        var loggerMock = new Mock<ILogger<WorkOrderListSummaryRefreshService>>();
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        var dailyOutputMock = new Mock<IDailyOutputEstimateService>();
        dailyOutputMock.Setup(x => x.GetAllAsync()).ReturnsAsync(dailyEstimates);
        return new WorkOrderListSummaryRefreshService(ctx, loggerMock.Object, configMock.Object, dailyOutputMock.Object);
    }

    [Fact]
    public async Task RefreshListSummary_产能工量_主号完成档1_置null()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        ctx.SalesOrders.Add(new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" });
        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        // 库存投料批次（Inventory 已完成）：被排除不计 completedOutput → 剩余重量=主号总重量=2500（本应高估 1 天）
        var stock = CreateSatisfiedBatch("B002", "WO001", "SO001", "D01", BatchStatus.Completed);
        stock.ProductionType = "Inventory";
        stock.CurrentValidWeight = 1500;
        stock.InputWeight = 1500;
        stock.ProcessGroups = new List<ProcessGroup>();
        ctx.ProductionBatches.Add(stock);
        // 执行读模型标记该主号「主号完成」（ScheduleStage=1）
        SeedSummary(ctx, "WO001", "SO001", "D01", specification: "219*8", totalWeight: 2500m);
        await ctx.SaveChangesAsync();
        var es = ctx.Set<WorkOrderExecutionSummary>().Single();
        es.ScheduleStage = 1;
        await ctx.SaveChangesAsync();

        var svc = CreateListSummaryService(ctx, new List<DailyOutputEstimateDto>
        {
            new() { MinOuterDiameter = 18, DailyOutputTons = 4m }
        });
        await svc.RefreshBySalesOrderAsync("SO001");

        var row = await ctx.Set<WorkOrderListSummary>().FirstAsync();
        // 主号完成 → 无需内部产能 → 产能工量置 null（显示「-」），与执行表 ScheduleStage=1 一致，避免两页不一致
        row.CapacityWorkDays.Should().BeNull();
    }

    [Fact]
    public async Task RefreshListSummary_产能工量_生产执行档3_按公式计算()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        ctx.SalesOrders.Add(new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" });
        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        // 库存投料批次（Inventory 已完成）：排除后剩余重量=2500
        var stock = CreateSatisfiedBatch("B002", "WO001", "SO001", "D01", BatchStatus.Completed);
        stock.ProductionType = "Inventory";
        stock.CurrentValidWeight = 1500;
        stock.InputWeight = 1500;
        stock.ProcessGroups = new List<ProcessGroup>();
        ctx.ProductionBatches.Add(stock);
        // 执行读模型标记该主号「生产执行」（ScheduleStage=3）→ 不影响产能工量公式
        SeedSummary(ctx, "WO001", "SO001", "D01", specification: "219*8", totalWeight: 2500m);
        await ctx.SaveChangesAsync();
        var es = ctx.Set<WorkOrderExecutionSummary>().Single();
        es.ScheduleStage = 3;
        await ctx.SaveChangesAsync();

        var svc = CreateListSummaryService(ctx, new List<DailyOutputEstimateDto>
        {
            new() { MinOuterDiameter = 18, DailyOutputTons = 4m }
        });
        await svc.RefreshBySalesOrderAsync("SO001");

        var row = await ctx.Set<WorkOrderListSummary>().FirstAsync();
        // 生产执行 → 按公式：ceil(2500/1000 / 4) = ceil(0.625) = 1
        row.CapacityWorkDays.Should().Be(1);
    }

    [Fact]
    public async Task RefreshListSummary_产能工量_非档1剩余零_为0()
    {
        using var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, "测试客户");
        ctx.SalesOrders.Add(new SalesOrder { OrderNumber = "SO001", SignDate = DateTime.Today, Status = SalesOrderStatus.Confirmed, RowVersion = new byte[8], CustomerName = "测试客户", Salesman = "测试业务员" });
        var wo = CreateWorkOrder("WO001", "SO001", WorkOrderStatus.Confirmed, salesman: "业务员A", mainNo: "D01", totalWeight: 2500m);
        ctx.WorkOrders.Add(wo);
        // 成品采购计划覆盖全部重量 → 剩余重量=0
        ctx.PurchaseFinishedPlans.Add(new PurchaseFinishedPlan
        {
            WorkOrderId = wo.Id,
            PlanDate = DateTime.Today,
            PlantGrade = "304",
            Specification = "219*8",
            LengthStatus = LengthStatus.Fixed,
            ProductType = FinishedProductType.Order,
            RequiredWeight = 2500m
        });
        // 执行读模型标记该主号「生产执行」（ScheduleStage=3）→ 非档1，剩余≤0 时产能工量=0（显示「0天」）
        SeedSummary(ctx, "WO001", "SO001", "D01", specification: "219*8", totalWeight: 2500m);
        await ctx.SaveChangesAsync();
        var es = ctx.Set<WorkOrderExecutionSummary>().Single();
        es.ScheduleStage = 3;
        await ctx.SaveChangesAsync();

        var svc = CreateListSummaryService(ctx, new List<DailyOutputEstimateDto>
        {
            new() { MinOuterDiameter = 18, DailyOutputTons = 4m }
        });
        await svc.RefreshBySalesOrderAsync("SO001");

        var row = await ctx.Set<WorkOrderListSummary>().FirstAsync();
        // 生产执行且剩余重量≤0 → 产能工量 0（非档1 显示「0天」，与执行表一致）
        row.CapacityWorkDays.Should().Be(0);
    }

    private ProductionBatch CreateDeformedBatch(string batchNo, string workOrderNo, string mainNo, BatchStatus status,
        string? currentGroupName, string? currentSectionName, int remainingWorkDays, bool? currentSectionCompleted)
    {
        return new ProductionBatch
        {
            BatchNo = batchNo,
            Status = status,
            WorkOrderNo = workOrderNo,
            SalesOrderNo = "SO001",
            ProductionMainNo = mainNo,
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "业务员A",
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
            InputQuantity = 100,
            InputWeight = 2500m,
            CurrentValidQty = 100,
            CurrentValidWeight = 1000,
            ProductionRatio = 2,
            RemainingWorkDays = remainingWorkDays,
            CurrentGroupName = currentGroupName,
            CurrentSectionName = currentSectionName,
            CurrentSectionCompleted = currentSectionCompleted,
            RowVersion = new byte[8],
            ProcessGroups = new List<ProcessGroup>
            {
                new() { ProcessName = ProcessKeys.ColdRoll60, SequenceNumber = 1, ColdRollDraw = 1 }
            }
        };
    }

    // ==================== GetErrorDoubtInputItemsAsync（错误疑问投料卡片） ====================

    [Fact]
    public async Task GetErrorDoubtInputItemsAsync_仅返回原料锁定且到料实投一致性错误疑问行()
    {
        using var ctx = CreateDbContext();
        // 重置容差快照（防其它测试污染静态状态）
        MaterialPlanToleranceProvider.Apply(0.03m);
        // 以下夹具均 ScheduleStage=2（原料锁定）
        // WO001：计划=100、现可=0、已投=50 → 错误-无料已投(4)，缺料=Max(0,100-0)=100
        SeedComputedSummary(ctx, "WO001", e => { e.ScheduleStage = 2; e.SemiPlanWeight = 100m; e.InputWeight = 50m; });
        // WO002：现可(到货量)=100、已投=120 → 疑问-到料超投(3)
        SeedComputedSummary(ctx, "WO002", e => { e.ScheduleStage = 2; e.SemiInWeight = 100m; e.InputWeight = 120m; e.TotalWeight = 800m; });
        // WO003：现可(到货量)=100、已投=50、截止到料日=昨天 → 疑问-到料少投(2)
        SeedComputedSummary(ctx, "WO003", e => { e.ScheduleStage = 2; e.SemiInWeight = 100m; e.InputWeight = 50m; e.CutoffArrivalDate = DateTime.Today.AddDays(-1); });
        // WO004：现可=100、已投=100 → 一致(0)，不返回
        SeedComputedSummary(ctx, "WO004", e => { e.ScheduleStage = 2; e.SemiInWeight = 100m; e.InputWeight = 100m; });
        // WO005：现可=100、已投=50、截止到料日=今天 → 待投(1)，不返回
        SeedComputedSummary(ctx, "WO005", e => { e.ScheduleStage = 2; e.SemiInWeight = 100m; e.InputWeight = 50m; e.CutoffArrivalDate = DateTime.Today; });
        // WO006：ScheduleStage=0（非原料锁定）、已投=50 → 错误-无料已投(4)，但非锁定不返回
        SeedComputedSummary(ctx, "WO006", e => { e.InputWeight = 50m; });
        // WO007：ScheduleStage=3（生产执行）、已投=50 → 阶段门控走 6 略，且非锁定不返回
        SeedComputedSummary(ctx, "WO007", e => { e.ScheduleStage = 3; e.InputWeight = 50m; });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var items = await svc.GetErrorDoubtInputItemsAsync();

        items.Should().HaveCount(3);
        items.Select(i => i.WorkOrderNo).Should().ContainInOrder("WO001", "WO002", "WO003");

        var w001 = items.Single(i => i.WorkOrderNo == "WO001");
        w001.PlanInputConsistency.Should().Be(4);
        w001.SalesOrderNo.Should().Be("SOWO001");
        w001.ProductionMainNo.Should().Be("D01");
        w001.PlantGrade.Should().Be("304");
        w001.Specification.Should().Be("219*8");
        w001.TotalPlanWeight.Should().Be(100m);
        w001.TotalAvailableWeight.Should().Be(0m);
        w001.TotalMissingWeight.Should().Be(100m);
        w001.ActualInputWeight.Should().Be(50m);

        var w002 = items.Single(i => i.WorkOrderNo == "WO002");
        w002.PlanInputConsistency.Should().Be(3);
        w002.SalesOrderNo.Should().Be("SOWO002");
        w002.TotalPlanWeight.Should().Be(0m);
        w002.TotalAvailableWeight.Should().Be(100m);
        w002.TotalMissingWeight.Should().Be(0m);
        w002.ActualInputWeight.Should().Be(120m);
        w002.TotalWeight.Should().Be(800m);

        var w003 = items.Single(i => i.WorkOrderNo == "WO003");
        w003.PlanInputConsistency.Should().Be(2);
        w003.TotalAvailableWeight.Should().Be(100m);
        w003.ActualInputWeight.Should().Be(50m);
        w003.CutoffArrivalDate.Should().Be(DateTime.Today.AddDays(-1));
    }

    // ==================== GetInProductionInspectionDoubtItemsAsync（在产在检-错疑待料卡片） ====================

    [Fact]
    public async Task GetInProductionInspectionDoubtItemsAsync_按关注档位统计理论原料未至与到料未投()
    {
        using var ctx = CreateDbContext();
        // 重置容差快照（防其它测试污染静态状态）
        MaterialPlanToleranceProvider.Apply(0.03m);

        // ScheduleStage=3 生产执行：WO101 计划=200 现可=100 已投=100 → 缺口=100>200×3% 取值、到料未投=0（已投满）；
        //                        WO102 计划=100 现可=100 → 无缺口、到料未投=100（料到未投）
        SeedComputedSummary(ctx, "WO101", e => { e.ScheduleStage = 3; e.SemiPlanWeight = 200m; e.SemiInWeight = 100m; e.InputWeight = 100m; });
        SeedComputedSummary(ctx, "WO102", e => { e.ScheduleStage = 3; e.SemiPlanWeight = 100m; e.SemiInWeight = 100m; });
        // ScheduleStage=4 成品检验：WO201 现可=150 已投=50 → 到料未投=100；WO202 现可=80 已投=100 → 未投=0
        SeedComputedSummary(ctx, "WO201", e => { e.ScheduleStage = 4; e.SemiInWeight = 150m; e.InputWeight = 50m; });
        SeedComputedSummary(ctx, "WO202", e => { e.ScheduleStage = 4; e.SemiInWeight = 80m; e.InputWeight = 100m; });
        // ScheduleStage=1 主号完成：WO301 计划=50 现可=0 → 缺口=50；WO302 现可=40 已投=10 → 到料未投=30
        SeedComputedSummary(ctx, "WO301", e => { e.ScheduleStage = 1; e.SemiPlanWeight = 50m; });
        SeedComputedSummary(ctx, "WO302", e => { e.ScheduleStage = 1; e.SemiInWeight = 40m; e.InputWeight = 10m; });
        // ScheduleStage=2 原料锁定（不在卡片统计域）与 ScheduleStage=0 主号暂停（不在卡片统计域）：不应出现
        SeedComputedSummary(ctx, "WO400", e => { e.ScheduleStage = 2; e.SemiPlanWeight = 200m; });
        SeedComputedSummary(ctx, "WO500", e => { e.InputWeight = 50m; });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var items = await svc.GetInProductionInspectionDoubtItemsAsync();

        items.Should().HaveCount(3);
        // 展示顺序：生产执行(3) → 成品检验(4) → 主号完成(1)
        items.Select(i => i.ScheduleStage).Should().ContainInOrder(3, 4, 1);

        var s3 = items.Single(i => i.ScheduleStage == 3);
        s3.ScheduleStageText.Should().Be("生产执行");
        s3.MissingOrderCount.Should().Be(1);          // 仅 WO101（缺口 100>200×3%）
        s3.MissingWeight.Should().Be(100m);
        s3.PendingInputOrderCount.Should().Be(1);     // 仅 WO102（现可100-已投0=100；WO101 已投满=0）
        s3.PendingInputWeight.Should().Be(100m);

        var s4 = items.Single(i => i.ScheduleStage == 4);
        s4.ScheduleStageText.Should().Be("成品检验");
        s4.MissingOrderCount.Should().Be(0);          // 计划=0，无缺口
        s4.MissingWeight.Should().Be(0m);
        s4.PendingInputOrderCount.Should().Be(1);     // 仅 WO201（现可150-已投50=100）
        s4.PendingInputWeight.Should().Be(100m);

        var s1 = items.Single(i => i.ScheduleStage == 1);
        s1.ScheduleStageText.Should().Be("主号完成");
        s1.MissingOrderCount.Should().Be(1);          // WO301（计划50-现可0=50）
        s1.MissingWeight.Should().Be(50m);
        s1.PendingInputOrderCount.Should().Be(1);     // WO302（现可40-已投10=30）
        s1.PendingInputWeight.Should().Be(30m);
    }

    [Fact]
    public async Task GetInProductionInspectionDoubtItemsAsync_理论原料未至走3pct门槛()
    {
        using var ctx = CreateDbContext();
        MaterialPlanToleranceProvider.Apply(0.03m);

        // WO601：计划=100 现可=97 → 缺口=3 = 计划×3%（≤ 门槛不取值）
        SeedComputedSummary(ctx, "WO601", e => { e.ScheduleStage = 3; e.SemiPlanWeight = 100m; e.SemiInWeight = 97m; });
        // WO602：计划=100 现可=95 → 缺口=5 > 3（> 门槛取值）
        SeedComputedSummary(ctx, "WO602", e => { e.ScheduleStage = 3; e.SemiPlanWeight = 100m; e.SemiInWeight = 95m; });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var items = await svc.GetInProductionInspectionDoubtItemsAsync();

        var s3 = items.Single(i => i.ScheduleStage == 3);
        s3.MissingOrderCount.Should().Be(1);          // 仅 WO602（缺口 5 > 3）
        s3.MissingWeight.Should().Be(5m);
        s3.PendingInputOrderCount.Should().Be(2);     // 两单现可均>已投(0)
        s3.PendingInputWeight.Should().Be(97m + 95m);
    }
}
