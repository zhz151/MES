using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Models;
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
using MES.Services;
using MES.Services.Batch;
using MES.Tests.Tests;
using Moq;


using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Quality;
using MES.Data.Entities.Warehouse;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Tests.Services;

/// <summary>
/// 生产记录服务测试：生产记录、工段委外、委外回收
/// </summary>
public class ProductionRecordServiceTests : TestBase
{
    private ProductionRecordService CreateService(AppDbContext ctx, IFixedLengthWorkOrderService? fixedLengthSvc = null)
    {
        var mockDaySvc = new Mock<IStandardWorkDayService>();
        mockDaySvc.Setup(s => s.GetStandardDaysMapAsync(It.IsAny<string?>()))
            .ReturnsAsync(new Dictionary<string, double>());
        var mockDsSvc = new Mock<IStandardWorkDayDeliveryStateService>();
        mockDsSvc.Setup(s => s.GetDeliveryStateExtraDaysMapAsync())
            .ReturnsAsync(new Dictionary<string, double>());
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        var qptMock = new Mock<IQualityProcessTrackingService>();
        return new(ctx,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProductionRecordService>.Instance,
            mockDaySvc.Object,
            mockDsSvc.Object,
            configMock.Object,
            qptMock.Object,
            Mock.Of<IWorkOrderExecutionService>(),
            fixedLengthSvc ?? Mock.Of<IFixedLengthWorkOrderService>(),
            Mock.Of<ISectionNameDisplayService>(),
            CreateProcessDefinitionServiceMock(),
            new MemoryCache(new MemoryCacheOptions()));
    }

    /// <summary>
    /// 构造一个定尺长度集合可配置的 IFixedLengthWorkOrderService Mock。
    /// 默认返回空集合（等价于非定尺主号，跳过校验）。
    /// </summary>
    private static IFixedLengthWorkOrderService CreateFixedLengthSvcMock(params decimal[] lengths)
    {
        var mock = new Mock<IFixedLengthWorkOrderService>();
        mock.Setup(s => s.GetLengthsByMainNoAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new HashSet<decimal>(lengths));
        return mock.Object;
    }

    private async Task<ProductionBatch> SeedBatchAsync(AppDbContext ctx, string batchNo = "BATCH001")
    {
        var batch = new ProductionBatch
        {
            BatchNo = batchNo,
            MaterialName = "不锈钢管",
            PlantGrade = "304",
            Specification = "219*8",
            Status = BatchStatus.InProgress,
            ProductionType = "Internal",
            ManufacturingItem = "OrderFinished",
            WorkOrderNo = "WO-001",
            SalesOrderNo = "SO-001",
            ProductionMainNo = "M-001",
            OrderItemIds = "1",
            Salesman = "张三",
            SettlementMethod = "Weighing",
            StandardCode = "GB/T 14976",
            DeliveryState = "Hard",
            LengthStatus = "NonFixed",
            TechnicalRequirements = "无",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            OuterDiameterNegative = 0.5m,
            OuterDiameterPositive = 0.5m,
            WallThicknessNegative = 0.3m,
            WallThicknessPositive = 0.3m,
            TotalQuantity = 100,
            TotalMeters = 1000m,
            TotalWeight = 5000m,
            TotalItemCount = 1
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    private async Task<ProcessGroup> SeedProcessGroupAsync(AppDbContext ctx, int batchId,
        string processName = "60冷轧", string mfgSpec = "219*8")
    {
        var pg = new ProcessGroup
        {
            ProductionBatchId = batchId,
            SequenceNumber = 1,
            ProcessName = processName,
            ManufacturingSpec = mfgSpec,
            ColdRollDraw = 1
        };
        ctx.ProcessGroups.Add(pg);
        await ctx.SaveChangesAsync();
        return pg;
    }

    // ========== 内部生产记录 ==========

    [Fact]
    public async Task GetProductionRecordsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetProductionRecordsAsync(batch.Id, new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateProductionRecordAsync_成功创建()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        var result = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            ExecDate = DateTime.Today,
            Quantity = 10,
            Weight = 1000m
        });

        result.Should().NotBeNull();
        result.Quantity.Should().Be(10);
        // 验证数据库已保存
        var saved = await ctx.ProductionRecords.FirstAsync(r => r.Id == result.Id);
        saved.Quantity.Should().Be(10);
    }

    [Fact]
    public async Task CreateProductionRecordAsync_批次不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "NONEXISTENT",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            ExecDate = DateTime.Today
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task UpdateProductionRecordAsync_成功更新()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        var created = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            ExecDate = DateTime.Today,
            Quantity = 10
        });

        var result = await svc.UpdateProductionRecordAsync(created.Id, new UpdateProductionRecordRequest
        {
            ExecDate = DateTime.Today,
            Quantity = 15,
            Weight = 1500m
        });

        result.Quantity.Should().Be(15);
        result.Weight.Should().Be(1500m);
    }

    [Fact]
    public async Task UpdateProductionRecordAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateProductionRecordAsync(999, new UpdateProductionRecordRequest { ExecDate = DateTime.Today });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task CreateProductionRecordAsync_定尺切割长度不在集合_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var act = () => svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today,
            FinishedCutLength = 6000m
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*成品切割长度(6000mm)不属于该订单号+主号(SO-001/M-001)下的定尺长度*");
    }

    [Fact]
    public async Task CreateProductionRecordAsync_定尺切割长度在集合_成功创建()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var result = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today,
            FinishedCutLength = 4000m
        });

        result.Should().NotBeNull();
        result.FinishedCutLength.Should().Be(4000m);
    }

    [Fact]
    public async Task CreateProductionRecordAsync_非定尺主号_跳过校验()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx, CreateFixedLengthSvcMock()); // 空集合 = 非定尺，跳过校验

        var result = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today,
            FinishedCutLength = 6000m
        });

        result.Should().NotBeNull();
        result.FinishedCutLength.Should().Be(6000m);
    }

    [Fact]
    public async Task UpdateProductionRecordAsync_定尺切割长度不在集合_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var created = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today,
            FinishedCutLength = 4000m
        });

        var act = () => svc.UpdateProductionRecordAsync(created.Id, new UpdateProductionRecordRequest
        {
            ExecDate = DateTime.Today,
            FinishedCutLength = 6000m
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*成品切割长度(6000mm)不属于该订单号+主号(SO-001/M-001)下的定尺长度*");
    }

    // ========== 预成切 ==========

    [Fact]
    public async Task CreateProductionRecordAsync_预成切长度属于定尺集合_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var act = () => svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today,
            FinishedCutLength = 4000m,
            IsPreCut = true
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*预成切长度(4000mm)不能属于该订单号+主号(SO-001/M-001)下的正式定尺长度*");
    }

    [Fact]
    public async Task CreateProductionRecordAsync_预成切长度不在定尺集合_成功创建()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var result = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today,
            FinishedCutLength = 6000m,
            IsPreCut = true
        });

        result.Should().NotBeNull();
        result.IsPreCut.Should().BeTrue();
        result.FinishedCutLength.Should().Be(6000m);
    }

    [Fact]
    public async Task CreateProductionRecordAsync_预成切非定尺主号_跳过校验()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx, CreateFixedLengthSvcMock()); // 空集合 = 非定尺，跳过校验

        var result = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today,
            FinishedCutLength = 6000m,
            IsPreCut = true
        });

        result.Should().NotBeNull();
        result.IsPreCut.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateProductionRecordAsync_改为预成切且长度属于定尺集合_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var created = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today,
            FinishedCutLength = 4000m
        });

        var act = () => svc.UpdateProductionRecordAsync(created.Id, new UpdateProductionRecordRequest
        {
            ExecDate = DateTime.Today,
            IsPreCut = true,
            FinishedCutLength = 4000m
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*预成切长度(4000mm)不能属于该订单号+主号(SO-001/M-001)下的正式定尺长度*");
    }

    [Fact]
    public async Task UpdateProductionRecordAsync_改为预成切且长度不在定尺集合_成功()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var created = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today,
            FinishedCutLength = 4000m
        });

        var result = await svc.UpdateProductionRecordAsync(created.Id, new UpdateProductionRecordRequest
        {
            ExecDate = DateTime.Today,
            IsPreCut = true,
            FinishedCutLength = 6000m
        });

        result.IsPreCut.Should().BeTrue();
        result.FinishedCutLength.Should().Be(6000m);
    }

    [Fact]
    public async Task CreateProductionRecordAsync_预成切非断切工段_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var act = () => svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw, // 非断切工段
            ExecDate = DateTime.Today,
            FinishedCutLength = 6000m,
            IsPreCut = true
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*预成切必须是断切工段*");
    }

    [Fact]
    public async Task CreateProductionRecordAsync_预成切断切工段未填长度_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx, CreateFixedLengthSvcMock());

        var act = () => svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today,
            IsPreCut = true
            // 未填 FinishedCutLength
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*预成切必须填写成品长度*");
    }

    [Fact]
    public async Task UpdateProductionRecordAsync_预成切未填长度_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        // 创建断切记录但无成品长度（长度为空，长度校验跳过）
        var created = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today
        });

        var act = () => svc.UpdateProductionRecordAsync(created.Id, new UpdateProductionRecordRequest
        {
            ExecDate = DateTime.Today,
            IsPreCut = true
            // 未传 FinishedCutLength，生效值仍为空
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*预成切必须填写成品长度*");
    }

    [Fact]
    public async Task DeleteProductionRecordAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        var created = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            ExecDate = DateTime.Today
        });

        await svc.DeleteProductionRecordAsync(created.Id);

        var deleted = await ctx.ProductionRecords.FindAsync(created.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProductionRecordAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteProductionRecordAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== 跨批次查询 ==========

    [Fact]
    public async Task GetAllProductionRecordsAsync_无数据_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetAllProductionRecordsAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().BeEmpty();
    }

    // ========== 委外回收 ==========

    // ========== B11 专项测试 ==========

    [Fact]
    public async Task GetAllProductionRecordsAsync_关键词搜索制造规格_返回匹配()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-REC-SPEC");
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH-REC-SPEC",
            ProcessName = "60冷轧",
            ManufacturingSpec = "273*10",
            SectionName = SectionKeys.ColdRollDraw,
            ExecDate = DateTime.Today,
            Quantity = 10,
            Weight = 1000m
        });

        var result = await svc.GetAllProductionRecordsAsync(new QueryParams
        { PageIndex = 1, PageSize = 20, Keyword = "273*10" });

        result.Items.Should().HaveCount(1);
        result.Items[0].ManufacturingSpec.Should().Be("273*10");
    }

    [Fact]
    public async Task GetAllProductionRecordsAsync_关键词搜索备注_返回匹配()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-REC-REM");
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH-REC-REM",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            ExecDate = DateTime.Today,
            Quantity = 10,
            Weight = 1000m,
            Remark = "生产记录备注测试"
        });

        var result = await svc.GetAllProductionRecordsAsync(new QueryParams
        { PageIndex = 1, PageSize = 20, Keyword = "生产记录备注" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Remark.Should().Be("生产记录备注测试");
    }

    // ========== 筛选测试（FilterDescriptor） ==========

    [Fact]
    public async Task GetAllProductionRecordsAsync_Filters_ProcessNameContains_返回匹配()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH001");
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            ExecDate = DateTime.Today,
            Quantity = 10
        });
        await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "冷拔",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            ExecDate = DateTime.Today,
            Quantity = 20
        });

        var result = await svc.GetAllProductionRecordsAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "ProcessName", Operator = "contains", Value = "冷轧" }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].ProcessName.Should().Be("60冷轧");
    }

    [Fact]
    public async Task GetAllProductionRecordsAsync_Filters_SectionNameIn_返回匹配()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH001");
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            ExecDate = DateTime.Today,
            Quantity = 10
        });

        var result = await svc.GetAllProductionRecordsAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "SectionName", Operator = "in", Values = new List<string> { SectionKeys.ColdRollDraw } }
            }
        });

        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllProductionRecordsAsync_Filters_NoMatch_返回空列表()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH001");
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            ExecDate = DateTime.Today,
            Quantity = 10
        });

        var result = await svc.GetAllProductionRecordsAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "ProcessName", Operator = "contains", Value = "NONEXISTENT" }
            }
        });

        result.Items.Should().BeEmpty();
    }

    // ========== GetFilterContextsAsync ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH001");
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            ExecDate = DateTime.Today,
            Quantity = 10
        });

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("BatchNo");
        contexts["BatchNo"].Should().Contain("BATCH001");
        contexts.Should().ContainKey("ProcessName");
        contexts["ProcessName"].Should().Contain("60冷轧");
        contexts["SectionName"].Should().Contain(SectionKeys.ColdRollDraw);
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["BatchNo"].Should().BeEmpty();
        contexts["ProcessName"].Should().BeEmpty();
        contexts["SectionName"].Should().BeEmpty();
    }

    [Fact]
    public async Task GetFilterContextsAsync_Nullable字段排除null()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH001");
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        // 创建一条只有必要字段的记录，可选字段为null
        await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            ExecDate = DateTime.Today,
            Quantity = 10,
            EquipmentName = null,
            Operator = null,
            Shift = null
        });

        var contexts = await svc.GetFilterContextsAsync();

        contexts["BatchNo"].Should().HaveCount(1);
        contexts["Operator"].Should().BeEmpty();
    }

    // ========== 成切跟踪四字段 ==========

    /// <summary>
    /// 构造：批次 + 成品工序组（ManufacturingSpec==批次Specification 且含断切工段）+ 可选断切生产记录
    /// </summary>
    private async Task<ProductionBatch> SeedCutBatchAsync(AppDbContext ctx, int? cutQty = 100,
        int? processQty = null, bool withCutRecord = true, bool addAdditionalInspection = false,
        string batchNo = "BATCH-CUT", string lengthStatus = "Fixed")
    {
        var batch = await SeedBatchAsync(ctx, batchNo);
        batch.ProductionRatio = 1;
        batch.CurrentValidQty = 100;
        batch.LengthStatus = lengthStatus;
        await ctx.SaveChangesAsync();

        var pg = new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 1,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8", // == batch.Specification
            Cut = 5
        };
        ctx.ProcessGroups.Add(pg);
        await ctx.SaveChangesAsync();

        if (addAdditionalInspection)
        {
            // 附加成检：末位工序组（Spec 相同但无断切工段），不得误导成切需求判定
            ctx.ProcessGroups.Add(new ProcessGroup
            {
                ProductionBatchId = batch.Id,
                SequenceNumber = 2,
                ProcessName = ProcessNames.AdditionalFinalInspection,
                ManufacturingSpec = "219*8",
                Inspection = 8
            });
            await ctx.SaveChangesAsync();
        }

        if (withCutRecord)
        {
            ctx.ProductionRecords.Add(new ProductionRecord
            {
                ProductionBatchId = batch.Id,
                ProcessGroupId = pg.Id,
                ProcessName = "60冷轧",
                ManufacturingSpec = "219*8",
                SectionName = SectionKeys.Cut,
                SequenceNumber = 5,
                ExecDate = DateTime.Today,
                ProductStatus = ProductStatuses.Finished,
                PostCutQuantity = cutQty,
                Quantity = processQty
            });
            await ctx.SaveChangesAsync();
        }

        return batch;
    }

    [Fact]
    public async Task RefreshCutTracking_成切需求是_有断切记录_偏差0_正常()
    {
        var ctx = CreateDbContext();
        var batch = await SeedCutBatchAsync(ctx, cutQty: 100);
        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.CutRequirement.Should().BeTrue();
        refreshed.CutExecution.Should().BeTrue();
        refreshed.CutQuantity.Should().Be(100);
        refreshed.CutDoubt.Should().Be(CutDoubtType.Normal); // |100-100|/100 = 0% ≤ 5% → 正常
    }

    [Fact]
    public async Task RefreshCutTracking_预成切记录不计入成切支数()
    {
        var ctx = CreateDbContext();
        var batch = await SeedCutBatchAsync(ctx, cutQty: 100); // 正式断切记录 PostCutQuantity=100

        // 再添加一条预成切断切记录（成品，PostCutQuantity=50，IsPreCut=true）
        var pgId = await ctx.ProcessGroups.Where(p => p.ProductionBatchId == batch.Id).Select(p => p.Id).FirstAsync();
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pgId,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            SequenceNumber = 6,
            ExecDate = DateTime.Today,
            ProductStatus = "成品",
            PostCutQuantity = 50,
            Quantity = 10,
            IsPreCut = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.CutRequirement.Should().BeTrue();
        refreshed.CutExecution.Should().BeTrue(); // 有正式断切记录 → 已执行成切
        refreshed.CutQuantity.Should().Be(100);   // 只计正式断切 100，预成切 50 不计入
        refreshed.CutDoubt.Should().Be(CutDoubtType.Normal);
    }

    [Fact]
    public async Task RefreshCutTracking_仅有预成切记录_不算已成切()
    {
        var ctx = CreateDbContext();
        var batch = await SeedCutBatchAsync(ctx, withCutRecord: false); // 无正式断切记录
        var pgId = await ctx.ProcessGroups.Where(p => p.ProductionBatchId == batch.Id).Select(p => p.Id).FirstAsync();
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pgId,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            SequenceNumber = 5,
            ExecDate = DateTime.Today,
            ProductStatus = "成品",
            PostCutQuantity = 50,
            Quantity = 10,
            IsPreCut = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.CutRequirement.Should().BeTrue();
        refreshed.CutExecution.Should().BeFalse(); // 仅有预成切，不算已成切
        refreshed.CutQuantity.Should().BeNull();   // 预成切不计入成切支数
        refreshed.CutDoubt.Should().BeNull();      // 状态在产，未到成检/完成 → 略
    }

    [Fact]
    public async Task RefreshCutTracking_仅有预成切记录到预检验_判正常()
    {
        var ctx = CreateDbContext();
        var batch = await SeedCutBatchAsync(ctx, withCutRecord: false, batchNo: "BATCH-PRECUT-MISS");
        // 预成切断切记录（成品，IsPreCut=true）
        var pg = await ctx.ProcessGroups.FirstAsync(p => p.ProductionBatchId == batch.Id);
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            SequenceNumber = 5,
            ExecDate = DateTime.Today,
            ProductStatus = "成品",
            PostCutQuantity = 50,
            Quantity = 10,
            IsPreCut = true
        });
        // 预成检到料 → 刷新时 hasMaterialCheck=true，批次状态保持"成检"
        ctx.MaterialReceiveChecks.Add(new MaterialReceiveCheck
        {
            ProductionBatchId = batch.Id,
            ReceiveDate = DateTime.Today,
            ProcessGroupId = pg.Id,
            ProcessName = "检验",
            SequenceNumber = 1,
            BatchNo = batch.BatchNo,
            InspectionType = nameof(InspectionType.PreInspection)
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.Status.Should().Be(BatchStatus.InFinalInspection);
        refreshed.CutRequirement.Should().BeTrue();
        refreshed.CutExecution.Should().BeFalse(); // 仅有预成切 → 不算已成切
        refreshed.CutQuantity.Should().BeNull();   // 预成切不计入成切支数
        refreshed.InspectionStage.Should().Be(nameof(InspectionType.PreInspection));
        // 成检+成检附加=预检：正式成切留待正式成检，非"缺失" → 正常
        refreshed.CutDoubt.Should().Be(CutDoubtType.Normal);
    }

    [Fact]
    public async Task RefreshCutTracking_普通批次仅预检到料_执行否_判正常()
    {
        var ctx = CreateDbContext();
        var batch = await SeedCutBatchAsync(ctx, withCutRecord: false, batchNo: "BATCH-PRECUT-ONLYPRE");
        // 无任何断切记录（无预成切、无正式），仅预成检到料
        var pg = await ctx.ProcessGroups.FirstAsync(p => p.ProductionBatchId == batch.Id);
        ctx.MaterialReceiveChecks.Add(new MaterialReceiveCheck
        {
            ProductionBatchId = batch.Id,
            ReceiveDate = DateTime.Today,
            ProcessGroupId = pg.Id,
            ProcessName = "检验",
            SequenceNumber = 1,
            BatchNo = batch.BatchNo,
            InspectionType = nameof(InspectionType.PreInspection)
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.Status.Should().Be(BatchStatus.InFinalInspection);
        refreshed.CutRequirement.Should().BeTrue();
        refreshed.CutExecution.Should().BeFalse();
        refreshed.CutQuantity.Should().BeNull();
        refreshed.InspectionStage.Should().Be(nameof(InspectionType.PreInspection));
        // 成检+成检附加=预检、执行=否 → 正常
        refreshed.CutDoubt.Should().Be(CutDoubtType.Normal);
    }

    [Fact]
    public async Task RefreshCutTracking_仅有预成切记录到正式成检_判疑问缺少()
    {
        var ctx = CreateDbContext();
        var batch = await SeedCutBatchAsync(ctx, withCutRecord: false, batchNo: "BATCH-PRECUT-FORMAL");
        // 预成切断切记录（成品，IsPreCut=true）
        var pg = await ctx.ProcessGroups.FirstAsync(p => p.ProductionBatchId == batch.Id);
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            SequenceNumber = 5,
            ExecDate = DateTime.Today,
            ProductStatus = "成品",
            PostCutQuantity = 50,
            Quantity = 10,
            IsPreCut = true
        });
        // 正式成检到料：已进入正式成检却没有正式成切记录 → 疑问-缺少
        ctx.MaterialReceiveChecks.Add(new MaterialReceiveCheck
        {
            ProductionBatchId = batch.Id,
            ReceiveDate = DateTime.Today,
            ProcessGroupId = pg.Id,
            ProcessName = "检验",
            SequenceNumber = 1,
            BatchNo = batch.BatchNo,
            InspectionType = nameof(InspectionType.FormalInspection)
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.Status.Should().Be(BatchStatus.InFinalInspection);
        refreshed.CutRequirement.Should().BeTrue();
        refreshed.CutExecution.Should().BeFalse(); // 仅有预成切 → 不算已成切
        refreshed.CutQuantity.Should().BeNull();   // 预成切不计入成切支数
        refreshed.InspectionStage.Should().Be(nameof(InspectionType.FormalInspection));
        // 预成切批次但已到正式成检：缺正式成切仍属"缺失" → 疑问-缺少
        refreshed.CutDoubt.Should().Be(CutDoubtType.MissingRecords);
    }

    [Fact]
    public async Task RefreshCutTracking_成切支数偏差超5_存疑_疑问()
    {
        var ctx = CreateDbContext();
        var batch = await SeedCutBatchAsync(ctx, cutQty: 90);
        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.CutRequirement.Should().BeTrue();
        refreshed.CutExecution.Should().BeTrue();
        refreshed.CutQuantity.Should().Be(90);
        refreshed.CutDoubt.Should().Be(CutDoubtType.QuantityMismatch); // |90-100|/100 = 10% > 5% → 疑问-数量
    }

    [Fact]
    public async Task RefreshCutTracking_成切需求否_四字段全部略()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-NOCUT");
        batch.ProductionRatio = 1;
        batch.CurrentValidQty = 100;
        await ctx.SaveChangesAsync();
        // 工序组 Spec 不匹配成品规格 → 非成品工序组
        await SeedProcessGroupAsync(ctx, batch.Id, mfgSpec: "OTHER-SPEC");
        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.CutRequirement.Should().BeFalse();
        refreshed.CutExecution.Should().BeNull();
        refreshed.CutQuantity.Should().BeNull();
        refreshed.CutDoubt.Should().BeNull();
    }

    [Fact]
    public async Task RefreshCutTracking_成切执行否_状态在产_未到成检完成_略()
    {
        var ctx = CreateDbContext();
        var batch = await SeedCutBatchAsync(ctx, withCutRecord: false);
        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.CutRequirement.Should().BeTrue();
        refreshed.CutExecution.Should().BeFalse();
        refreshed.CutQuantity.Should().BeNull();
        // 需求=是、执行=否，但状态仍在产（未到成检/完成）→ 略
        refreshed.CutDoubt.Should().BeNull();
    }

    [Fact]
    public async Task RefreshCutTracking_执行否已达成检_疑问缺少()
    {
        var ctx = CreateDbContext();
        var batch = await SeedCutBatchAsync(ctx, withCutRecord: false, batchNo: "BATCH-CUT-MISS");
        // 成检到料 → 刷新时 hasMaterialCheck=true，批次状态保持"成检"（不被覆盖为在产）
        var pg = await ctx.ProcessGroups.FirstAsync(p => p.ProductionBatchId == batch.Id);
        ctx.MaterialReceiveChecks.Add(new MaterialReceiveCheck
        {
            ProductionBatchId = batch.Id,
            ReceiveDate = DateTime.Today,
            ProcessGroupId = pg.Id,
            ProcessName = "检验",
            SequenceNumber = 1,
            BatchNo = batch.BatchNo
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.Status.Should().Be(BatchStatus.InFinalInspection);
        refreshed.CutRequirement.Should().BeTrue();
        refreshed.CutExecution.Should().BeFalse();
        refreshed.CutQuantity.Should().BeNull();
        refreshed.InspectionStage.Should().Be(nameof(InspectionType.FormalInspection)); // InspectionType 为空按终检保守
        // 需求=是、执行=否、已到成检且非强制完成 → 疑问-缺少（缺失成品切割记录）
        refreshed.CutDoubt.Should().Be(CutDoubtType.MissingRecords);
    }

    [Fact]
    public async Task RefreshCutTracking_执行否已完成_疑问缺少()
    {
        var ctx = CreateDbContext();
        var batch = await SeedCutBatchAsync(ctx, withCutRecord: false, batchNo: "BATCH-CUT-DONE");
        // 成检到料 + 仓库入库 → 刷新时批次状态=完成
        var pg = await ctx.ProcessGroups.FirstAsync(p => p.ProductionBatchId == batch.Id);
        ctx.MaterialReceiveChecks.Add(new MaterialReceiveCheck
        {
            ProductionBatchId = batch.Id,
            ReceiveDate = DateTime.Today,
            ProcessGroupId = pg.Id,
            ProcessName = "检验",
            SequenceNumber = 1,
            BatchNo = batch.BatchNo
        });
        // 仓库：InventoryBatch.Warehouse 为 required 导航，InMemory 下 Include 需有对应记录
        var warehouse = new Warehouse
        {
            Code = "WH-01",
            Name = "成品库",
            SortOrder = 1,
            IsActive = true
        };
        ctx.Warehouses.Add(warehouse);
        await ctx.SaveChangesAsync();
        ctx.InventoryBatches.Add(new InventoryBatch
        {
            BatchNo = "CK-CUT-DONE",
            ProductionBatchNo = batch.BatchNo,
            InboundSource = "生产",
            SourceName = "内部",
            MaterialType = batch.ManufacturingItem,
            PlantGrade = batch.PlantGrade,
            Specification = batch.Specification,
            InitialQuantity = 100,
            InitialWeight = 5000m,
            RemainingQuantity = 100,
            RemainingWeight = 5000m,
            WarehouseId = warehouse.Id,
            InboundDate = DateTime.Today
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.Status.Should().Be(BatchStatus.Completed);
        refreshed.CutRequirement.Should().BeTrue();
        refreshed.CutExecution.Should().BeFalse();
        refreshed.CutQuantity.Should().BeNull();
        // 需求=是、执行=否、已完成且非强制完成 → 疑问-缺少
        refreshed.CutDoubt.Should().Be(CutDoubtType.MissingRecords);
    }

    [Fact]
    public async Task RefreshCutTracking_附加成检末位_不误导成切需求判定()
    {
        var ctx = CreateDbContext();
        var batch = await SeedCutBatchAsync(ctx, addAdditionalInspection: true);
        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        // 60冷轧 工序组（Spec=成品规格 含断切）即使不是 SequenceNumber 最大，也应判定成切需求=是
        refreshed.CutRequirement.Should().BeTrue();
        refreshed.CutExecution.Should().BeTrue();
        refreshed.CutQuantity.Should().Be(100);
        refreshed.CutDoubt.Should().Be(CutDoubtType.Normal);
    }

    [Fact]
    public async Task RefreshCutTracking_定尺_按切后支数汇总()
    {
        var ctx = CreateDbContext();
        // 定尺：即使加工支数(90)与切后支数(80)不同，也应汇总切后支数
        var batch = await SeedCutBatchAsync(ctx, cutQty: 80, processQty: 90, lengthStatus: "Fixed");
        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.CutRequirement.Should().BeTrue();
        refreshed.CutExecution.Should().BeTrue();
        refreshed.CutQuantity.Should().Be(80); // 定尺 → 切后支数 PostCutQuantity
        refreshed.CutDoubt.Should().Be(CutDoubtType.QuantityMismatch); // |80-100|/100 = 20% > 5% → 疑问-数量
    }

    [Fact]
    public async Task RefreshCutTracking_非定尺_按加工支数汇总()
    {
        var ctx = CreateDbContext();
        // 非定尺：即使切后支数(80)与加工支数(90)不同，也应汇总加工支数
        var batch = await SeedCutBatchAsync(ctx, cutQty: 80, processQty: 90, lengthStatus: "NonFixed");
        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.CutRequirement.Should().BeTrue();
        refreshed.CutExecution.Should().BeTrue();
        refreshed.CutQuantity.Should().Be(90); // 非定尺 → 加工支数 Quantity
        refreshed.CutDoubt.Should().Be(CutDoubtType.QuantityMismatch); // |90-100|/100 = 10% > 5% → 疑问-数量
    }

    [Fact]
    public async Task BatchUpdateBatchTrackingAsync_人工暂停批次状态不被覆盖()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-SUSP");
        batch.Status = BatchStatus.Suspended;
        await ctx.SaveChangesAsync();

        // 有一条生产记录：若状态被覆盖，会被改为"在产"
        var pg = await SeedProcessGroupAsync(ctx, batch.Id);
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            SequenceNumber = 1,
            ExecDate = DateTime.Today
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.BatchUpdateBatchTrackingAsync(new[] { batch.Id });

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.Status.Should().Be(BatchStatus.Suspended);
    }

    [Fact]
    public async Task BatchUpdateBatchTrackingAsync_混合场景_强制完成批次理论成品被回填()
    {
        var ctx = CreateDbContext();
        var activeBatch = await SeedBatchAsync(ctx, "BATCH-ACTIVE");
        var fcBatch = await SeedBatchAsync(ctx, "BATCH-FC");
        fcBatch.IsForceCompleted = true;
        fcBatch.ProductionRatio = 1;
        fcBatch.CurrentValidQty = 100;
        await ctx.SaveChangesAsync();
        await SeedProcessGroupAsync(ctx, fcBatch.Id);

        var svc = CreateService(ctx);
        // 活跃批次 + 强制完成批次混合：修复前强制完成批次会被主分支跳过
        await svc.BatchUpdateBatchTrackingAsync(new[] { activeBatch.Id, fcBatch.Id });

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == fcBatch.Id);
        // 强制完成批次仅重算理论成品/全工量/成切跟踪：理论成品应按 100×1 回填
        refreshed.TheoreticalOutputQty.Should().Be(100);
    }
}
