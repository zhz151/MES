using FluentAssertions;
using Moq;
using MES.Core.Constants;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.Scheduling;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Core.Interfaces.Scheduling;
using MES.Data;
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

    /// <summary>造一条销售出库记录</summary>
    private static OutboundRecord NewSalesOut(long id, int inventoryBatchId, decimal weight)
        => new()
        {
            Id = id,
            InventoryBatchId = inventoryBatchId,
            OutboundType = OutboundType.SalesOut,
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

    // ===================== 主号聚合：生产8节点 / 总重 / 紧急 =====================

    [Fact]
    public async Task GetTreeAsync_两工单一主号_生产8节点与总重按SUM聚合且取最急紧急()
    {
        var ctx = CreateDbContext();
        var r1 = NewSummary(1, "G100", stage: 3);
        r1.TotalWeight = 1000m;
        r1.PendingSection60Roll = 100m;
        r1.PendingSection50Roll = 200m;
        r1.UrgencyLevel = UrgencyLevelKeys.BOrder;
        r1.LastRefreshTime = new DateTime(2026, 9, 1, 10, 0, 0);

        var r2 = NewSummary(2, "G100", stage: 3);
        r2.TotalWeight = 2000m;
        r2.PendingSection60Roll = 150m;
        r2.UrgencyLevel = UrgencyLevelKeys.APlusUrgent;
        r2.LastRefreshTime = new DateTime(2026, 9, 1, 11, 0, 0);

        ctx.Set<WorkOrderExecutionSummary>().AddRange(r1, r2);
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
        LeafOf(main.Production, "ColdRoll60")!.WeightKg.Should().Be(250m); // 100+150 工单级 SUM
        LeafOf(main.Production, "ColdRoll50")!.WeightKg.Should().Be(200m); // 单值仅取一次
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

    // ===================== 完结主号（仅成品入库分支） =====================

    [Fact]
    public async Task GetTreeAsync_完结主号_仅成品入库分支_忽略在产待量与看板()
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

        main.Warehousing.Should().NotBeNull();
        main.Warehousing!.Leaves.Should().HaveCount(3);
        LeafOf(main.Warehousing, "Inbound")!.Text.Should().Be("入库");
        LeafOf(main.Warehousing, "Inbound")!.WeightKg.Should().Be(1000m);
        LeafOf(main.Warehousing, "Stock")!.Text.Should().Be("库存");
        LeafOf(main.Warehousing, "Stock")!.WeightKg.Should().Be(700m);
        LeafOf(main.Warehousing, "Outbound")!.Text.Should().Be("出库");
        LeafOf(main.Warehousing, "Outbound")!.WeightKg.Should().Be(500m); // 300+200 两条 SalesOut 求和
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
