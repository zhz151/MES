using FluentAssertions;
using Moq;
using MES.Core.Constants;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.Scheduling;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Core.Interfaces.Scheduling;
using MES.Data;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.WorkOrder;
using MES.Services.Order;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 订单进度树查询服务测试：主号聚合(WorkOrderExecutionSummary 快照) + 成品检验(看板前3档按批去重) + 成品入库(实时)。
/// </summary>
public class OrderProgressQueryServiceTests : TestBase
{
    private const string SO = "SO001";

    private static OrderProgressQueryService CreateService(AppDbContext ctx, IFinalInspectionPlanService kanban)
        => new(ctx, kanban);

    /// <summary>看板恒空 Mock（多数测试不关心成品检验分支）</summary>
    private static IFinalInspectionPlanService EmptyKanban()
    {
        var mock = new Mock<IFinalInspectionPlanService>();
        mock.Setup(s => s.GetKanbanAsync()).ReturnsAsync(new List<FinalInspectionPlanDto>());
        return mock.Object;
    }

    /// <summary>造一条指定看板档位的成检计划行</summary>
    private static FinalInspectionPlanDto NewKanbanRow(int batchId, string mainNo, string stage, decimal weight)
        => new()
        {
            ProductionBatchId = batchId,
            BatchNo = $"B{batchId}",
            SalesOrderNo = SO,
            ProductionMainNo = mainNo,
            WorkOrderNo = $"WO-{batchId}",
            KanbanStage = stage,
            ProductionWeight = weight,
        };

    /// <summary>造一条工单执行快照行（必填字符串默认值齐全）</summary>
    private static WorkOrderExecutionSummary NewSummary(int woId, string mainNo, int stage)
        => new()
        {
            WorkOrderId = woId,
            WorkOrderNo = $"WO-{woId:000}",
            SalesOrderNo = SO,
            ProductionMainNo = mainNo,
            Salesman = "测试业务员",
            CustomerName = "测试客户",
            MaterialName = "无缝管",
            DeliveryState = "Fixed",
            PlantGrade = "Q345B",
            Specification = "219*8",
            LengthStatus = "Fixed",
            SignDate = new DateTime(2026, 8, 1),
            DeliveryDate = new DateTime(2026, 12, 1),
            SettlementMethod = "电汇",
            ScheduleStage = stage,
            LastRefreshTime = new DateTime(2026, 9, 1, 10, 0, 0),
            CreatedBy = "u1",
        };

    /// <summary>造一条订单成品库存批次（OrderFinished，成品库）</summary>
    private static InventoryBatch NewOrderFinishedBatch(int warehouseId, string batchNo, string? mainNo,
        decimal initialWeight, decimal remainingWeight)
        => new()
        {
            BatchNo = batchNo,
            WarehouseId = warehouseId,
            MaterialType = InventoryMaterialTypes.OrderFinished,
            InboundSource = "Purchase",
            SourceName = "供应商A",
            PlantGrade = "Q345B",
            Specification = "219*8",
            InitialQuantity = (int)initialWeight,
            InitialWeight = initialWeight,
            RemainingQuantity = (int)remainingWeight,
            RemainingWeight = remainingWeight,
            InboundDate = new DateTime(2026, 9, 1),
            SalesOrderNo = SO,
            ProductionMainNo = mainNo,
            CreatedBy = "u1",
        };

    /// <summary>造一个指定仓库代码的仓库（SeedWarehouseAsync 固定 WH001，此处需 WIP/FG）</summary>
    private static async Task<Warehouse> SeedWarehouseWithCodeAsync(AppDbContext ctx, string code, string name)
    {
        var wh = new Warehouse { Code = code, Name = name };
        ctx.Warehouses.Add(wh);
        await ctx.SaveChangesAsync();
        return wh;
    }

    /// <summary>造一条生产批次（既是「生产批号 → 订单+主号」反查桥，也是完结主号「投料」的工艺卡取数源）</summary>
    private static ProductionBatch NewProductionBatch(string batchNo, string mainNo,
        string manufacturingItem = InventoryMaterialTypes.OrderFinished, string? productionType = null,
        decimal? inputWeight = null)
        => new()
        {
            BatchNo = batchNo,
            ManufacturingItem = manufacturingItem,
            ProductionType = productionType,
            InputWeight = inputWeight,
            WorkOrderNo = $"WO-{batchNo}",
            SalesOrderNo = SO,
            ProductionMainNo = mainNo,
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

    /// <summary>造一条工艺卡工序组（生产执行实时重算的工段判定数据源）</summary>
    private static ProcessGroup Pg(string processName, int seq,
        int? coldRollDraw = null, int? outerPolish = null, int? straighten = null)
        => new()
        {
            ProcessName = processName,
            SequenceNumber = seq,
            ColdRollDraw = coldRollDraw,
            OuterPolish = outerPolish,
            Straighten = straighten,
        };

    /// <summary>
    /// 造一条「生产执行实时重算」用生产批次：状态在产、带工序组。
    /// ⚠️ 工序组必须经批次导航集合挂载（EF InMemory 才会 fixup），否则 Include 后恒空 → 待量恒 0。
    /// </summary>
    private static ProductionBatch NewPendingBatch(string batchNo, string workOrderNo, string mainNo,
        decimal? validWeight, string? currentGroup, string? currentSection, bool? sectionCompleted,
        params ProcessGroup[] groups)
        => new()
        {
            BatchNo = batchNo,
            Status = BatchStatus.InProgress,
            CurrentValidWeight = validWeight == null ? null : (int)validWeight.Value,
            CurrentGroupName = currentGroup,
            CurrentSectionName = currentSection,
            CurrentSectionCompleted = sectionCompleted,
            ManufacturingItem = InventoryMaterialTypes.OrderFinished,
            WorkOrderNo = workOrderNo,
            SalesOrderNo = SO,
            ProductionMainNo = mainNo,
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
            ProcessGroups = groups.ToList(),
        };

    /// <summary>造一条指定仓库的入库批次（余料/备料成品共用；ProductionBatchNo 为反查订单的唯一桥）</summary>
    private static InventoryBatch NewInboundBatch(int warehouseId, string batchNo, string materialType,
        string? productionBatchNo, decimal initialWeight)
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
            InboundDate = new DateTime(2026, 9, 1),
            ProductionBatchNo = productionBatchNo,
            CreatedBy = "u1",
        };

    /// <summary>造一条销售出库记录</summary>
    private static OutboundRecord NewSalesOut(long id, int inventoryBatchId, decimal weight)
        => NewOutbound(id, inventoryBatchId, OutboundType.SalesOut, weight);

    /// <summary>造一条退货出库记录（次品库「先入库、后退货出库」的下半程）</summary>
    private static OutboundRecord NewReturnOut(long id, int inventoryBatchId, decimal weight)
        => NewOutbound(id, inventoryBatchId, OutboundType.ReturnOut, weight);

    private static OutboundRecord NewOutbound(long id, int inventoryBatchId, OutboundType type, decimal weight)
        => new()
        {
            Id = id,
            InventoryBatchId = inventoryBatchId,
            OutboundType = type,
            OutboundQuantity = (int)weight,
            OutboundWeight = weight,
            OutboundDate = new DateTime(2026, 9, 2),
            CreatedTime = DateTimeOffset.Now,
            CreatedBy = "u1",
            UpdatedTime = DateTimeOffset.Now,
            UpdatedBy = "u1",
        };

    private static MainProgressLeafDto? LeafOf(MainProgressBranchDto? branch, string key)
        => branch?.Leaves.FirstOrDefault(l => l.Key == key);

    private static async Task WithToleranceAsync(decimal tolerance, Func<Task> action)
    {
        var original = MaterialPlanToleranceProvider.InputConsistencyTolerance;
        MaterialPlanToleranceProvider.Apply(tolerance);
        try { await action(); }
        finally { MaterialPlanToleranceProvider.Apply(original); }
    }

    // ===================== 主号聚合：生产8节点（实时）/ 总重 / 紧急 =====================

    [Fact]
    public async Task GetTreeAsync_两工单一主号_生产8节点实时重算且总重按SUM聚合取最急紧急()
    {
        var ctx = CreateDbContext();
        var r1 = NewSummary(1, "G100", stage: 3);
        r1.TotalWeight = 1000m;
        r1.PendingSection60Roll = 999m; // 快照旧值：应被实时口径忽略
        r1.PendingSection50Roll = 999m;
        r1.UrgencyLevel = UrgencyLevelKeys.BOrder;
        r1.LastRefreshTime = new DateTime(2026, 9, 1, 10, 0, 0);

        var r2 = NewSummary(2, "G100", stage: 3);
        r2.TotalWeight = 2000m;
        r2.PendingSection60Roll = 999m;
        r2.PendingSection50Roll = 999m;
        r2.UrgencyLevel = UrgencyLevelKeys.APlusUrgent;
        r2.LastRefreshTime = new DateTime(2026, 9, 1, 11, 0, 0);

        // 生产执行改为实时重算：数据源是该订单各工单下的生产批次（同主号并集一次算完）
        ctx.Set<WorkOrderExecutionSummary>().AddRange(r1, r2);
        ctx.ProductionBatches.AddRange(
            NewPendingBatch("2609-A1", "WO-001", "G100", 100m, ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, false,
                Pg(ProcessKeys.ColdRoll60, 1, coldRollDraw: 1)),
            NewPendingBatch("2609-A2", "WO-002", "G100", 150m, ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, false,
                Pg(ProcessKeys.ColdRoll60, 1, coldRollDraw: 1)),
            NewPendingBatch("2609-B1", "WO-002", "G100", 200m, ProcessKeys.ColdRoll50, SectionKeys.ColdRollDraw, false,
                Pg(ProcessKeys.ColdRoll50, 1, coldRollDraw: 1)));
        await ctx.SaveChangesAsync();

        var tree = await CreateService(ctx, EmptyKanban()).GetTreeAsync(SO);

        tree.Should().NotBeNull();
        tree!.SalesOrderNo.Should().Be(SO);
        tree.CustomerName.Should().Be("测试客户");
        tree.Salesman.Should().Be("测试业务员");

        var main = tree.MainNos.Should().ContainSingle().Subject;
        main.ProductionMainNo.Should().Be("G100");
        main.ScheduleStage.Should().Be(3);
        main.IsCompleted.Should().BeFalse();
        main.TotalWeightKg.Should().Be(3000m); // 组内 SUM
        main.UrgencyLevel.Should().Be(UrgencyLevelKeys.APlusUrgent); // 取最紧急

        main.RawMaterialLock.Should().BeNull();
        main.FinalInspection.Should().BeNull();
        main.Warehousing.Should().BeNull();
        main.Production.Should().NotBeNull();
        main.Production!.Leaves.Should().HaveCount(2);
        LeafOf(main.Production, "ColdRoll60")!.WeightKg.Should().Be(250m); // 实时：100+150（快照 999 不被采用）
        LeafOf(main.Production, "ColdRoll50")!.WeightKg.Should().Be(200m); // 实时：200（快照 999 不被采用）
    }

    [Fact]
    public async Task GetTreeAsync_生产节点_快照有值但无即时批次_不渲染分支()
    {
        var ctx = CreateDbContext();
        var r = NewSummary(1, "G100", stage: 3);
        r.PendingSection60Roll = 500m; // 快照旧值：实时口径下无批次 → 不建叶、不建分支
        ctx.Set<WorkOrderExecutionSummary>().Add(r);
        await ctx.SaveChangesAsync();

        var tree = await CreateService(ctx, EmptyKanban()).GetTreeAsync(SO);

        tree!.MainNos.Should().ContainSingle().Subject.Production.Should().BeNull();
    }

    [Fact]
    public async Task GetTreeAsync_生产节点_带在产在途两段名单且合计等于叶重()
    {
        var ctx = CreateDbContext();
        ctx.Set<WorkOrderExecutionSummary>().Add(NewSummary(1, "G100", stage: 3));
        // 三批同挂 荒管处理(seq1，整直=3、外抛光=5) + 60冷轧(seq2，冷轧拔=1) 两个工序组：
        //   B1/B2 当前仍在荒管处理（无当前工段 / 在整直）→ 荒管处理 在产；60冷轧 在途（尚未做到）
        //   B3 已到 60冷轧·冷轧拔未完成 → 荒管处理已越过不计；60冷轧 在产
        ctx.ProductionBatches.AddRange(
            NewPendingBatch("2609-001", "WO-001", "G100", 100m, ProcessKeys.RoughTubeProcessing, null, null,
                Pg(ProcessKeys.RoughTubeProcessing, 1, outerPolish: 5, straighten: 3),
                Pg(ProcessKeys.ColdRoll60, 2, coldRollDraw: 1)),
            NewPendingBatch("2609-002", "WO-001", "G100", 200m, ProcessKeys.RoughTubeProcessing, SectionKeys.Straighten, null,
                Pg(ProcessKeys.RoughTubeProcessing, 1, outerPolish: 5, straighten: 3),
                Pg(ProcessKeys.ColdRoll60, 2, coldRollDraw: 1)),
            NewPendingBatch("2609-003", "WO-001", "G100", 300m, ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, false,
                Pg(ProcessKeys.RoughTubeProcessing, 1, outerPolish: 5, straighten: 3),
                Pg(ProcessKeys.ColdRoll60, 2, coldRollDraw: 1)));
        await ctx.SaveChangesAsync();

        var tree = await CreateService(ctx, EmptyKanban()).GetTreeAsync(SO);
        var production = tree!.MainNos[0].Production!;

        // 荒管处理：只有「在产」一段（B3 已越过 → 不计）
        var rough = LeafOf(production, "RoughTubeProcessing")!;
        rough.WeightKg.Should().Be(300m); // 100+200
        var roughSeg = rough.BatchSegments.Should().ContainSingle().Subject;
        roughSeg.Key.Should().Be("InProgress");
        roughSeg.Label.Should().Be("在产");
        roughSeg.Batches.Select(b => b.BatchNo).Should().Equal("2609-001", "2609-002");

        // 60冷轧：两段齐全（在产 B3 / 在途 B1+B2）
        var roll60 = LeafOf(production, "ColdRoll60")!;
        roll60.WeightKg.Should().Be(600m); // 100+200+300
        roll60.BatchSegments.Select(s => s.Key).Should().Equal("InProgress", "InTransit");
        roll60.BatchSegments[0].Label.Should().Be("在产");
        roll60.BatchSegments[0].WeightKg.Should().Be(300m);
        roll60.BatchSegments[0].Batches.Select(b => b.BatchNo).Should().Equal("2609-003");
        roll60.BatchSegments[1].Label.Should().Be("在途");
        roll60.BatchSegments[1].WeightKg.Should().Be(300m);
        roll60.BatchSegments[1].Batches.Select(b => b.BatchNo).Should().Equal("2609-001", "2609-002");

        roll60.BatchSegments.Sum(s => s.WeightKg).Should().Be(roll60.WeightKg); // 分段不增不减
        roll60.BatchSegments[1].BatchCount.Should().Be(roll60.BatchSegments[1].Batches.Count);
    }

    [Fact]
    public async Task GetTreeAsync_生产节点_仅在产无在途时只出一段()
    {
        var ctx = CreateDbContext();
        ctx.Set<WorkOrderExecutionSummary>().Add(NewSummary(1, "G100", stage: 3));
        ctx.ProductionBatches.Add(NewPendingBatch("2609-001", "WO-001", "G100", 100m,
            ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, false,
            Pg(ProcessKeys.ColdRoll60, 1, coldRollDraw: 1)));
        await ctx.SaveChangesAsync();

        var tree = await CreateService(ctx, EmptyKanban()).GetTreeAsync(SO);

        var leaf = LeafOf(tree!.MainNos[0].Production, "ColdRoll60")!;
        leaf.BatchSegments.Should().ContainSingle().Which.Key.Should().Be("InProgress");
    }

    [Fact]
    public async Task GetTreeAsync_生产节点_只取本订单工单范围批次()
    {
        var ctx = CreateDbContext();
        ctx.Set<WorkOrderExecutionSummary>().Add(NewSummary(1, "G100", stage: 3));
        // 同订单同主号，但挂在不属于本订单的工单（不在快照行范围内）→ 不计
        var other = NewPendingBatch("2609-900", "WO-999", "G100", 777m,
            ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, false,
            Pg(ProcessKeys.ColdRoll60, 1, coldRollDraw: 1));
        other.SalesOrderNo = SO;
        ctx.ProductionBatches.Add(other);
        await ctx.SaveChangesAsync();

        var tree = await CreateService(ctx, EmptyKanban()).GetTreeAsync(SO);

        tree!.MainNos.Should().ContainSingle().Subject.Production.Should().BeNull();
    }

    [Fact]
    public async Task GetTreeAsync_无该订单工单_返回空()
    {
        var ctx = CreateDbContext();
        var tree = await CreateService(ctx, EmptyKanban()).GetTreeAsync("SO-NOT-EXIST");
        tree.Should().BeNull();
    }

    [Fact]
    public async Task GetTreeAsync_空订单号_返回空()
    {
        var ctx = CreateDbContext();
        ctx.Set<WorkOrderExecutionSummary>().Add(NewSummary(1, "G100", stage: 3));
        await ctx.SaveChangesAsync();

        var tree = await CreateService(ctx, EmptyKanban()).GetTreeAsync("  ");
        tree.Should().BeNull();
    }

    [Fact]
    public async Task GetTreeAsync_头部聚合字段与实时项次牌号标准_取值正确()
    {
        var ctx = CreateDbContext();

        // SalesOrder → OrderItem(Sequence 定位) → WorkOrder(OrderItemIds 逗号分隔引用 Sequence)
        var so = new MES.Data.Entities.Order.SalesOrder
        {
            OrderNumber = SO,
            SignDate = new DateTime(2026, 8, 1),
            Status = MES.Core.Enums.SalesOrderStatus.Confirmed,
            RowVersion = new byte[8],
            CustomerName = "实时客户",
            Salesman = "实时业务员",
        };
        ctx.SalesOrders.Add(so);
        await ctx.SaveChangesAsync();

        ctx.OrderItems.Add(new MES.Data.Entities.Order.OrderItem
        {
            SalesOrderId = so.Id,
            Sequence = 1,
            StandardGrade = "S32168",
            StandardNo = "GB/T 13296-2023",
            PlantGrade = "20#",
            Density = 7.85m,
            OuterDiameter = 25m,
            WallThickness = 2.5m,
            Specification = "25*2.5",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            LengthStatus = LengthStatus.Fixed,
            Quantity = 10,
            ContractWeight = 2500m,
            TheoreticalWeight = 2500m,
        });
        ctx.WorkOrders.Add(new MES.Data.Entities.WorkOrder.WorkOrder
        {
            WorkOrderNo = "WO-1",
            SalesOrderNo = SO,
            ProductionMainNo = "G100",
            ProductionSubNo = "01",
            OrderItemIds = "1",
            Status = WorkOrderStatus.Pending,
            RowVersion = new byte[8],
            SignDate = new DateTime(2026, 8, 1),
            Salesman = "测试",
            DeliveryDate = new DateTime(2026, 12, 1),
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            SettlementMethod = SettlementMethod.Theoretical,
            StandardCode = "GB/T13296-2023",
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            PlantGrade = "20#",
            Specification = "25*2.5",
            LengthStatus = LengthStatus.Fixed,
            TotalQuantity = 100,
            TotalWeight = 1000m,
            TotalItemCount = 1,
        });
        await ctx.SaveChangesAsync();

        var r = NewSummary(1, "G100", stage: 3);
        r.TotalWeight = 1000m;
        r.TotalQuantity = 100;
        r.Specification = "25*2.5";
        r.LengthStatus = "Fixed";
        r.DeliveryState = "SolutionAnnealedAndPickled";
        r.DelayPenalty = true;
        r.SignDate = new DateTime(2026, 8, 1);
        r.DeliveryDate = new DateTime(2026, 12, 1);
        r.EstimatedProcessCompletionDate = new DateTime(2026, 10, 8);
        ctx.Set<WorkOrderExecutionSummary>().Add(r);
        await ctx.SaveChangesAsync();

        var tree = await CreateService(ctx, EmptyKanban()).GetTreeAsync(SO);

        // 订单头：签订 / 交期截止(取最大) / 延期罚款(Any) / 总重(Σ) / 含项次(实时去重)
        tree!.SignDate.Should().Be(new DateTime(2026, 8, 1));
        tree.DeliveryDate.Should().Be(new DateTime(2026, 12, 1));
        tree.DelayPenalty.Should().BeTrue();
        tree.OrderTotalWeightKg.Should().Be(1000m);
        tree.ItemCount.Should().Be(1);

        // 主号头：牌号/产品标准走实时 join OrderItem；规格/长度/交货/支数/预计走 WES 快照
        var main = tree.MainNos.Should().ContainSingle().Subject;
        main.StandardGrade.Should().Be("S32168");
        main.ProductStandard.Should().Be("GB/T 13296-2023");
        main.Specification.Should().Be("25*2.5");
        main.LengthStatus.Should().Be("Fixed");
        main.DeliveryState.Should().Be("SolutionAnnealedAndPickled");
        main.QuantitySum.Should().Be(100);
        main.EstimatedCompletionDate.Should().Be(new DateTime(2026, 10, 8));
    }

    // ===================== 原料锁定（A–D 单叶，按类取现成字段） =====================

    [Fact]
    public async Task GetTreeAsync_原料锁定B执行返整_仅B叶等于待返整成重和()
    {
        var ctx = CreateDbContext();
        var r1 = NewSummary(1, "G100", stage: 2);
        r1.RawMaterialLockRemark = RawMaterialLockRemarkKeys.ExecuteRework;
        r1.PendingReworkOutputWeight = 200m;
        var r2 = NewSummary(2, "G100", stage: 2);
        r2.RawMaterialLockRemark = RawMaterialLockRemarkKeys.ExecuteRework;
        r2.PendingReworkOutputWeight = 300m;
        ctx.Set<WorkOrderExecutionSummary>().AddRange(r1, r2);
        await ctx.SaveChangesAsync();

        var tree = await CreateService(ctx, EmptyKanban()).GetTreeAsync(SO);

        var main = tree!.MainNos.Should().ContainSingle().Subject;
        main.RawMaterialLock.Should().NotBeNull();
        main.RawMaterialLock!.Leaves.Should().ContainSingle();
        var leaf = LeafOf(main.RawMaterialLock, RawMaterialLockRemarkKeys.ExecuteRework)!;
        leaf.Text.Should().Be("生产返整补足");
        leaf.WeightKg.Should().Be(500m); // = Σ PendingReworkOutputWeight
        main.Production.Should().BeNull(); // 无在产待量不建分支
        main.FinalInspection.Should().BeNull();
        main.Warehousing.Should().BeNull();
    }

    [Fact]
    public async Task GetTreeAsync_原料锁定A质量补料_叶重等于缺料缺口减返整()
    {
        await WithToleranceAsync(0.03m, async () =>
        {
            var ctx = CreateDbContext();
            var r1 = NewSummary(1, "G100", stage: 2);
            r1.RawMaterialLockRemark = RawMaterialLockRemarkKeys.QualityReplenish;
            r1.SemiPlanWeight = 1000m;   // 计划投料，现可 0 → 缺 1000
            r1.PendingReworkOutputWeight = 300m;
            var r2 = NewSummary(2, "G100", stage: 2);
            r2.RawMaterialLockRemark = RawMaterialLockRemarkKeys.QualityReplenish;
            r2.SemiPlanWeight = 1000m;
            r2.PendingReworkOutputWeight = 300m;
            ctx.Set<WorkOrderExecutionSummary>().AddRange(r1, r2);
            await ctx.SaveChangesAsync();

            var tree = await CreateService(ctx, EmptyKanban()).GetTreeAsync(SO);

            var main = tree!.MainNos.Should().ContainSingle().Subject;
            var leaf = LeafOf(main.RawMaterialLock!, RawMaterialLockRemarkKeys.QualityReplenish)!;
            leaf.Text.Should().Be("质量补料");
            // 缺料 2000 − 待返整 600 = 真需外补 1400
            leaf.WeightKg.Should().Be(1400m);
        });
    }

    [Fact]
    public async Task GetTreeAsync_原料锁定C执行计划与D完善计划_叶重等于缺料缺口()
    {
        await WithToleranceAsync(0.03m, async () =>
        {
            var ctx = CreateDbContext();
            var r1 = NewSummary(1, "G100", stage: 2);
            r1.RawMaterialLockRemark = RawMaterialLockRemarkKeys.ExecutePlan;
            r1.SemiPlanWeight = 1500m;   // 缺 1500
            var r2 = NewSummary(2, "G200", stage: 2);
            r2.RawMaterialLockRemark = RawMaterialLockRemarkKeys.ImprovePlan;
            r2.SemiPlanWeight = 800m;    // 缺 800
            ctx.Set<WorkOrderExecutionSummary>().AddRange(r1, r2);
            await ctx.SaveChangesAsync();

            var tree = await CreateService(ctx, EmptyKanban()).GetTreeAsync(SO);

            tree!.MainNos.Should().HaveCount(2);
            LeafOf(tree.MainNos[0].RawMaterialLock!, RawMaterialLockRemarkKeys.ExecutePlan)!.WeightKg.Should().Be(1500m);
            LeafOf(tree.MainNos[0].RawMaterialLock!, RawMaterialLockRemarkKeys.ExecutePlan)!.Text.Should().Be("执行用料计划");
            LeafOf(tree.MainNos[1].RawMaterialLock!, RawMaterialLockRemarkKeys.ImprovePlan)!.WeightKg.Should().Be(800m);
            LeafOf(tree.MainNos[1].RawMaterialLock!, RawMaterialLockRemarkKeys.ImprovePlan)!.Text.Should().Be("完善用料计划");
        });
    }

    // ===================== 完结主号（投料：生产批次工艺卡 + 产出：各仓库实收） =====================

    [Fact]
    public async Task GetTreeAsync_完结主号_忽略在产待量与看板_零值快照分支不渲染且成品入库保留()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var batch = NewOrderFinishedBatch(wh.Id, "CK001", "G100", initialWeight: 1000m, remainingWeight: 700m);
        ctx.InventoryBatches.Add(batch);
        await ctx.SaveChangesAsync();

        ctx.OutboundRecords.AddRange(
            NewSalesOut(9001, batch.Id, weight: 300m),
            NewSalesOut(9002, batch.Id, weight: 200m));
        var r = NewSummary(1, "G100", stage: 1); // 完结
        r.TotalWeight = 1000m;
        r.PendingSection60Roll = 500m;           // 完结主号亦强制不显示在产
        r.RawMaterialLockRemark = RawMaterialLockRemarkKeys.ExecuteRework;
        ctx.Set<WorkOrderExecutionSummary>().Add(r);
        await ctx.SaveChangesAsync();

        // 看板即使含该主号（检验中）也不出检验分支
        var mock = new Mock<IFinalInspectionPlanService>();
        mock.Setup(s => s.GetKanbanAsync()).ReturnsAsync(new List<FinalInspectionPlanDto>
        {
            NewKanbanRow(10, "G100", KanbanStageKeys.Inspecting, weight: 777m),
        });

        var tree = await CreateService(ctx, mock.Object).GetTreeAsync(SO);

        var main = tree!.MainNos.Should().ContainSingle().Subject;
        main.IsCompleted.Should().BeTrue();
        main.RawMaterialLock.Should().BeNull();
        main.Production.Should().BeNull();
        main.FinalInspection.Should().BeNull();

        // 完结专属分支零值全部不渲染（无生产批次 → 投料 0；无次品库/在制品库/成品库备料入库）
        main.ProductionInput.Should().BeNull();
        main.SurplusInbound.Should().BeNull();
        main.DefectInbound.Should().BeNull();
        main.FinishedStockInbound.Should().BeNull();

        main.Warehousing.Should().NotBeNull();
        main.Warehousing!.Leaves.Should().HaveCount(3);
        LeafOf(main.Warehousing, "Inbound")!.Text.Should().Be("入库");
        LeafOf(main.Warehousing, "Inbound")!.WeightKg.Should().Be(1000m);
        LeafOf(main.Warehousing, "Stock")!.Text.Should().Be("库存");
        LeafOf(main.Warehousing, "Stock")!.WeightKg.Should().Be(700m);
        LeafOf(main.Warehousing, "Outbound")!.Text.Should().Be("出库");
        LeafOf(main.Warehousing, "Outbound")!.WeightKg.Should().Be(500m); // 300+200 两条 SalesOut 求和
    }

    [Fact]
    public async Task GetTreeAsync_完结主号_投料取生产批次工艺卡领料重并排除返整委外生产对外加工()
    {
        var ctx = CreateDbContext();

        // 投料 = Σ 生产批次工艺卡 InputWeight；排除 返整/委外生产/对外加工；不限制造物品；
        // 标题 = 该主号投料批次 ProductionType 去重、按 ProductionTypeKeys.All 序、中文「+」连接
        ctx.ProductionBatches.AddRange(
            NewProductionBatch("2609-1001", "G100", InventoryMaterialTypes.OrderFinished, ProductionTypeKeys.RoughTube, 600m),
            NewProductionBatch("2609-1002", "G100", InventoryMaterialTypes.Surplus, ProductionTypeKeys.InProcess, 400m), // 非订单成品制造物品亦计
            NewProductionBatch("2609-1003", "G100", InventoryMaterialTypes.OrderFinished, ProductionTypeKeys.InProcess, 300m), // 类型重复：标题去重
            NewProductionBatch("2609-1004", "G100", InventoryMaterialTypes.OrderFinished, ProductionTypeKeys.OutsourcedPurchased, 200m),
            NewProductionBatch("2609-1005", "G100", InventoryMaterialTypes.OrderFinished, ProductionTypeKeys.Rework, 999m),        // 返整：不计
            NewProductionBatch("2609-1006", "G100", InventoryMaterialTypes.OrderFinished, ProductionTypeKeys.Subcontract, 888m),   // 委外生产：不计
            NewProductionBatch("2609-1007", "G100", InventoryMaterialTypes.OrderFinished, ProductionTypeKeys.ExternalProcessing, 777m)); // 对外加工：不计

        var r = NewSummary(1, "G100", stage: 1); // 完结
        r.InputWeight = 12345m;                  // WES 快照投料：新口径不再采用
        ctx.Set<WorkOrderExecutionSummary>().Add(r);
        await ctx.SaveChangesAsync();

        var tree = await CreateService(ctx, EmptyKanban()).GetTreeAsync(SO);

        var main = tree!.MainNos.Should().ContainSingle().Subject;
        main.IsCompleted.Should().BeTrue();

        main.ProductionInput.Should().NotBeNull();
        main.ProductionInput!.Title.Should().Be("生产投料[荒管生产+在制生产+外购]");
        var input = LeafOf(main.ProductionInput, "Input")!;
        input.Text.Should().Be("投料");
        input.WeightKg.Should().Be(1500m); // 600+400+300+200

        // 非完结分支在完结主号下恒空
        main.RawMaterialLock.Should().BeNull();
        main.Production.Should().BeNull();
        main.FinalInspection.Should().BeNull();
    }

    [Fact]
    public async Task GetTreeAsync_完结主号_次品入库按生产批号反查主号聚合且每叶附同叶退货出库量()
    {
        var ctx = CreateDbContext();
        var defect = await SeedWarehouseWithCodeAsync(ctx, WarehouseCodes.Defect, "次品库");
        var fg = await SeedWarehouseWithCodeAsync(ctx, WarehouseCodes.FinishedGoods, "成品库");

        ctx.ProductionBatches.AddRange(
            NewProductionBatch("2609-3001", "G100"),
            NewProductionBatch("2609-3002", "G200"));

        // 次品库 6 类物料均可入叶；叶序与 InventoryMaterialTypes.WarehouseAllowedTypes["DEFECT"] 一致
        var d1 = NewInboundBatch(defect.Id, "CK-D1", InventoryMaterialTypes.DefectRoughTube, "2609-3001", 500m);
        var d2 = NewInboundBatch(defect.Id, "CK-D2", InventoryMaterialTypes.DefectRoughTube, "2609-3002", 111m); // 归属另一主号
        var d3 = NewInboundBatch(defect.Id, "CK-D3", InventoryMaterialTypes.Scrap, "2609-3001", 60m);
        var d4 = NewInboundBatch(defect.Id, "CK-D4", InventoryMaterialTypes.DefectSemi, null, 999m);            // 无生产批号：不计
        // 成品库批次不属次品入库分支（仅取次品库）
        var d5 = NewInboundBatch(fg.Id, "CK-B9", InventoryMaterialTypes.Finished, "2609-3001", 777m);
        ctx.InventoryBatches.AddRange(d1, d2, d3, d4, d5);
        await ctx.SaveChangesAsync();

        // 退货出库只挂次品叶：d1 退 400；d5 的退货出库因不在次品库而不计
        ctx.OutboundRecords.AddRange(
            NewReturnOut(9101, d1.Id, 400m),
            NewReturnOut(9102, d5.Id, 700m));
        await ctx.SaveChangesAsync();

        ctx.Set<WorkOrderExecutionSummary>().AddRange(
            NewSummary(1, "G100", stage: 1),
            NewSummary(2, "G200", stage: 1));
        await ctx.SaveChangesAsync();

        var tree = await CreateService(ctx, EmptyKanban()).GetTreeAsync(SO);

        var g1 = tree!.MainNos.Should().ContainSingle(m => m.ProductionMainNo == "G100").Subject;
        g1.DefectInbound.Should().NotBeNull();
        g1.DefectInbound!.Title.Should().Be("次品入库");
        g1.DefectInbound.Leaves.Should().HaveCount(2); // 次品荒管 + 报废品（无值的叶省略）
        var rt = LeafOf(g1.DefectInbound, InventoryMaterialTypes.DefectRoughTube)!;
        rt.Text.Should().Be("次品荒管");
        rt.WeightKg.Should().Be(500m);
        rt.ReturnWeightKg.Should().Be(400m);
        var scrap = LeafOf(g1.DefectInbound, InventoryMaterialTypes.Scrap)!;
        scrap.Text.Should().Be("报废品");
        scrap.WeightKg.Should().Be(60m);
        scrap.ReturnWeightKg.Should().Be(0m);

        var g2 = tree.MainNos.Should().ContainSingle(m => m.ProductionMainNo == "G200").Subject;
        LeafOf(g2.DefectInbound!, InventoryMaterialTypes.DefectRoughTube)!.WeightKg.Should().Be(111m);
    }

    [Fact]
    public async Task GetTreeAsync_完结主号_余料与备料入库按生产批号反查订单主号聚合并忽略无批号行()
    {
        var ctx = CreateDbContext();
        var wip = await SeedWarehouseWithCodeAsync(ctx, WarehouseCodes.WorkInProgress, "在制品库");
        var fg = await SeedWarehouseWithCodeAsync(ctx, WarehouseCodes.FinishedGoods, "成品库");

        ctx.ProductionBatches.AddRange(
            NewProductionBatch("2609-0001", "G100"),
            NewProductionBatch("2609-0002", "G100"),
            NewProductionBatch("2609-0003", "G200", inputWeight: 800m));

        ctx.InventoryBatches.AddRange(
            // 在制品库余料：同主号两批求和；入库行自身不带订单号，只能靠生产批号反查
            NewInboundBatch(wip.Id, "CK-S1", InventoryMaterialTypes.Surplus, "2609-0001", 300m),
            NewInboundBatch(wip.Id, "CK-S2", InventoryMaterialTypes.Surplus, "2609-0002", 200m),
            NewInboundBatch(wip.Id, "CK-S3", InventoryMaterialTypes.Surplus, null, 999m), // 无生产批号：不计
            // 成品库备料成品：归属另一主号
            NewInboundBatch(fg.Id, "CK-B1", InventoryMaterialTypes.Finished, "2609-0003", 700m));

        var g200 = NewSummary(2, "G200", stage: 1);
        ctx.Set<WorkOrderExecutionSummary>().AddRange(NewSummary(1, "G100", stage: 1), g200);
        await ctx.SaveChangesAsync();

        var tree = await CreateService(ctx, EmptyKanban()).GetTreeAsync(SO);

        var g1 = tree!.MainNos.Should().ContainSingle(m => m.ProductionMainNo == "G100").Subject;
        LeafOf(g1.SurplusInbound, "Inbound")!.Text.Should().Be("入库");
        LeafOf(g1.SurplusInbound, "Inbound")!.WeightKg.Should().Be(500m); // 300+200，无批号的 999 不计
        g1.FinishedStockInbound.Should().BeNull();

        var g2 = tree.MainNos.Should().ContainSingle(m => m.ProductionMainNo == "G200").Subject;
        LeafOf(g2.FinishedStockInbound, "Inbound")!.WeightKg.Should().Be(700m);
        g2.SurplusInbound.Should().BeNull();
        g2.ProductionInput!.Title.Should().Be("生产投料"); // 无生产类型 → 无方括号
    }

    // ===================== 成品检验（看板去重 / 档位归属 / 第4档不入树） =====================

    [Fact]
    public async Task GetTreeAsync_成检同批两行去重取首行_重量按档归属且第4档不入树()
    {
        var ctx = CreateDbContext();
        var r = NewSummary(1, "G100", stage: 4); // 成检档非完结 → 出检验分支
        ctx.Set<WorkOrderExecutionSummary>().Add(r);
        await ctx.SaveChangesAsync();

        var mock = new Mock<IFinalInspectionPlanService>();
        mock.Setup(s => s.GetKanbanAsync()).ReturnsAsync(new List<FinalInspectionPlanDto>
        {
            NewKanbanRow(10, "G100", KanbanStageKeys.WaitingMaterial, weight: 100m),
            NewKanbanRow(10, "G100", KanbanStageKeys.WaitingMaterial, weight: 999m), // 同批去重，首行 100 生效
            NewKanbanRow(11, "G100", KanbanStageKeys.Inspecting, weight: 400m),
            NewKanbanRow(12, "G100", KanbanStageKeys.CompletedAwaitingInbound, weight: 600m), // 第4档不入树
        });

        var tree = await CreateService(ctx, mock.Object).GetTreeAsync(SO);

        var main = tree!.MainNos.Should().ContainSingle().Subject;
        main.FinalInspection.Should().NotBeNull();
        main.FinalInspection!.Leaves.Should().HaveCount(2);
        var waiting = LeafOf(main.FinalInspection, KanbanStageKeys.WaitingMaterial)!;
        waiting.Text.Should().Be("待到料");
        waiting.WeightKg.Should().Be(100m); // 同批去重只计一次
        LeafOf(main.FinalInspection, KanbanStageKeys.Inspecting)!.WeightKg.Should().Be(400m);
        LeafOf(main.FinalInspection, KanbanStageKeys.CompletedAwaitingInbound).Should().BeNull();
        main.Warehousing.Should().BeNull();
        main.Production.Should().BeNull();
    }

    [Fact]
    public async Task GetTreeAsync_成检叶名单_单段且沿同批去重取首行同源()
    {
        var ctx = CreateDbContext();
        var r = NewSummary(1, "G100", stage: 4);
        ctx.Set<WorkOrderExecutionSummary>().Add(r);
        await ctx.SaveChangesAsync();

        var mock = new Mock<IFinalInspectionPlanService>();
        mock.Setup(s => s.GetKanbanAsync()).ReturnsAsync(new List<FinalInspectionPlanDto>
        {
            NewKanbanRow(10, "G100", KanbanStageKeys.WaitingMaterial, weight: 100m),
            NewKanbanRow(10, "G100", KanbanStageKeys.WaitingMaterial, weight: 999m), // 同批去重：重量与名单同取首行
            NewKanbanRow(11, "G100", KanbanStageKeys.WaitingMaterial, weight: 400m),
        });

        var tree = await CreateService(ctx, mock.Object).GetTreeAsync(SO);

        var waiting = LeafOf(tree!.MainNos[0].FinalInspection, KanbanStageKeys.WaitingMaterial)!;
        waiting.WeightKg.Should().Be(500m);

        var seg = waiting.BatchSegments.Should().ContainSingle().Subject; // 单段（每批恰好落一个档）
        seg.Key.Should().Be("Batches");
        seg.Label.Should().Be(KanbanStageKeys.WaitingMaterial);
        seg.WeightKg.Should().Be(waiting.WeightKg); // 段合计 == 叶重
        seg.BatchCount.Should().Be(2);
        seg.Batches.Select(b => b.BatchId).Should().Equal(10, 11);
        seg.Batches[0].WeightKg.Should().Be(100m); // 同批只计首行 100，不取整组 999
    }

    [Fact]
    public async Task GetTreeAsync_成检第4档_不入树也无名单()
    {
        var ctx = CreateDbContext();
        ctx.Set<WorkOrderExecutionSummary>().Add(NewSummary(1, "G100", stage: 4));
        await ctx.SaveChangesAsync();

        var mock = new Mock<IFinalInspectionPlanService>();
        mock.Setup(s => s.GetKanbanAsync()).ReturnsAsync(new List<FinalInspectionPlanDto>
        {
            NewKanbanRow(12, "G100", KanbanStageKeys.CompletedAwaitingInbound, weight: 600m),
        });

        var tree = await CreateService(ctx, mock.Object).GetTreeAsync(SO);

        tree!.MainNos[0].FinalInspection.Should().BeNull();
    }

    [Fact]
    public async Task GetTreeAsync_批次名单_按生产编号Ordinal稳定序()
    {
        var ctx = CreateDbContext();
        ctx.Set<WorkOrderExecutionSummary>().Add(NewSummary(1, "G100", stage: 3));
        // 插入序打乱，断言输出按生产编号 Ordinal 升序（不依赖 ProcessGroups/GroupBy 组序）
        ctx.ProductionBatches.AddRange(
            NewPendingBatch("2609-300", "WO-001", "G100", 1m, ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, false,
                Pg(ProcessKeys.ColdRoll60, 1, coldRollDraw: 1)),
            NewPendingBatch("2609-100", "WO-001", "G100", 1m, ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, false,
                Pg(ProcessKeys.ColdRoll60, 1, coldRollDraw: 1)),
            NewPendingBatch("2609-200", "WO-001", "G100", 1m, ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, false,
                Pg(ProcessKeys.ColdRoll60, 1, coldRollDraw: 1)));
        await ctx.SaveChangesAsync();

        var tree = await CreateService(ctx, EmptyKanban()).GetTreeAsync(SO);

        LeafOf(tree!.MainNos[0].Production, "ColdRoll60")!
            .BatchSegments.Single().Batches.Select(b => b.BatchNo)
            .Should().Equal("2609-100", "2609-200", "2609-300");
    }

    // ===================== 成品入库（跨批求和 / 0叶省略 / 空分支为 null） =====================

    [Fact]
    public async Task GetTreeAsync_成品入库多批求和_零叶省略且空分支为null()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        ctx.InventoryBatches.AddRange(
            NewOrderFinishedBatch(wh.Id, "CK101", "G100", initialWeight: 1000m, remainingWeight: 700m),
            NewOrderFinishedBatch(wh.Id, "CK102", "G100", initialWeight: 2000m, remainingWeight: 0m),
            NewOrderFinishedBatch(wh.Id, "CK201", "G200", initialWeight: 0m, remainingWeight: 0m));
        ctx.Set<WorkOrderExecutionSummary>().AddRange(
            NewSummary(1, "G100", stage: 3),
            NewSummary(2, "G200", stage: 3));
        await ctx.SaveChangesAsync();

        var tree = await CreateService(ctx, EmptyKanban()).GetTreeAsync(SO);

        var g1 = tree!.MainNos.Should().ContainSingle(m => m.ProductionMainNo == "G100").Subject;
        g1.Warehousing.Should().NotBeNull();
        g1.Warehousing!.Leaves.Should().HaveCount(2); // 入库+库存；无出库 → 出库 0 叶省略
        LeafOf(g1.Warehousing, "Inbound")!.WeightKg.Should().Be(3000m); // 1000+2000 跨批求和
        LeafOf(g1.Warehousing, "Stock")!.WeightKg.Should().Be(700m);
        LeafOf(g1.Warehousing, "Outbound").Should().BeNull();

        var g2 = tree.MainNos.Should().ContainSingle(m => m.ProductionMainNo == "G200").Subject;
        g2.Warehousing.Should().BeNull(); // 全 0 → 空分支为 null
    }

    // ===================== 排序：完结沉底、非完结按紧急升序 =====================

    [Fact]
    public async Task GetTreeAsync_完结主号沉底_非完结按紧急程度升序()
    {
        var ctx = CreateDbContext();
        var active1 = NewSummary(1, "ACTIVE-B", stage: 3);
        active1.UrgencyLevel = UrgencyLevelKeys.BOrder;
        var active2 = NewSummary(2, "ACTIVE-A", stage: 3);
        active2.UrgencyLevel = UrgencyLevelKeys.APlusUrgent;
        var completed = NewSummary(3, "COMPLETED", stage: 1);
        completed.UrgencyLevel = UrgencyLevelKeys.APlusUrgent;
        ctx.Set<WorkOrderExecutionSummary>().AddRange(active1, active2, completed);
        await ctx.SaveChangesAsync();

        var tree = await CreateService(ctx, EmptyKanban()).GetTreeAsync(SO);

        tree!.MainNos.Select(m => m.ProductionMainNo).Should().Equal("ACTIVE-A", "ACTIVE-B", "COMPLETED");
        tree.MainNos[0].IsCompleted.Should().BeFalse();
        tree.MainNos[1].IsCompleted.Should().BeFalse();
        tree.MainNos[2].IsCompleted.Should().BeTrue();
    }
}
