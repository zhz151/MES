using FluentAssertions;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Data;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.WorkOrder;
using MES.Services.Order;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 投料产出总况查询服务测试：按「订单完成月」跨订单聚合投料 / 订单成品 / 余库料 / 次品 / 备料成品 / 退货，
/// 覆盖完成订单判定（订单级档位归并序）、退货双向扣减、双路径订单成品口径（含/不含非交付态）、
/// 生产类型范围口径切换、比率 null 分支与 12 个月窗口。
/// </summary>
public class OrderThroughputQueryServiceTests : TestBase
{
    private const string OrderA = "SO-A";
    private const string OrderB = "SO-B";

    private static OrderThroughputQueryService CreateService(AppDbContext ctx) => new(ctx);

    /// <summary>造一条工单执行快照行（订单月归属只取 SalesOrderNo / ScheduleStage / WarehousingEndDate）</summary>
    private static WorkOrderExecutionSummary NewSummary(string orderNo, string mainNo, int stage,
        DateTime? warehousingEndDate, int woId = 1)
        => new()
        {
            WorkOrderId = woId,
            WorkOrderNo = $"WO-{orderNo}-{woId}",
            SalesOrderNo = orderNo,
            ProductionMainNo = mainNo,
            Salesman = "测试业务员",
            CustomerName = "测试客户",
            MaterialName = "无缝管",
            DeliveryState = "Fixed",
            PlantGrade = "Q345B",
            Specification = "219*8",
            LengthStatus = "Fixed",
            SignDate = new DateTime(2026, 1, 1),
            DeliveryDate = new DateTime(2026, 12, 1),
            SettlementMethod = "电汇",
            ScheduleStage = stage,
            WarehousingEndDate = warehousingEndDate,
            LastRefreshTime = new DateTime(2026, 9, 1, 10, 0, 0),
            CreatedBy = "u1",
        };

    /// <summary>造一条生产批次（生产类型 / 投料重 / 订单归属）</summary>
    private static ProductionBatch NewProductionBatch(string batchNo, string orderNo, string? productionType,
        decimal? inputWeight)
        => new()
        {
            BatchNo = batchNo,
            ManufacturingItem = InventoryMaterialTypes.OrderFinished,
            ProductionType = productionType,
            InputWeight = inputWeight,
            WorkOrderNo = $"WO-{batchNo}",
            SalesOrderNo = orderNo,
            ProductionMainNo = "G100",
            OrderItemIds = "1",
            Salesman = "测试业务员",
            MaterialName = "无缝管",
            SettlementMethod = "电汇",
            StandardCode = "GB/T13296-2023",
            DeliveryState = "Fixed",
            LengthStatus = "Fixed",
            PlantGrade = "Q345B",
            Specification = "219*8",
            TechnicalRequirements = "无",
            CreatedBy = "u1",
        };

    /// <summary>造一条指定仓库的入库批次（订单成品 / 余库料 / 次品 / 备料成品共用）</summary>
    private static InventoryBatch NewInventoryBatch(int warehouseId, string batchNo, string materialType,
        decimal initialWeight, string? productionBatchNo = null, string? orderNo = null)
        => new()
        {
            BatchNo = batchNo,
            WarehouseId = warehouseId,
            MaterialType = materialType,
            InboundSource = "ProductionInbound",
            SourceName = "生产入库",
            PlantGrade = "Q345B",
            Specification = "219*8",
            InitialQuantity = (int)initialWeight,
            InitialWeight = initialWeight,
            RemainingQuantity = (int)initialWeight,
            RemainingWeight = initialWeight,
            InboundDate = new DateTime(2026, 1, 5),
            ProductionBatchNo = productionBatchNo,
            SalesOrderNo = orderNo,
            CreatedBy = "u1",
        };

    /// <summary>造一条退货出库记录（次品库 ReturnOut）</summary>
    private static OutboundRecord NewReturnOut(long id, int inventoryBatchId, decimal weight)
        => new()
        {
            Id = id,
            InventoryBatchId = inventoryBatchId,
            OutboundType = OutboundType.ReturnOut,
            OutboundQuantity = (int)weight,
            OutboundWeight = weight,
            OutboundDate = new DateTime(2026, 1, 10),
            CreatedTime = DateTimeOffset.Now,
            CreatedBy = "u1",
            UpdatedTime = DateTimeOffset.Now,
            UpdatedBy = "u1",
        };

    private static async Task<Warehouse> SeedWarehouseWithCodeAsync(AppDbContext ctx, string code, string name)
    {
        var wh = new Warehouse { Code = code, Name = name };
        ctx.Warehouses.Add(wh);
        await ctx.SaveChangesAsync();
        return wh;
    }

    // ===================== 完成月归属 / 订单数 =====================

    [Fact]
    public async Task GetMonthlySummaryAsync_按订单完成月聚合_订单数与投料同月归并()
    {
        var ctx = CreateDbContext();
        // SO-A 两个主号均完成 → 完成日取最大值 2026-01-20
        ctx.WorkOrderExecutionSummaries.AddRange(
            NewSummary(OrderA, "G100", stage: 1, new DateTime(2026, 1, 15), woId: 1),
            NewSummary(OrderA, "G101", stage: 1, new DateTime(2026, 1, 20), woId: 2),
            // SO-B 单主号完成 2026-02-10
            NewSummary(OrderB, "G200", stage: 1, new DateTime(2026, 2, 10), woId: 3));
        ctx.ProductionBatches.AddRange(
            NewProductionBatch("PB-A1", OrderA, ProductionTypeKeys.RoughTube, 1000m),
            NewProductionBatch("PB-A2", OrderA, ProductionTypeKeys.RoughTube, 500m),
            NewProductionBatch("PB-B1", OrderB, ProductionTypeKeys.InProcess, 2000m));
        await ctx.SaveChangesAsync();

        var result = await CreateService(ctx).GetMonthlySummaryAsync(null);

        result.Scope.Should().Be(ProductionScopeKeys.All);
        result.Months.Should().HaveCount(2);
        result.Months[0].Month.Should().Be("2026-01");
        result.Months[0].OrderCount.Should().Be(1);
        result.Months[0].InputWeight.Should().Be(1500m);
        result.Months[1].Month.Should().Be("2026-02");
        result.Months[1].OrderCount.Should().Be(1);
        result.Months[1].InputWeight.Should().Be(2000m);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_订单级档位归并序_任一行非完成档即整单不计入()
    {
        var ctx = CreateDbContext();
        // SO-A 两个主号：一个完成、一个仍在生产（档 3）→ 归并后档位=3，整单不计入
        ctx.WorkOrderExecutionSummaries.AddRange(
            NewSummary(OrderA, "G100", stage: 1, new DateTime(2026, 1, 15), woId: 1),
            NewSummary(OrderA, "G101", stage: 3, null, woId: 2));
        ctx.ProductionBatches.Add(NewProductionBatch("PB-A1", OrderA, ProductionTypeKeys.RoughTube, 1000m));
        await ctx.SaveChangesAsync();

        var result = await CreateService(ctx).GetMonthlySummaryAsync(null);

        result.Months.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_窗口外完成月与无完成月隐藏()
    {
        var ctx = CreateDbContext();
        var windowStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
        // SO-A 完成日早于 12 个月窗口 → 隐藏
        ctx.WorkOrderExecutionSummaries.Add(NewSummary(OrderA, "G100", stage: 1, windowStart.AddDays(-1)));
        // SO-B 未完成（档 4 成检）→ 不产生行
        ctx.WorkOrderExecutionSummaries.Add(NewSummary(OrderB, "G200", stage: 4, null, woId: 2));
        ctx.ProductionBatches.AddRange(
            NewProductionBatch("PB-A1", OrderA, ProductionTypeKeys.RoughTube, 1000m),
            NewProductionBatch("PB-B1", OrderB, ProductionTypeKeys.RoughTube, 800m));
        await ctx.SaveChangesAsync();

        var result = await CreateService(ctx).GetMonthlySummaryAsync(null);

        result.Months.Should().BeEmpty();
    }

    // ===================== 完成日期区间（V2.0：替代原「完成月模糊搜索」） =====================

    [Fact]
    public async Task GetMonthlySummaryAsync_日期区间_整区间聚合为单行且按真实完成日纳入()
    {
        var ctx = CreateDbContext();
        // 三个完成日：2026-03-20 / 2026-04-02 / 2026-06-28，均落入 [2026-03-15, 2026-06-20]
        ctx.WorkOrderExecutionSummaries.AddRange(
            NewSummary(OrderA, "G100", stage: 1, new DateTime(2026, 3, 20), woId: 1),
            NewSummary(OrderB, "G200", stage: 1, new DateTime(2026, 4, 2), woId: 2),
            NewSummary("SO-C", "G300", stage: 1, new DateTime(2026, 6, 28), woId: 3));
        ctx.ProductionBatches.AddRange(
            NewProductionBatch("PB-A1", OrderA, ProductionTypeKeys.RoughTube, 100m),
            NewProductionBatch("PB-B1", OrderB, ProductionTypeKeys.RoughTube, 200m),
            NewProductionBatch("PB-C1", "SO-C", ProductionTypeKeys.RoughTube, 300m));
        await ctx.SaveChangesAsync();

        var result = await CreateService(ctx).GetMonthlySummaryAsync(
            null, new DateTime(2026, 3, 15), new DateTime(2026, 6, 20));

        // 区间模式只返回一行：跨 2026-03 + 2026-04 两个完成月聚合（2026-06-28 > 止日 → 剔除），首列 = 所选范围文本
        var row = result.Months.Should().ContainSingle().Subject;
        row.Month.Should().Be("2026-03-15 - 2026-06-20");
        row.OrderCount.Should().Be(2);            // SO-A + SO-B
        row.InputWeight.Should().Be(300m);        // 100 + 200（SO-C 完成日 06-28 超出止日）
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_日期区间_边界按真实完成日直比含起止当日()
    {
        var ctx = CreateDbContext();
        // 边界四例：起日前一天 / 起日当天（含）/ 止日当天（含）/ 止日后一天
        ctx.WorkOrderExecutionSummaries.AddRange(
            NewSummary("SO-BEFORE", "G100", stage: 1, new DateTime(2026, 3, 14), woId: 1),
            NewSummary("SO-ON-FROM", "G200", stage: 1, new DateTime(2026, 3, 15), woId: 2),
            NewSummary("SO-ON-TO", "G300", stage: 1, new DateTime(2026, 4, 10), woId: 3),
            NewSummary("SO-AFTER", "G400", stage: 1, new DateTime(2026, 4, 11), woId: 4));
        ctx.ProductionBatches.AddRange(
            NewProductionBatch("PB-1", "SO-BEFORE", ProductionTypeKeys.RoughTube, 1m),
            NewProductionBatch("PB-2", "SO-ON-FROM", ProductionTypeKeys.RoughTube, 10m),
            NewProductionBatch("PB-3", "SO-ON-TO", ProductionTypeKeys.RoughTube, 100m),
            NewProductionBatch("PB-4", "SO-AFTER", ProductionTypeKeys.RoughTube, 1000m));
        await ctx.SaveChangesAsync();

        // 修正前按「完成月月首」锚定：起日 03-15 → 3 月整月被剔除（月首 03-01 < 03-15），止日 04-10 → 4 月整月被纳入，
        // 即只得 SO-ON-TO + SO-AFTER（含 04-11 这一越界订单）。修正后按真实完成日直比，只留两端闭区间内的 2 单。
        var row = (await CreateService(ctx).GetMonthlySummaryAsync(
            null, new DateTime(2026, 3, 15), new DateTime(2026, 4, 10)))
            .Months.Should().ContainSingle().Subject;

        row.OrderCount.Should().Be(2);            // 起日当天 + 止日当天
        row.InputWeight.Should().Be(110m);        // 10 + 100
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_默认窗口_仍按完成月分行()
    {
        var ctx = CreateDbContext();
        var thisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 5);
        var lastMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1).AddDays(4);
        ctx.WorkOrderExecutionSummaries.AddRange(
            NewSummary(OrderA, "G100", stage: 1, lastMonth, woId: 1),
            NewSummary(OrderB, "G200", stage: 1, thisMonth, woId: 2));
        ctx.ProductionBatches.AddRange(
            NewProductionBatch("PB-A1", OrderA, ProductionTypeKeys.RoughTube, 100m),
            NewProductionBatch("PB-B1", OrderB, ProductionTypeKeys.RoughTube, 200m));
        await ctx.SaveChangesAsync();

        var result = await CreateService(ctx).GetMonthlySummaryAsync(null);

        result.Months.Should().HaveCount(2);
        result.Months[0].Month.Should().Be(lastMonth.ToString("yyyy-MM"));
        result.Months[1].Month.Should().Be(thisMonth.ToString("yyyy-MM"));
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_日期区间_可查默认12个月窗口之外的历史完成月()
    {
        var ctx = CreateDbContext();
        // 完成日远早于默认 12 个月窗口（3 年前）
        var oldDone = new DateTime(DateTime.Today.Year - 3, 5, 12);
        ctx.WorkOrderExecutionSummaries.Add(NewSummary(OrderA, "G100", stage: 1, oldDone));
        ctx.ProductionBatches.Add(NewProductionBatch("PB-A1", OrderA, ProductionTypeKeys.RoughTube, 700m));
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);

        // 默认窗口（两端皆空）→ 该月隐藏
        (await service.GetMonthlySummaryAsync(null)).Months.Should().BeEmpty();

        // 显式指定该历史区间 → 命中（聚合单行，首列为所选范围文本）
        var ranged = await service.GetMonthlySummaryAsync(
            null, new DateTime(oldDone.Year, 5, 1), new DateTime(oldDone.Year, 5, 31));
        var row = ranged.Months.Should().ContainSingle().Subject;
        row.Month.Should().Be($"{oldDone.Year}-05-01 - {oldDone.Year}-05-31");
        row.InputWeight.Should().Be(700m);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_日期区间_单端为空视为开放边界()
    {
        var ctx = CreateDbContext();
        ctx.WorkOrderExecutionSummaries.AddRange(
            NewSummary(OrderA, "G100", stage: 1, new DateTime(2020, 1, 10), woId: 1),
            NewSummary(OrderB, "G200", stage: 1, new DateTime(2030, 1, 10), woId: 2));
        ctx.ProductionBatches.AddRange(
            NewProductionBatch("PB-A1", OrderA, ProductionTypeKeys.RoughTube, 100m),
            NewProductionBatch("PB-B1", OrderB, ProductionTypeKeys.RoughTube, 200m));
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);

        // 仅给止日 → 起点开放：2020-01-10 命中，2030-01-10（完成日 > 止日）排除
        var toOnly = (await service.GetMonthlySummaryAsync(null, null, new DateTime(2025, 12, 31)))
            .Months.Should().ContainSingle().Subject;
        toOnly.Month.Should().Be("不限 - 2025-12-31");
        toOnly.InputWeight.Should().Be(100m);

        // 仅给起日 → 终点开放：2030-01 命中，2020-01 排除
        var fromOnly = (await service.GetMonthlySummaryAsync(null, new DateTime(2025, 1, 1), null))
            .Months.Should().ContainSingle().Subject;
        fromOnly.Month.Should().Be("2025-01-01 - 不限");
        fromOnly.InputWeight.Should().Be(200m);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_日期区间_起晚于止返回空行()
    {
        var ctx = CreateDbContext();
        ctx.WorkOrderExecutionSummaries.Add(NewSummary(OrderA, "G100", stage: 1, new DateTime(2026, 3, 5)));
        ctx.ProductionBatches.Add(NewProductionBatch("PB-A1", OrderA, ProductionTypeKeys.RoughTube, 100m));
        await ctx.SaveChangesAsync();

        var result = await CreateService(ctx).GetMonthlySummaryAsync(
            null, new DateTime(2026, 6, 1), new DateTime(2026, 3, 31));

        result.Months.Should().BeEmpty();
    }

    // ===================== 退货双向扣减 =====================

    [Fact]
    public async Task GetMonthlySummaryAsync_退货_分别从生产投料与次品入库扣减且单列供核对()
    {
        var ctx = CreateDbContext();
        var defectWh = await SeedWarehouseWithCodeAsync(ctx, WarehouseCodes.Defect, "次品库");
        ctx.WorkOrderExecutionSummaries.Add(NewSummary(OrderA, "G100", stage: 1, new DateTime(2026, 3, 5)));
        ctx.ProductionBatches.Add(NewProductionBatch("PB-X", OrderA, ProductionTypeKeys.RoughTube, 1000m));
        var defectBatch = NewInventoryBatch(defectWh.Id, "IB-D1", InventoryMaterialTypes.DefectFinished,
            200m, productionBatchNo: "PB-X");
        ctx.InventoryBatches.Add(defectBatch);
        await ctx.SaveChangesAsync();

        ctx.OutboundRecords.Add(NewReturnOut(1L, defectBatch.Id, 50m));
        await ctx.SaveChangesAsync();

        var result = await CreateService(ctx).GetMonthlySummaryAsync(null);

        var row = result.Months.Should().ContainSingle().Subject;
        row.Month.Should().Be("2026-03");
        row.InputWeight.Should().Be(950m);      // 1000 - 50
        row.DefectWeight.Should().Be(150m);     // 200 - 50
        row.ReturnWeight.Should().Be(50m);      // 单列核对值
        row.SurplusWeight.Should().Be(0m);
        row.PreparedWeight.Should().Be(0m);
        // 总产出 = 150 → 投料产出率 150/950、产出成品比 0/150
        row.InputOutputRate.Should().Be(150m / 950m);
        row.FinishedOutputRate.Should().Be(0m);
    }

    // ===================== 订单成品双路径（交付态 / 非交付态） =====================

    [Fact]
    public async Task GetMonthlySummaryAsync_全部口径_订单成品只计交付态且排除非交付态()
    {
        var ctx = CreateDbContext();
        var fgWh = await SeedWarehouseWithCodeAsync(ctx, WarehouseCodes.FinishedGoods, "成品库");
        ctx.WorkOrderExecutionSummaries.Add(NewSummary(OrderA, "G100", stage: 1, new DateTime(2026, 4, 1)));
        ctx.ProductionBatches.Add(NewProductionBatch("PB-U", OrderA, ProductionTypeKeys.InProcess, 500m));
        ctx.InventoryBatches.AddRange(
            // 交付态：带订单号 → 「全部」口径直取命中
            NewInventoryBatch(fgWh.Id, "IB-F1", InventoryMaterialTypes.OrderFinished, 200m,
                productionBatchNo: "PB-U", orderNo: OrderA),
            // 非交付态（U 型管厂内自产）：不带订单号 → 「全部」口径不计
            NewInventoryBatch(fgWh.Id, "IB-F2", InventoryMaterialTypes.SpecialDeliveryStatus, 300m,
                productionBatchNo: "PB-U"));
        await ctx.SaveChangesAsync();

        var result = await CreateService(ctx).GetMonthlySummaryAsync(ProductionScopeKeys.All);

        var row = result.Months.Should().ContainSingle().Subject;
        row.OrderFinishedWeight.Should().Be(200m);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_非全部口径_订单成品含非交付态_U型管自产只到非交付态()
    {
        var ctx = CreateDbContext();
        var fgWh = await SeedWarehouseWithCodeAsync(ctx, WarehouseCodes.FinishedGoods, "成品库");
        ctx.WorkOrderExecutionSummaries.Add(NewSummary(OrderA, "G100", stage: 1, new DateTime(2026, 4, 1)));
        ctx.ProductionBatches.Add(NewProductionBatch("PB-U", OrderA, ProductionTypeKeys.InProcess, 500m));
        ctx.InventoryBatches.AddRange(
            NewInventoryBatch(fgWh.Id, "IB-F1", InventoryMaterialTypes.OrderFinished, 200m,
                productionBatchNo: "PB-U", orderNo: OrderA),
            NewInventoryBatch(fgWh.Id, "IB-F2", InventoryMaterialTypes.SpecialDeliveryStatus, 300m,
                productionBatchNo: "PB-U"));
        await ctx.SaveChangesAsync();

        var result = await CreateService(ctx).GetMonthlySummaryAsync(ProductionScopeKeys.Pure);

        var row = result.Months.Should().ContainSingle().Subject;
        row.OrderFinishedWeight.Should().Be(500m);   // 200 交付态 + 300 非交付态
    }

    // ===================== 余库料 / 备料成品（经生产批号反查） =====================

    [Fact]
    public async Task GetMonthlySummaryAsync_余库料与备料成品_经生产批号反查归属订单()
    {
        var ctx = CreateDbContext();
        var wipWh = await SeedWarehouseWithCodeAsync(ctx, WarehouseCodes.WorkInProgress, "在制库");
        var fgWh = await SeedWarehouseWithCodeAsync(ctx, WarehouseCodes.FinishedGoods, "成品库");
        ctx.WorkOrderExecutionSummaries.Add(NewSummary(OrderA, "G100", stage: 1, new DateTime(2026, 5, 8)));
        ctx.ProductionBatches.Add(NewProductionBatch("PB-R", OrderA, ProductionTypeKeys.RoughTube, 1000m));
        ctx.InventoryBatches.AddRange(
            NewInventoryBatch(wipWh.Id, "IB-S1", InventoryMaterialTypes.Surplus, 120m, productionBatchNo: "PB-R"),
            NewInventoryBatch(fgWh.Id, "IB-P1", InventoryMaterialTypes.Finished, 80m, productionBatchNo: "PB-R"));
        await ctx.SaveChangesAsync();

        var result = await CreateService(ctx).GetMonthlySummaryAsync(null);

        var row = result.Months.Should().ContainSingle().Subject;
        row.SurplusWeight.Should().Be(120m);
        row.PreparedWeight.Should().Be(80m);
        // 总产出 = 0 + 120 + 0 + 80 = 200
        row.InputOutputRate.Should().Be(200m / 1000m);
        row.FinishedOutputRate.Should().Be(0m);
    }

    // ===================== 生产类型范围口径 =====================

    [Fact]
    public async Task GetMonthlySummaryAsync_口径切换_合计档与单一生产类型档分别取数()
    {
        var ctx = CreateDbContext();
        ctx.WorkOrderExecutionSummaries.Add(NewSummary(OrderA, "G100", stage: 1, new DateTime(2026, 6, 1)));
        ctx.ProductionBatches.AddRange(
            NewProductionBatch("PB-S1", OrderA, ProductionTypeKeys.RoughTube, 100m),
            NewProductionBatch("PB-S2", OrderA, ProductionTypeKeys.OutsourcedPurchased, 900m),
            // 返整（Rework）不在报表口径内，任何档都不计
            NewProductionBatch("PB-S3", OrderA, ProductionTypeKeys.Rework, 777m));
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);

        (await service.GetMonthlySummaryAsync(null)).Months.Single().InputWeight.Should().Be(1000m);
        (await service.GetMonthlySummaryAsync("UnknownKey")).Months.Single().InputWeight.Should().Be(1000m);
        (await service.GetMonthlySummaryAsync(ProductionScopeKeys.All)).Months.Single().InputWeight.Should().Be(1000m);
        (await service.GetMonthlySummaryAsync(ProductionScopeKeys.Pure)).Months.Single().InputWeight.Should().Be(100m);
        (await service.GetMonthlySummaryAsync(ProductionTypeKeys.RoughTube)).Months.Single().InputWeight.Should().Be(100m);
        (await service.GetMonthlySummaryAsync(ProductionTypeKeys.OutsourcedPurchased)).Months.Single().InputWeight.Should().Be(900m);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_未知口径_归一为全部并回带回口径Key()
    {
        var ctx = CreateDbContext();
        ctx.WorkOrderExecutionSummaries.Add(NewSummary(OrderA, "G100", stage: 1, new DateTime(2026, 6, 1)));
        ctx.ProductionBatches.Add(NewProductionBatch("PB-S1", OrderA, ProductionTypeKeys.RoughTube, 100m));
        await ctx.SaveChangesAsync();

        var result = await CreateService(ctx).GetMonthlySummaryAsync("NotAValidScope");

        result.Scope.Should().Be(ProductionScopeKeys.All);
    }

    // ===================== 订单数（本口径下相关订单数） =====================

    [Fact]
    public async Task GetMonthlySummaryAsync_单一类型口径_订单数只计该类型有批次的订单()
    {
        var ctx = CreateDbContext();
        // 同一完成月（2026-06）两个订单：SO-A 有荒管批次，SO-B 只有外购批次
        ctx.WorkOrderExecutionSummaries.AddRange(
            NewSummary(OrderA, "G100", stage: 1, new DateTime(2026, 6, 3), woId: 1),
            NewSummary(OrderB, "G200", stage: 1, new DateTime(2026, 6, 8), woId: 2));
        ctx.ProductionBatches.AddRange(
            NewProductionBatch("PB-A1", OrderA, ProductionTypeKeys.RoughTube, 100m),
            NewProductionBatch("PB-B1", OrderB, ProductionTypeKeys.OutsourcedPurchased, 900m));
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);

        // 「全部」口径：两单都相关
        (await service.GetMonthlySummaryAsync(null)).Months.Single().OrderCount.Should().Be(2);

        // 仅荒管口径：只有 SO-A 相关（SO-B 的外购批次不在范围内）
        var rough = (await service.GetMonthlySummaryAsync(ProductionTypeKeys.RoughTube)).Months.Single();
        rough.OrderCount.Should().Be(1);
        rough.InputWeight.Should().Be(100m);

        // 仅外购口径：只有 SO-B 相关
        (await service.GetMonthlySummaryAsync(ProductionTypeKeys.OutsourcedPurchased))
            .Months.Single().OrderCount.Should().Be(1);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_该口径下无相关订单_该月整行隐藏()
    {
        var ctx = CreateDbContext();
        // SO-A 完成月有荒管批次；SO-B 完成月只有返整批次（返整不在任何报表口径内）
        ctx.WorkOrderExecutionSummaries.AddRange(
            NewSummary(OrderA, "G100", stage: 1, new DateTime(2026, 6, 3), woId: 1),
            NewSummary(OrderB, "G200", stage: 1, new DateTime(2026, 7, 8), woId: 2));
        ctx.ProductionBatches.AddRange(
            NewProductionBatch("PB-A1", OrderA, ProductionTypeKeys.RoughTube, 100m),
            NewProductionBatch("PB-B1", OrderB, ProductionTypeKeys.Rework, 500m));
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);

        // 仅荒管口径：2026-07 无相关订单 → 整行不显示
        var rough = (await service.GetMonthlySummaryAsync(ProductionTypeKeys.RoughTube)).Months;
        rough.Should().ContainSingle();
        rough[0].Month.Should().Be("2026-06");
        rough[0].OrderCount.Should().Be(1);

        // 「全部（四种）」口径同样不含返整 → 2026-07 整行不显示
        (await service.GetMonthlySummaryAsync(null)).Months.Should().ContainSingle()
            .Which.Month.Should().Be("2026-06");
    }

    // ===================== 比率 null 分支 =====================

    [Fact]
    public async Task GetMonthlySummaryAsync_分母为零_比率返回null不渲染()
    {
        var ctx = CreateDbContext();
        ctx.WorkOrderExecutionSummaries.Add(NewSummary(OrderA, "G100", stage: 1, new DateTime(2026, 7, 1)));
        // 投料重为空 → 生产投料净量 0，且无任何产出 → 两比率均为 null
        ctx.ProductionBatches.Add(NewProductionBatch("PB-N", OrderA, ProductionTypeKeys.RoughTube, null));
        await ctx.SaveChangesAsync();

        var result = await CreateService(ctx).GetMonthlySummaryAsync(null);

        var row = result.Months.Should().ContainSingle().Subject;
        row.InputWeight.Should().Be(0m);
        row.InputOutputRate.Should().BeNull();
        row.FinishedOutputRate.Should().BeNull();
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_无完成订单_直接返回空行为不查询明细()
    {
        var ctx = CreateDbContext();
        ctx.WorkOrderExecutionSummaries.Add(NewSummary(OrderA, "G100", stage: 0, null));
        await ctx.SaveChangesAsync();

        var result = await CreateService(ctx).GetMonthlySummaryAsync(ProductionScopeKeys.All);

        result.Months.Should().BeEmpty();
        result.Scope.Should().Be(ProductionScopeKeys.All);
    }
}
