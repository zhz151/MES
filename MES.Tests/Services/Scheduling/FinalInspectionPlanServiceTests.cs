using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Data;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Order;
using MES.Data.Entities.Quality;
using MES.Data.Entities.Warehouse;
using WorkOrderEntity = MES.Data.Entities.WorkOrder.WorkOrder;
using MES.Services.Scheduling;
using MES.Tests.Tests;

namespace MES.Tests.Services.Scheduling;

/// <summary>
/// 成检计划服务测试：三档看板（待到料/待检验/检验中）批次归类口径。
/// 候选口径：批次状态 == InFinalInspection（成检）的批次；
/// 行粒度 = 生产编号 + 成检类型（批次「成检附加」InspectionStage，空默认正式成检）；
/// 检验日期/数量按「生产编号+成检类型」匹配。
/// </summary>
public class FinalInspectionPlanServiceTests : TestBase
{
    private static FinalInspectionPlanService CreateService(AppDbContext ctx)
        => new(ctx);

    /// <summary>
    /// 种子一个批次（含工序组），默认模拟「已完成生产、处于成品检验阶段」的 InFinalInspection 字段形态。
    /// </summary>
    private static async Task<ProductionBatch> SeedBatchAsync(AppDbContext ctx, string batchNo,
        BatchStatus status,
        string? inspectionStage = null,
        string? manufacturingStatus = null,
        string lengthStatus = "Fixed",
        bool cutRequirement = false,
        int? theoreticalOutputQty = null,
        int? theoreticalOutputWeight = null,
        decimal? theoreticalUnitWeight = null,
        decimal? productUnitWeight = null,
        string? currentSectionName = null, bool? currentSectionCompleted = null,
        string? nextSectionName = null, string? nextProcess = null,
        string? productionType = null, string? sourceHeatNo = null, string? sourceName = null,
        string? workOrderNo = null, string? salesOrderNo = null, string? productionMainNo = null,
        string? orderItemIds = null,
        params (string ProcessName, int Seq)[] processGroups)
    {
        var batch = new ProductionBatch
        {
            BatchNo = batchNo,
            Status = status,
            WorkOrderNo = workOrderNo ?? "WO-" + batchNo,
            SalesOrderNo = salesOrderNo ?? "SO001",
            ProductionMainNo = productionMainNo ?? "D01",
            OrderItemIds = orderItemIds ?? "1",
            SignDate = DateTime.Today,
            Salesman = "业务员A",
            DeliveryDate = DateTime.Today.AddMonths(1),
            MaterialName = "无缝管",
            SettlementMethod = "Theoretical",
            StandardCode = "GB/T 8163",
            DeliveryState = "SolutionAnnealedAndPickled",
            LengthStatus = lengthStatus,
            ManufacturingItem = "OrderFinished",
            ManufacturingStatus = manufacturingStatus,
            InspectionStage = inspectionStage,
            ProductionType = productionType,
            SourceHeatNo = sourceHeatNo,
            SourceName = sourceName,
            PlantGrade = "304",
            Specification = "219*8",
            TotalQuantity = 50,
            TotalMeters = 600,
            TotalWeight = 1000,
            TotalItemCount = 1,
            TechnicalRequirements = "NORMAL",
            CutRequirement = cutRequirement,
            TheoreticalOutputQty = theoreticalOutputQty,
            TheoreticalOutputWeight = theoreticalOutputWeight,
            TheoreticalUnitWeight = theoreticalUnitWeight,
            ProductUnitWeight = productUnitWeight,
            CurrentSectionName = currentSectionName,
            CurrentSectionCompleted = currentSectionCompleted,
            NextSectionName = nextSectionName,
            NextProcess = nextProcess,
            CurrentValidQty = 50,
            CurrentValidWeight = 1000,
            RowVersion = new byte[8],
            ProcessGroups = processGroups
                .Select(pg => new ProcessGroup { ProcessName = pg.ProcessName, SequenceNumber = pg.Seq })
                .ToList()
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    private static async Task SeedReceiveCheckAsync(AppDbContext ctx, ProductionBatch batch,
        bool isForceCompleted = false, string? inspectionType = null)
    {
        var pg = batch.ProcessGroups.First();
        ctx.MaterialReceiveChecks.Add(new MaterialReceiveCheck
        {
            ProductionBatchId = batch.Id,
            ReceiveDate = DateTime.Today,
            ProcessGroupId = pg.Id,
            ProcessName = SectionKeys.Inspection,
            SequenceNumber = 2,
            IsForceCompleted = isForceCompleted,
            InspectionType = inspectionType
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedFinalInspectionAsync(AppDbContext ctx, ProductionBatch batch,
        string? inspectionType = null, int quantity = 50)
    {
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.PMIInspection,
            InspectionDate = DateTime.Today,
            BatchNo = batch.BatchNo,
            ProductionBatchId = batch.Id,
            InspectionType = inspectionType,
            Quantity = quantity,
            QualifiedQuantity = quantity
        });
        await ctx.SaveChangesAsync();
    }

    /// <summary>种子多条正式成检记录（指定检验项集合）</summary>
    private static async Task SeedFinalInspectionsAsync(AppDbContext ctx, ProductionBatch batch,
        params InspectionItem[] items)
    {
        foreach (var item in items)
        {
            ctx.FinalInspections.Add(new FinalInspection
            {
                InspectionItem = item,
                InspectionDate = DateTime.Today,
                BatchNo = batch.BatchNo,
                ProductionBatchId = batch.Id,
                InspectionType = "FormalInspection",
                Quantity = 50,
                QualifiedQuantity = 50
            });
        }
        await ctx.SaveChangesAsync();
    }

    /// <summary>种子订单项次（Sequence=1）+ 技术要求（10 个成品检验项 4 值枚举；表检/尺寸默认「终」=正式成检）</summary>
    private static async Task SeedRequirementAsync(AppDbContext ctx,
        InspectionRequirementStage surfaceInspection = InspectionRequirementStage.FinalOnly,
        InspectionRequirementStage dimension = InspectionRequirementStage.FinalOnly,
        InspectionRequirementStage eddyCurrent = InspectionRequirementStage.None,
        InspectionRequirementStage ultrasonicTest = InspectionRequirementStage.None,
        InspectionRequirementStage pmiInspection = InspectionRequirementStage.None)
    {
        var orderItem = new OrderItem
        {
            OrderNumber = "SO001",
            Sequence = 1,
            DeliveryDate = DateTime.Today,
            StandardGrade = "304",
            PlantGrade = "304",
            Specification = "219*8",
            SettlementMethod = SettlementMethod.Theoretical,
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            LengthStatus = LengthStatus.Fixed,
            Quantity = 50,
            ContractWeight = 1000,
            TheoreticalWeight = 1000
        };
        ctx.OrderItems.Add(orderItem);
        await ctx.SaveChangesAsync();

        ctx.ProductRequirements.Add(new ProductRequirement
        {
            OrderItemId = orderItem.Id,
            RequirementType = RequirementType.Normal,
            PmiInspection = pmiInspection,
            SurfaceInspection = surfaceInspection,
            Dimension = dimension,
            Endoscopy = InspectionRequirementStage.None,
            HydrostaticTest = InspectionRequirementStage.None,
            UnderwaterPressure = InspectionRequirementStage.None,
            EddyCurrent = eddyCurrent,
            UltrasonicTest = ultrasonicTest,
            PortColoring = InspectionRequirementStage.None
        });
        await ctx.SaveChangesAsync();
    }

    /// <summary>种子指定检验项 + 检验类型（预成检/正式成检）的成检记录</summary>
    private static async Task SeedInspectionAsync(AppDbContext ctx, ProductionBatch batch,
        InspectionItem item, string? inspectionType = null)
    {
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = item,
            InspectionDate = DateTime.Today,
            BatchNo = batch.BatchNo,
            ProductionBatchId = batch.Id,
            InspectionType = inspectionType,
            Quantity = 50,
            QualifiedQuantity = 50
        });
        await ctx.SaveChangesAsync();
    }

    /// <summary>种子工单（OrderItemIds 存项次序号 Sequence 列表，供批次 OrderItemIds 为空时回退关联）</summary>
    private static async Task SeedWorkOrderAsync(AppDbContext ctx, string workOrderNo, string salesOrderNo, string orderItemIds)
    {
        ctx.WorkOrders.Add(new WorkOrderEntity
        {
            WorkOrderNo = workOrderNo,
            SalesOrderNo = salesOrderNo,
            ProductionMainNo = "D01",
            OrderItemIds = orderItemIds,
            Salesman = "业务员A",
            StandardCode = "GB/T 8163",
            PlantGrade = "304",
            Specification = "219*8",
            RowVersion = new byte[8],
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1)
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedCutRecordAsync(AppDbContext ctx, ProductionBatch batch,
        int? postCutQuantity = null, int? quantity = null)
    {
        var pg = batch.ProcessGroups.First();
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = pg.ProcessName,
            SectionName = SectionKeys.Cut,
            SequenceNumber = 2,
            ExecDate = DateTime.Today,
            ProductStatus = ProductStatuses.Finished,
            Quantity = quantity,
            PostCutQuantity = postCutQuantity
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedInboundAsync(AppDbContext ctx, ProductionBatch batch)
    {
        ctx.InventoryBatches.Add(new InventoryBatch
        {
            BatchNo = "IB-" + batch.BatchNo,
            WarehouseId = 1,
            MaterialType = "OrderFinished",
            PlantGrade = "304",
            Specification = "219*8",
            InboundSource = "FinalInspection",
            SourceName = "成品检验",
            InboundDate = DateTime.Today,
            InitialQuantity = 50,
            InitialWeight = 1000,
            RemainingQuantity = 50,
            RemainingWeight = 1000,
            ProductionBatchNo = batch.BatchNo,
            RowVersion = new byte[8]
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task GetKanbanAsync_InFinalInspection批次无到料_归入待到料()
    {
        using var ctx = CreateDbContext();
        await SeedBatchAsync(ctx, "B-FIN-1", BatchStatus.InFinalInspection,
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            productionType: "RoughTube", sourceHeatNo: "HN-001", sourceName: "供应商X",
            workOrderNo: "WO-B-FIN-1", salesOrderNo: "SO001", productionMainNo: "D01",
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        result.Should().HaveCount(1);
        var row = result.Single();
        row.BatchNo.Should().Be("B-FIN-1");
        row.KanbanStage.Should().Be("待到料");
        // 成检附加为空默认正式成检 + 合并字段基础映射
        row.InspectionType.Should().Be(InspectionType.FormalInspection);
        row.InspectionTypeDisplay.Should().Be("正式成检");
        row.ManufacturingItem.Should().Be(MaterialType.OrderFinished);
        row.ManufacturingStatusDisplay.Should().Be("-");
        row.IsDeliveryStatusDisplay.Should().Be("否");
        // 新增批次信息列：生产类型/炉号/来料单位/工单号/订单号/主号
        row.ProductionType.Should().Be(ProductionType.RoughTube);
        row.ProductionTypeDisplay.Should().Be("荒管生产");
        row.SourceHeatNo.Should().Be("HN-001");
        row.SourceName.Should().Be("供应商X");
        row.WorkOrderNo.Should().Be("WO-B-FIN-1");
        row.SalesOrderNo.Should().Be("SO001");
        row.ProductionMainNo.Should().Be("D01");
        row.Salesman.Should().Be("业务员A");
    }

    [Fact]
    public async Task GetKanbanAsync_InFinalInspection批次有到料无检验_归入待检验()
    {
        using var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "B-FIN-2", BatchStatus.InFinalInspection,
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        await SeedReceiveCheckAsync(ctx, batch);

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        result.Should().HaveCount(1);
        var row = result.Single();
        row.BatchNo.Should().Be("B-FIN-2");
        row.KanbanStage.Should().Be("待检验");
        result.Should().NotContain(x => x.KanbanStage == "待到料");
    }

    [Fact]
    public async Task GetKanbanAsync_有检验无到料批次_归入检验中()
    {
        using var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "B-FIN-5", BatchStatus.InFinalInspection,
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        // 无到料记录，但已有检验记录（真实库 6 个批次形态）——必须归入检验中而非待到料
        await SeedFinalInspectionAsync(ctx, batch);

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        result.Should().HaveCount(1);
        var row = result.Single();
        row.KanbanStage.Should().Be("检验中");
        row.ReceiveDate.Should().BeNull();
        row.MaxInspectionDate.Should().Be(DateTime.Today);
        result.Should().NotContain(x => x.KanbanStage == "待到料");
    }

    [Fact]
    public async Task GetKanbanAsync_InProgress批次_不纳入看板()
    {
        using var ctx = CreateDbContext();
        // 批次状态非「成检」，即使下一工段为检验且为最后一道，也不纳入成检计划
        await SeedBatchAsync(ctx, "B-IP-1", BatchStatus.InProgress,
            currentSectionName: SectionKeys.OuterPolish, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Inspection, nextProcess: ProcessKeys.AdditionalFinalInspection,
            processGroups: new[] { (ProcessKeys.RoughTubeProcessing, 1), (ProcessKeys.AdditionalFinalInspection, 2) });

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetKanbanAsync_成检附加预检_预成检且是否交付态制造状态显示减号()
    {
        using var ctx = CreateDbContext();
        // 制造状态==交货状态，底层是否交付态="是"，但预成检统一显示 "-"
        var batch = await SeedBatchAsync(ctx, "B-PRE-1", BatchStatus.InFinalInspection,
            inspectionStage: "PreInspection",
            manufacturingStatus: "SolutionAnnealedAndPickled",
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        await SeedReceiveCheckAsync(ctx, batch, inspectionType: "PreInspection");

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        result.Should().HaveCount(1);
        var row = result.Single();
        row.KanbanStage.Should().Be("待检验");
        row.InspectionType.Should().Be(InspectionType.PreInspection);
        row.InspectionTypeDisplay.Should().Be("预成检");
        row.IsDeliveryStatusDisplay.Should().Be("-");
        row.ManufacturingStatusDisplay.Should().Be("-");
    }

    [Fact]
    public async Task GetKanbanAsync_已入库批次_脱离看板()
    {
        using var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "B-FIN-3", BatchStatus.InFinalInspection,
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        await SeedReceiveCheckAsync(ctx, batch);
        await SeedFinalInspectionAsync(ctx, batch);
        await SeedInboundAsync(ctx, batch);

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetKanbanAsync_强制完成的到料批次_无检验_脱离看板()
    {
        using var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "B-FIN-4", BatchStatus.InFinalInspection,
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        await SeedReceiveCheckAsync(ctx, batch, isForceCompleted: true);

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        // 强制完成 = 到料后执行有特殊情况，属异常完成批次，不属于待到料/待检验/检验中任一档 → 看板主动跳过
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetKanbanAsync_强制完成的到料批次_有检验_仍脱离看板()
    {
        using var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "B-FIN-6", BatchStatus.InFinalInspection,
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        // 即使已产生检验记录，强制完成到料批次仍应脱离看板（避免被误归入「检验中/完成检验待入库」）
        await SeedReceiveCheckAsync(ctx, batch, isForceCompleted: true);
        await SeedFinalInspectionAsync(ctx, batch);

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetKanbanAsync_检验数据按生产编号成检类型匹配()
    {
        using var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "B-TYPE-1", BatchStatus.InFinalInspection,
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        await SeedReceiveCheckAsync(ctx, batch, inspectionType: "FormalInspection");

        // 同批次两条检验记录：预成检(PMI) + 正式成检(表检)，正式成检行只应匹配正式成检记录
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.PMIInspection,
            InspectionDate = DateTime.Today,
            BatchNo = batch.BatchNo,
            ProductionBatchId = batch.Id,
            InspectionType = "PreInspection",
            Quantity = 50,
            QualifiedQuantity = 50
        });
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.VisualInspection,
            InspectionDate = DateTime.Today.AddDays(1),
            BatchNo = batch.BatchNo,
            ProductionBatchId = batch.Id,
            InspectionType = "FormalInspection",
            Quantity = 30,
            QualifiedQuantity = 30
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        result.Should().HaveCount(1);
        var row = result.Single();
        row.KanbanStage.Should().Be("检验中");
        row.InspectionType.Should().Be(InspectionType.FormalInspection);
        row.PmiDate.Should().BeNull();                     // PMI 记录为预成检，不匹配正式成检行
        row.VisualDate.Should().Be(DateTime.Today.AddDays(1));
        row.InspectionCount.Should().Be(1);
        row.TotalQuantity.Should().Be(30);
        row.QualifiedQuantity.Should().Be(30);
    }

    [Fact]
    public async Task GetKanbanAsync_理论合格支_检验支数分组求和取最大减三次品()
    {
        using var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "B-REQ-1", BatchStatus.InFinalInspection,
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        await SeedReceiveCheckAsync(ctx, batch, inspectionType: "FormalInspection");

        // 同项目(表检)两条记录求和 100，另一项目(超声) 100 → 检验支数取最大 100；三次品=10+5+5=20 → 理论合格=80
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.VisualInspection,
            InspectionDate = DateTime.Today,
            BatchNo = batch.BatchNo,
            ProductionBatchId = batch.Id,
            InspectionType = "FormalInspection",
            Quantity = 60,
            QualifiedQuantity = 60,
            DefectReworkQuantity = 10,
            DefectWarehouseQuantity = 5,
            DefectScrapQuantity = 5
        });
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.VisualInspection,
            InspectionDate = DateTime.Today.AddDays(1),
            BatchNo = batch.BatchNo,
            ProductionBatchId = batch.Id,
            InspectionType = "FormalInspection",
            Quantity = 40,
            QualifiedQuantity = 40
        });
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Ultrasonic,
            InspectionDate = DateTime.Today,
            BatchNo = batch.BatchNo,
            ProductionBatchId = batch.Id,
            InspectionType = "FormalInspection",
            Quantity = 100,
            QualifiedQuantity = 100
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        var row = result.Single();
        row.TotalQuantity.Should().Be(100);       // 表检求和 100 = 超声 100 → 取最大 100
        row.DefectReworkQuantity.Should().Be(10);
        row.DefectWarehouseQuantity.Should().Be(5);
        row.DefectScrapQuantity.Should().Be(5);
        row.QualifiedQuantity.Should().Be(80);    // 100 - 20
    }

    [Fact]
    public async Task GetKanbanAsync_理论合格支_次品大于检验支数归零()
    {
        using var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "B-REQ-2", BatchStatus.InFinalInspection,
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        await SeedReceiveCheckAsync(ctx, batch, inspectionType: "FormalInspection");

        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.VisualInspection,
            InspectionDate = DateTime.Today,
            BatchNo = batch.BatchNo,
            ProductionBatchId = batch.Id,
            InspectionType = "FormalInspection",
            Quantity = 10,
            QualifiedQuantity = 10,
            DefectReworkQuantity = 8,
            DefectWarehouseQuantity = 9
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        var row = result.Single();
        row.TotalQuantity.Should().Be(10);
        row.QualifiedQuantity.Should().Be(0);     // 10 - 17 < 0 → 归零
    }

    [Fact]
    public async Task GetKanbanAsync_生产支数重量_定尺按切后支数()
    {
        using var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "B-CUT-1", BatchStatus.InFinalInspection,
            cutRequirement: true, productUnitWeight: 2.5m,
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        await SeedReceiveCheckAsync(ctx, batch);
        await SeedCutRecordAsync(ctx, batch, postCutQuantity: 40);

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        var row = result.Single();
        row.ProductionCutQuantity.Should().Be(40);
        row.ProductionWeight.Should().Be(100m); // 40 * 2.5
    }

    [Fact]
    public async Task GetKanbanAsync_生产支数重量_非定尺按加工支数理论重量()
    {
        using var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "B-CUT-2", BatchStatus.InFinalInspection,
            lengthStatus: "NonFixed", cutRequirement: true, theoreticalOutputWeight: 800,
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        await SeedReceiveCheckAsync(ctx, batch);
        await SeedCutRecordAsync(ctx, batch, quantity: 30);

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        var row = result.Single();
        row.ProductionCutQuantity.Should().Be(30);
        row.ProductionWeight.Should().Be(800m);
    }

    [Fact]
    public async Task GetKanbanAsync_技术要求要求项全部检验_归入完成检验待入库()
    {
        using var ctx = CreateDbContext();
        // 要求项 = 表检+尺寸（恒必检）+ 涡流+超声波（技术要求）= 4 项
        await SeedRequirementAsync(ctx,
            surfaceInspection: InspectionRequirementStage.FinalOnly, dimension: InspectionRequirementStage.FinalOnly,
            eddyCurrent: InspectionRequirementStage.FinalOnly, ultrasonicTest: InspectionRequirementStage.FinalOnly);
        var batch = await SeedBatchAsync(ctx, "B-REQ-1", BatchStatus.InFinalInspection,
            orderItemIds: "1", // 项次序号 Sequence=1（OrderItemIds 存 Sequence 非 OrderItem.Id）
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        await SeedReceiveCheckAsync(ctx, batch);
        // 4 项要求全部检验完毕 → 完成检验待入库
        await SeedFinalInspectionsAsync(ctx, batch,
            InspectionItem.VisualInspection, InspectionItem.Dimension,
            InspectionItem.EddyCurrent, InspectionItem.Ultrasonic);

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        result.Should().HaveCount(1);
        var row = result.Single();
        row.KanbanStage.Should().Be("完成检验待入库");
        // 技术要求检验项填充（含必检项数）
        row.ReqCount.Should().Be(4);
        row.ReqVisual.Should().BeTrue();
        row.ReqDimension.Should().BeTrue();
        row.ReqEddy.Should().BeTrue();
        row.ReqUltrasonic.Should().BeTrue();
        row.ReqPmi.Should().BeFalse();
        row.ReqEndoscopy.Should().BeFalse();
        row.ReqHydro.Should().BeFalse();
        row.ReqUnderwater.Should().BeFalse();
        row.ReqPortColoring.Should().BeFalse();
    }

    [Fact]
    public async Task GetKanbanAsync_技术要求部分检验_仍归入检验中()
    {
        using var ctx = CreateDbContext();
        await SeedRequirementAsync(ctx,
            surfaceInspection: InspectionRequirementStage.FinalOnly, dimension: InspectionRequirementStage.FinalOnly,
            eddyCurrent: InspectionRequirementStage.FinalOnly, ultrasonicTest: InspectionRequirementStage.FinalOnly);
        var batch = await SeedBatchAsync(ctx, "B-REQ-2", BatchStatus.InFinalInspection,
            orderItemIds: "1", // 项次序号 Sequence=1
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        await SeedReceiveCheckAsync(ctx, batch);
        // 只检验了表检+尺寸，涡流+超声波未检 → 要求项 4 ⊄ 已检 2 → 仍检验中
        await SeedFinalInspectionsAsync(ctx, batch,
            InspectionItem.VisualInspection, InspectionItem.Dimension);

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        var row = result.Single();
        row.KanbanStage.Should().Be("检验中");
        row.ReqCount.Should().Be(4);
        result.Should().NotContain(x => x.KanbanStage == "完成检验待入库");
    }

    [Fact]
    public async Task GetKanbanAsync_无技术要求_默认PMI表检尺寸全检_归入完成检验待入库()
    {
        using var ctx = CreateDbContext();
        // 无 OrderItem/ProductRequirement 关联 → 非工单批次兜底：{PMI,表检,尺寸} 正式成检与预成检均要求
        var batch = await SeedBatchAsync(ctx, "B-REQ-3", BatchStatus.InFinalInspection,
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        await SeedReceiveCheckAsync(ctx, batch);
        // PMI+表检+尺寸已检 → 完成检验待入库
        await SeedFinalInspectionsAsync(ctx, batch,
            InspectionItem.PMIInspection, InspectionItem.VisualInspection, InspectionItem.Dimension);

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        var row = result.Single();
        row.KanbanStage.Should().Be("完成检验待入库");
        row.ReqCount.Should().Be(3);
        row.ReqVisual.Should().BeTrue();
        row.ReqDimension.Should().BeTrue();
        row.ReqPmi.Should().BeTrue();
        row.ReqEddy.Should().BeFalse();
    }

    [Fact]
    public async Task GetKanbanAsync_批次OrderItemIds为空_经工单号回退关联技术要求()
    {
        using var ctx = CreateDbContext();
        // 存量形态：批次自身 OrderItemIds 为空，技术要求经「批次工单号 → 工单 OrderItemIds(Sequence) → OrderItem → ProductRequirement」关联
        await SeedRequirementAsync(ctx,
            surfaceInspection: InspectionRequirementStage.FinalOnly, dimension: InspectionRequirementStage.FinalOnly,
            eddyCurrent: InspectionRequirementStage.FinalOnly, ultrasonicTest: InspectionRequirementStage.FinalOnly);
        await SeedWorkOrderAsync(ctx, "WO-B-REQ-4", "SO001", "1");
        var batch = await SeedBatchAsync(ctx, "B-REQ-4", BatchStatus.InFinalInspection,
            orderItemIds: "", // 批次 OrderItemIds 为空 → 回退工单关联
            workOrderNo: "WO-B-REQ-4", salesOrderNo: "SO001",
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        await SeedReceiveCheckAsync(ctx, batch);
        await SeedFinalInspectionsAsync(ctx, batch,
            InspectionItem.VisualInspection, InspectionItem.Dimension,
            InspectionItem.EddyCurrent, InspectionItem.Ultrasonic);

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        var row = result.Single();
        row.KanbanStage.Should().Be("完成检验待入库");
        row.ReqCount.Should().Be(4);
        row.ReqVisual.Should().BeTrue();
        row.ReqDimension.Should().BeTrue();
        row.ReqEddy.Should().BeTrue();
        row.ReqUltrasonic.Should().BeTrue();
        row.ReqPmi.Should().BeFalse();
    }

    // ===== R17：按类型独立判定（「预」只需预成检、「终」只需正式成检、「预+终」两者均需检验，不互相认可）=====

    [Fact]
    public async Task GetKanbanAsync_预加终_预成检已检PMI_正式成检需复检PMI_仍归入检验中()
    {
        using var ctx = CreateDbContext();
        // PMI=预+终 → 预成检与正式成检均要求 PMI；正式行要求项 = {表检,尺寸(终), PMI}
        await SeedRequirementAsync(ctx, pmiInspection: InspectionRequirementStage.PreAndFinal);
        var batch = await SeedBatchAsync(ctx, "B-PRE-FIN-1", BatchStatus.InFinalInspection,
            orderItemIds: "1",
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        await SeedReceiveCheckAsync(ctx, batch);
        // PMI 仅在「预成检」检验过，正式成检只做了表检+尺寸 → 正式行 PMI 需复检 → 仍检验中（不互相认可）
        await SeedInspectionAsync(ctx, batch, InspectionItem.PMIInspection, "PreInspection");
        await SeedInspectionAsync(ctx, batch, InspectionItem.VisualInspection, "FormalInspection");
        await SeedInspectionAsync(ctx, batch, InspectionItem.Dimension, "FormalInspection");

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        var row = result.Single();
        row.KanbanStage.Should().Be("检验中");
        row.ReqCount.Should().Be(3);
        row.ReqPmi.Should().BeTrue();
        row.ReqVisual.Should().BeTrue();
        row.ReqDimension.Should().BeTrue();
        // 日期按「批次+成检类型」填充：PMI 仅有预成检记录，正式行 PMI 日期为空（类型不跨显）
        row.PmiDate.Should().BeNull();
        result.Should().NotContain(x => x.KanbanStage == "完成检验待入库");
    }

    [Fact]
    public async Task GetKanbanAsync_预加终_正式成检仅部分检验_仍归入检验中()
    {
        using var ctx = CreateDbContext();
        await SeedRequirementAsync(ctx, pmiInspection: InspectionRequirementStage.PreAndFinal);
        var batch = await SeedBatchAsync(ctx, "B-PRE-FIN-2", BatchStatus.InFinalInspection,
            orderItemIds: "1",
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        await SeedReceiveCheckAsync(ctx, batch);
        // 正式成检只做了表检（尺寸+PMI 未检）→ 正式行要求项 {表检,尺寸,PMI} ⊄ 已检 {表检} → 检验中
        await SeedInspectionAsync(ctx, batch, InspectionItem.VisualInspection, "FormalInspection");

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        var row = result.Single();
        row.KanbanStage.Should().Be("检验中");
        row.ReqCount.Should().Be(3);
        result.Should().NotContain(x => x.KanbanStage == "完成检验待入库");
    }

    [Fact]
    public async Task GetKanbanAsync_预成检行_不认可正式成检PMI_仍待检验()
    {
        using var ctx = CreateDbContext();
        // PMI=预（PreOnly）→ 预成检行要求项 = {PMI}；表检/尺寸(终)不进入预成检要求
        await SeedRequirementAsync(ctx, pmiInspection: InspectionRequirementStage.PreOnly);
        var batch = await SeedBatchAsync(ctx, "B-PRE-FIN-3", BatchStatus.InFinalInspection,
            inspectionStage: "PreInspection", // 预成检行
            orderItemIds: "1",
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        // 预成检已到料，但 PMI 仅在「正式成检」检验过 → 预成检行不认可正式记录 → 仍待检验
        await SeedReceiveCheckAsync(ctx, batch, inspectionType: "PreInspection");
        await SeedInspectionAsync(ctx, batch, InspectionItem.PMIInspection, "FormalInspection");

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        var row = result.Single();
        row.KanbanStage.Should().Be("待检验");
        row.ReqCount.Should().Be(1);
        row.ReqPmi.Should().BeTrue();
        row.ReqVisual.Should().BeFalse();
        row.ReqDimension.Should().BeFalse();
        result.Should().NotContain(x => x.KanbanStage == "完成检验待入库");
    }

    [Fact]
    public async Task GetKanbanAsync_预成检行_预成检已检PMI_归入完成检验待入库()
    {
        using var ctx = CreateDbContext();
        // PMI=预（PreOnly）→ 预成检行要求项 = {PMI}；预成检自己做 PMI → 完成检验待入库（「预」仅需预成检）
        await SeedRequirementAsync(ctx, pmiInspection: InspectionRequirementStage.PreOnly);
        var batch = await SeedBatchAsync(ctx, "B-PRE-FIN-5", BatchStatus.InFinalInspection,
            inspectionStage: "PreInspection", // 预成检行
            orderItemIds: "1",
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        await SeedReceiveCheckAsync(ctx, batch, inspectionType: "PreInspection");
        await SeedInspectionAsync(ctx, batch, InspectionItem.PMIInspection, "PreInspection");

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        var row = result.Single();
        row.KanbanStage.Should().Be("完成检验待入库");
        row.ReqCount.Should().Be(1);
        row.ReqPmi.Should().BeTrue();
        row.ReqVisual.Should().BeFalse();
        row.ReqDimension.Should().BeFalse();
        // 预成检行 PMI 日期取预成检记录
        row.PmiDate.Should().NotBeNull();
    }

    [Fact]
    public async Task GetKanbanAsync_预加终_预终均未做PMI_正式成检行归入检验中()
    {
        using var ctx = CreateDbContext();
        await SeedRequirementAsync(ctx, pmiInspection: InspectionRequirementStage.PreAndFinal);
        var batch = await SeedBatchAsync(ctx, "B-PRE-FIN-4", BatchStatus.InFinalInspection,
            orderItemIds: "1",
            currentSectionName: SectionKeys.Inspection, currentSectionCompleted: true,
            nextSectionName: SectionKeys.Warehouse, nextProcess: ProcessKeys.InProcessRepair,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1), (ProcessKeys.AdditionalFinalInspection, 2) });
        await SeedReceiveCheckAsync(ctx, batch);
        // 预成检与正式均未做 PMI → 正式行要求项 {表检,尺寸,PMI} ⊄ 已检 {表检,尺寸} → 检验中
        await SeedInspectionAsync(ctx, batch, InspectionItem.VisualInspection, "FormalInspection");
        await SeedInspectionAsync(ctx, batch, InspectionItem.Dimension, "FormalInspection");

        var svc = CreateService(ctx);
        var result = await svc.GetKanbanAsync();

        var row = result.Single();
        row.KanbanStage.Should().Be("检验中");
        row.ReqPmi.Should().BeTrue();
        result.Should().NotContain(x => x.KanbanStage == "完成检验待入库");
    }

    // ========== 待检批支重汇总（行=检验项，列=待到料/待检验/检验中/汇总数据；
    // 统计要求该检验项且尚未完成该检验的看板批次，每列批次数/生产支数/生产重量，预+正式合并、按批次去重） ==========

    [Fact]
    public async Task GetSummaryAsync_待检量_已检检验项不纳入_按看板状态分列()
    {
        using var ctx = CreateDbContext();
        // A：待到料（无到料无检验），无技术要求 → 兜底 {PMI,表检,尺寸}，全部未检
        await SeedBatchAsync(ctx, "B-SUM-A", BatchStatus.InFinalInspection,
            orderItemIds: "1", salesOrderNo: "SO001",
            theoreticalOutputQty: 100, productUnitWeight: 80m);
        // B：检验中（正式成检只做了表检），PMI/尺寸未检 → PMI 计入「检验中」；表检已检不计入
        var b = await SeedBatchAsync(ctx, "B-SUM-B", BatchStatus.InFinalInspection,
            orderItemIds: "1", salesOrderNo: "SO002",
            theoreticalOutputQty: 50, productUnitWeight: 60m);
        await SeedInspectionAsync(ctx, b, InspectionItem.VisualInspection);
        // C：待检验（有到料无检验），全部未检
        var c = await SeedBatchAsync(ctx, "B-SUM-C", BatchStatus.InFinalInspection,
            orderItemIds: "1", salesOrderNo: "SO003",
            theoreticalOutputQty: 80, productUnitWeight: 70m,
            processGroups: new[] { (ProcessKeys.InProcessRepair, 1) });
        await SeedReceiveCheckAsync(ctx, c);
        // D：完成检验待入库（PMI+表检+尺寸全检，未入库）→ 全部已检，不计入任何检验项
        var d = await SeedBatchAsync(ctx, "B-SUM-D", BatchStatus.InFinalInspection,
            orderItemIds: "1", salesOrderNo: "SO004",
            theoreticalOutputQty: 120, productUnitWeight: 100m);
        await SeedInspectionAsync(ctx, d, InspectionItem.PMIInspection);
        await SeedInspectionAsync(ctx, d, InspectionItem.VisualInspection);
        await SeedInspectionAsync(ctx, d, InspectionItem.Dimension);

        var svc = CreateService(ctx);
        var rows = await svc.GetSummaryAsync();

        // PMI：待到料=A、待检验=C、检验中=B（D 完成检验待入库已检不计入）→ 汇总 3 批 / 230 支 / 16600kg
        var pmi = rows.Single(r => r.InspectionItemName == "PMI检验");
        pmi.WaitingMaterialCount.Should().Be(1);
        pmi.WaitingMaterialQuantity.Should().Be(100);
        pmi.WaitingMaterialWeight.Should().Be(8000m);
        pmi.WaitingInspectionCount.Should().Be(1);
        pmi.WaitingInspectionQuantity.Should().Be(80);
        pmi.WaitingInspectionWeight.Should().Be(5600m);
        pmi.InspectingCount.Should().Be(1);
        pmi.InspectingQuantity.Should().Be(50);
        pmi.InspectingWeight.Should().Be(3000m);
        pmi.TotalCount.Should().Be(3);
        pmi.TotalQuantity.Should().Be(230);
        pmi.TotalWeight.Should().Be(16600m);

        // 表检：待到料=A、待检验=C；检验中=0（B 表检已检，不纳入待检）→ 汇总 2 批 / 180 支 / 13600kg
        var visual = rows.Single(r => r.InspectionItemName == "表检");
        visual.InspectingCount.Should().Be(0);
        visual.TotalCount.Should().Be(2);
        visual.TotalQuantity.Should().Be(180);
        visual.TotalWeight.Should().Be(13600m);

        // 无要求项恒 0
        var eddy = rows.Single(r => r.InspectionItemName == "涡流");
        eddy.TotalCount.Should().Be(0);
        eddy.TotalQuantity.Should().Be(0);
        eddy.TotalWeight.Should().Be(0);
    }

    [Fact]
    public async Task GetSummaryAsync_预成检与正式成检批次_待检合并按各自要求项统计()
    {
        using var ctx = CreateDbContext();
        // PMI=预+终 → 预成检批次要求 {PMI}；正式成检批次要求 {表检,尺寸,PMI}
        await SeedRequirementAsync(ctx, pmiInspection: InspectionRequirementStage.PreAndFinal);
        await SeedBatchAsync(ctx, "B-SUM-PRE", BatchStatus.InFinalInspection,
            inspectionStage: "PreInspection",
            orderItemIds: "1", salesOrderNo: "SO001",
            theoreticalOutputQty: 100, productUnitWeight: 80m);
        await SeedBatchAsync(ctx, "B-SUM-FORMAL", BatchStatus.InFinalInspection,
            orderItemIds: "1", salesOrderNo: "SO001",
            theoreticalOutputQty: 200, productUnitWeight: 90m);

        var svc = CreateService(ctx);
        var rows = await svc.GetSummaryAsync();

        // PMI：预+正式两批都要求且未检 → 待到料合并 2 批 / 300 支 / 26000kg
        var pmi = rows.Single(r => r.InspectionItemName == "PMI检验");
        pmi.WaitingMaterialCount.Should().Be(2);
        pmi.WaitingMaterialQuantity.Should().Be(300);
        pmi.WaitingMaterialWeight.Should().Be(26000m);
        pmi.TotalCount.Should().Be(2);
        pmi.TotalQuantity.Should().Be(300);
        pmi.TotalWeight.Should().Be(26000m);
        // 表检：仅正式批次要求（表检=终，不入预成检）→ 待到料 1 批 / 200 支 / 18000kg
        var visual = rows.Single(r => r.InspectionItemName == "表检");
        visual.WaitingMaterialCount.Should().Be(1);
        visual.WaitingMaterialQuantity.Should().Be(200);
        visual.WaitingMaterialWeight.Should().Be(18000m);
        visual.TotalCount.Should().Be(1);
    }
}
