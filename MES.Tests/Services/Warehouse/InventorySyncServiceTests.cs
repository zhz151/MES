using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MES.Core.Enums;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.WorkOrder;
using MES.Data;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Warehouse;
using WorkOrderEntity = MES.Data.Entities.WorkOrder.WorkOrder;
using MES.Services.Warehouse;
using MES.Tests.Tests;
using Moq;

namespace MES.Tests.Services;

/// <summary>
/// 库存来源单同步服务测试：采购单/委外单到货字段与入库批次汇总一致，批次删光后应回退归零
/// </summary>
public class InventorySyncServiceTests : TestBase
{
    private InventorySyncService CreateService(AppDbContext ctx)
    {
        var configMock = new Mock<IConfigParameterService>();
        var woExecMock = new Mock<IWorkOrderExecutionService>();
        woExecMock.Setup(x => x.RefreshByWorkOrderNosAsync(It.IsAny<List<string>>()))
            .Returns(Task.CompletedTask);
        var loggerMock = new Mock<ILogger<InventorySyncService>>();
        return new InventorySyncService(ctx, configMock.Object, woExecMock.Object, loggerMock.Object, new MemoryCache(new MemoryCacheOptions()));
    }

    private async Task<PurchaseOrder> SeedPurchaseOrderAsync(AppDbContext ctx, string orderNo,
        PurchaseOrderStatus status, int? quantity, int receivedQty = 0, decimal receivedWt = 0m)
    {
        var order = new PurchaseOrder
        {
            OrderNo = orderNo,
            SupplierId = 1,
            SupplierName = "测试供应商",
            OrderDate = DateTime.Today,
            Status = status,
            MaterialCategory = "RoughTube",
            PlantGrade = "20#",
            Specification = "219*8",
            Quantity = quantity,
            Weight = 1000m,
            RequiredDate = DateTime.Today.AddDays(30),
            ReceivedQuantity = receivedQty,
            ReceivedWeight = receivedWt
        };
        ctx.PurchaseOrders.Add(order);
        await ctx.SaveChangesAsync();
        return order;
    }

    [Fact]
    public async Task SyncSourceOrdersAsync_批次删光_采购单到货字段回退为零状态Open()
    {
        // 场景：采购单曾到货（残留快照），但关联批次已被删除（表内无匹配）→ 应回退为 0 / Open
        var ctx = CreateDbContext();
        var order = await SeedPurchaseOrderAsync(ctx, "CG_RESIDUE001",
            PurchaseOrderStatus.Completed, quantity: 41, receivedQty: 1500, receivedWt: 32000m);
        order.LastArrivalDate = DateTime.Today;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SyncSourceOrdersAsync(new List<string> { "CG_RESIDUE001" });

        var updated = await ctx.PurchaseOrders.FindAsync(order.Id);
        updated!.ReceivedQuantity.Should().Be(0);
        updated.ReceivedWeight.Should().Be(0m);
        updated.LastArrivalDate.Should().BeNull();
        updated.Status.Should().Be(PurchaseOrderStatus.Open);
    }

    [Fact]
    public async Task SyncSourceOrdersAsync_有批次_汇总到货并置状态()
    {
        var ctx = CreateDbContext();
        var order = await SeedPurchaseOrderAsync(ctx, "CG_OK001", PurchaseOrderStatus.Open, quantity: 100);

        ctx.InventoryBatches.Add(new InventoryBatch
        {
            BatchNo = "BATCH001",
            InboundSource = "采购",
            SourceName = "测试供应商",
            SourceOrderNo = order.OrderNo,
            MaterialType = "RoughTube",
            PlantGrade = "20#",
            Specification = "219*8",
            InitialQuantity = 30,
            InitialWeight = 300m,
            WarehouseId = 1,
            InboundDate = DateTime.Today
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.SyncSourceOrdersAsync(new List<string> { "CG_OK001" });

        var updated = await ctx.PurchaseOrders.FindAsync(order.Id);
        updated!.ReceivedQuantity.Should().Be(30);
        updated.ReceivedWeight.Should().Be(300m);
        updated.LastArrivalDate.Should().Be(DateTime.Today);
        updated.Status.Should().Be(PurchaseOrderStatus.Partial);
    }

    // ========== 来源单工单号变更实时扫描 ==========

    private async Task<InventoryBatch> SeedInboundBatchAsync(AppDbContext ctx, string batchNo, string sourceOrderNo,
        int? sourceOrderSequence, string workOrderNo, int warehouseId)
    {
        var batch = new InventoryBatch
        {
            BatchNo = batchNo,
            InboundSource = InboundSource.Purchase.ToString(),
            SourceName = "测试供应商",
            SourceOrderNo = sourceOrderNo,
            SourceOrderSequence = sourceOrderSequence,
            WorkOrderNo = workOrderNo,
            MaterialType = "RoughTube",
            PlantGrade = "20#",
            Specification = "219*8",
            InitialQuantity = 10,
            InitialWeight = 100m,
            WarehouseId = warehouseId,
            InboundDate = DateTime.Today
        };
        ctx.InventoryBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    [Fact]
    public async Task GetSourceOrderChangedBatchesAsync_采购单工单号已变更_返回批次及期望工单号()
    {
        var ctx = CreateDbContext();
        var order = await SeedPurchaseOrderAsync(ctx, "CG_CHG001", PurchaseOrderStatus.Open, quantity: 100);
        order.SourceWorkOrderNo = "D26Z1104002-X01-01";
        await ctx.SaveChangesAsync();

        await SeedInboundBatchAsync(ctx, "CK260209073", "CG_CHG001", null,
            workOrderNo: "OLD-WO-01", warehouseId: 1);

        var svc = CreateService(ctx);
        var result = await svc.GetSourceOrderChangedBatchesAsync();

        result.Should().ContainSingle();
        result[0].BatchNo.Should().Be("CK260209073");
        result[0].SourceOrderNo.Should().Be("CG_CHG001");
        result[0].ExpectedWorkOrderNo.Should().Be("D26Z1104002-X01-01");
    }

    [Fact]
    public async Task GetSourceOrderChangedBatchesAsync_委外单明细工单号已变更_按序号返回()
    {
        var ctx = CreateDbContext();
        var subOrder = new SubcontractOrder
        {
            OrderNo = "WW_CHG001",
            SupplierId = 1,
            SupplierName = "委外供应商",
            OrderDate = DateTime.Today,
            ProcessType = "Piercing",
            Status = SubcontractOrderStatus.Sent,
            OutMaterialCategory = "RoundBar",
            OutPlantGrade = "20#",
            OutSpecification = "150*8",
            OutQuantity = 10,
            OutWeight = 1000m
        };
        ctx.SubcontractOrders.Add(subOrder);
        await ctx.SaveChangesAsync();

        ctx.SubcontractReturnItems.Add(new SubcontractReturnItem
        {
            SubcontractOrderId = subOrder.Id,
            Sequence = 1,
            MaterialCategory = "RoughTube",
            ProcessSpecification = "219*8",
            SourceWorkOrderNo = "D26Z1104002-X01-01"
        });
        await ctx.SaveChangesAsync();

        await SeedInboundBatchAsync(ctx, "CK260209074", "WW_CHG001", sourceOrderSequence: 1,
            workOrderNo: "OLD-WO-02", warehouseId: 1);

        var svc = CreateService(ctx);
        var result = await svc.GetSourceOrderChangedBatchesAsync();

        result.Should().ContainSingle();
        result[0].BatchNo.Should().Be("CK260209074");
        result[0].SourceOrderSequence.Should().Be(1);
        result[0].ExpectedWorkOrderNo.Should().Be("D26Z1104002-X01-01");
    }

    [Fact]
    public async Task GetSourceOrderChangedBatchesAsync_批次工单号已一致且工单存在_不返回()
    {
        var ctx = CreateDbContext();
        var order = await SeedPurchaseOrderAsync(ctx, "CG_SAME001", PurchaseOrderStatus.Open, quantity: 100);
        order.SourceWorkOrderNo = "D26Z1104002-X01-01";
        await ctx.SaveChangesAsync();
        await SeedWorkOrderAsync(ctx, "D26Z1104002-X01-01", "SO2604002", "X01");

        await SeedInboundBatchAsync(ctx, "CK260209075", "CG_SAME001", null,
            workOrderNo: "D26Z1104002-X01-01", warehouseId: 1);

        var svc = CreateService(ctx);
        var result = await svc.GetSourceOrderChangedBatchesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSourceOrderChangedBatchesAsync_来源单清空工单号_批次残留旧工单号_提示取消()
    {
        // 场景：采购单 SourceWorkOrderNo 已清空（工单被删后手工清空/待重选），批次残留旧工单号 → 判「已取消」
        var ctx = CreateDbContext();
        var order = await SeedPurchaseOrderAsync(ctx, "CG_CANCEL001", PurchaseOrderStatus.Open, quantity: 100);
        // 不设置 SourceWorkOrderNo → purchaseMap 中为 null（空）

        await SeedInboundBatchAsync(ctx, "CK260209077", "CG_CANCEL001", null,
            workOrderNo: "OLD-WO-CANCEL", warehouseId: 1);

        var svc = CreateService(ctx);
        var result = await svc.GetSourceOrderChangedBatchesAsync();

        result.Should().ContainSingle();
        result[0].BatchNo.Should().Be("CK260209077");
        result[0].IsCancelled.Should().BeTrue();
        result[0].ExpectedWorkOrderNo.Should().Be("OLD-WO-CANCEL");
    }

    [Fact]
    public async Task GetSourceOrderChangedBatchesAsync_工单已物理删除_批次与来源单一致_提示取消()
    {
        // 场景：采购单 SourceWorkOrderNo=工单A、批次工单号=工单A（两者一致），但工单A已被物理删除 → 判「已取消」
        var ctx = CreateDbContext();
        var order = await SeedPurchaseOrderAsync(ctx, "CG_DELETED001", PurchaseOrderStatus.Open, quantity: 100);
        order.SourceWorkOrderNo = "WO-DELETED-01";
        await ctx.SaveChangesAsync();
        // 不 seed WorkOrder → existingWorkOrderSet 无此工单

        await SeedInboundBatchAsync(ctx, "CK260209078", "CG_DELETED001", null,
            workOrderNo: "WO-DELETED-01", warehouseId: 1);

        var svc = CreateService(ctx);
        var result = await svc.GetSourceOrderChangedBatchesAsync();

        result.Should().ContainSingle();
        result[0].BatchNo.Should().Be("CK260209078");
        result[0].IsCancelled.Should().BeTrue();
        result[0].ExpectedWorkOrderNo.Should().Be("WO-DELETED-01");
    }

    [Fact]
    public async Task GetSourceOrderChangedBatchesAsync_批次工单号为空_采购单有工单号_提示同步()
    {
        var ctx = CreateDbContext();
        var order = await SeedPurchaseOrderAsync(ctx, "CG_EMPTYWO001", PurchaseOrderStatus.Open, quantity: 100);
        order.SourceWorkOrderNo = "D26Z1104002-X01-01";
        await ctx.SaveChangesAsync();

        await SeedInboundBatchAsync(ctx, "CK260209076", "CG_EMPTYWO001", null,
            workOrderNo: "", warehouseId: 1);

        var svc = CreateService(ctx);
        var result = await svc.GetSourceOrderChangedBatchesAsync();

        result.Should().ContainSingle();
        result[0].BatchNo.Should().Be("CK260209076");
        result[0].ExpectedWorkOrderNo.Should().Be("D26Z1104002-X01-01");
    }

    // ========== 关联工单即时解析（入库更正页点击「关联工单=是」按来源匹配回填） ==========

    private async Task<WorkOrderEntity> SeedWorkOrderAsync(AppDbContext ctx, string workOrderNo, string salesOrderNo, string mainNo)
    {
        var wo = new WorkOrderEntity
        {
            WorkOrderNo = workOrderNo,
            SalesOrderNo = salesOrderNo,
            ProductionMainNo = mainNo,
            ProductionSubNo = "01",
            OrderItemIds = "1",
            Status = WorkOrderStatus.Confirmed,
            StandardCode = "GB/T 8163",
            PlantGrade = "20#",
            Specification = "219*8",
            SignDate = DateTime.Today,
            Salesman = "测试员",
            DeliveryDate = DateTime.Today.AddDays(30)
        };
        ctx.WorkOrders.Add(wo);
        await ctx.SaveChangesAsync();
        return wo;
    }

    [Fact]
    public async Task ResolveLinkedWorkOrderAsync_采购批次_返回工单号订单号主号()
    {
        var ctx = CreateDbContext();
        var order = await SeedPurchaseOrderAsync(ctx, "CG_RESOLVE001", PurchaseOrderStatus.Open, quantity: 100);
        order.SourceWorkOrderNo = "D26Z1104002-X01-01";
        await ctx.SaveChangesAsync();
        await SeedWorkOrderAsync(ctx, "D26Z1104002-X01-01", "SO2604002", "X01");

        var batch = await SeedInboundBatchAsync(ctx, "CK260209100", "CG_RESOLVE001", null, workOrderNo: "", warehouseId: 1);
        batch.InboundSource = InboundSource.Purchase.ToString();
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.ResolveLinkedWorkOrderAsync(batch.Id);

        result.IsValid.Should().BeTrue();
        result.ExpectedWorkOrderNo.Should().Be("D26Z1104002-X01-01");
        result.SalesOrderNo.Should().Be("SO2604002");
        result.ProductionMainNo.Should().Be("X01");
    }

    [Fact]
    public async Task ResolveLinkedWorkOrderAsync_委外批次_按序号返回()
    {
        var ctx = CreateDbContext();
        var subOrder = new SubcontractOrder
        {
            OrderNo = "WW_RESOLVE001",
            SupplierId = 1,
            SupplierName = "委外供应商",
            OrderDate = DateTime.Today,
            ProcessType = "Piercing",
            Status = SubcontractOrderStatus.Sent,
            OutMaterialCategory = "RoundBar",
            OutPlantGrade = "20#",
            OutSpecification = "150*8",
            OutQuantity = 10,
            OutWeight = 1000m
        };
        ctx.SubcontractOrders.Add(subOrder);
        await ctx.SaveChangesAsync();

        ctx.SubcontractReturnItems.Add(new SubcontractReturnItem
        {
            SubcontractOrderId = subOrder.Id,
            Sequence = 1,
            MaterialCategory = "RoughTube",
            ProcessSpecification = "219*8",
            SourceWorkOrderNo = "D26Z1104002-X01-01"
        });
        await ctx.SaveChangesAsync();
        await SeedWorkOrderAsync(ctx, "D26Z1104002-X01-01", "SO2604002", "X01");

        var batch = await SeedInboundBatchAsync(ctx, "CK260209101", "WW_RESOLVE001", sourceOrderSequence: 1,
            workOrderNo: "", warehouseId: 1);
        batch.InboundSource = InboundSource.Subcontract.ToString();
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.ResolveLinkedWorkOrderAsync(batch.Id);

        result.IsValid.Should().BeTrue();
        result.ExpectedWorkOrderNo.Should().Be("D26Z1104002-X01-01");
        result.SalesOrderNo.Should().Be("SO2604002");
        result.ProductionMainNo.Should().Be("X01");
    }

    [Fact]
    public async Task ResolveLinkedWorkOrderAsync_检验入库批次_返回批次工单号订单号主号()
    {
        var ctx = CreateDbContext();
        ctx.ProductionBatches.Add(new ProductionBatch
        {
            BatchNo = "P2608001",
            ManufacturingItem = "Finished",
            WorkOrderNo = "D26Z1104002-X01-01",
            SalesOrderNo = "SO2604002",
            ProductionMainNo = "X01",
            OrderItemIds = "1",
            PlantGrade = "20#",
            Specification = "219*8",
            MaterialName = "无缝钢管",
            Salesman = "测试员",
            SettlementMethod = "Weighing",
            StandardCode = "GB/T 8163",
            DeliveryState = "Hard",
            LengthStatus = "NonFixed",
            TechnicalRequirements = "无",
            Status = BatchStatus.InProgress
        });
        await ctx.SaveChangesAsync();

        var batch = await SeedInboundBatchAsync(ctx, "CK260209102", "", null, workOrderNo: "", warehouseId: 1);
        batch.InboundSource = InboundSource.InspectionInbound.ToString();
        batch.ProductionBatchNo = "P2608001";
        batch.SourceOrderNo = null;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.ResolveLinkedWorkOrderAsync(batch.Id);

        result.IsValid.Should().BeTrue();
        result.ExpectedWorkOrderNo.Should().Be("D26Z1104002-X01-01");
        result.SalesOrderNo.Should().Be("SO2604002");
        result.ProductionMainNo.Should().Be("X01");
    }

    [Fact]
    public async Task ResolveLinkedWorkOrderAsync_来源不支持_返回无效()
    {
        var ctx = CreateDbContext();
        var batch = await SeedInboundBatchAsync(ctx, "CK260209103", "", null, workOrderNo: "", warehouseId: 1);
        batch.InboundSource = InboundSource.Other.ToString();
        batch.SourceOrderNo = null;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.ResolveLinkedWorkOrderAsync(batch.Id);

        result.IsValid.Should().BeFalse();
        result.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ResolveLinkedWorkOrderAsync_采购单未关联工单_工单号为空()
    {
        var ctx = CreateDbContext();
        await SeedPurchaseOrderAsync(ctx, "CG_NOWO001", PurchaseOrderStatus.Open, quantity: 100); // SourceWorkOrderNo 为空

        var batch = await SeedInboundBatchAsync(ctx, "CK260209104", "CG_NOWO001", null, workOrderNo: "", warehouseId: 1);
        batch.InboundSource = InboundSource.Purchase.ToString();
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.ResolveLinkedWorkOrderAsync(batch.Id);

        result.IsValid.Should().BeTrue();
        result.ExpectedWorkOrderNo.Should().BeNullOrEmpty();
    }
}
