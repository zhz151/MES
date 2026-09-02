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
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IOperatorNameValidator>());
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

    /// <summary>
    /// 构造支持「定尺切割长度匹配标识」判定的 IFixedLengthWorkOrderService Mock。
    /// 同时 mock 工单号级 / 主号级长度集合与批量映射（GetLengthMapsAsync）。
    /// 约定批次 WorkOrderNo=WO-001、SalesOrderNo=SO-001、ProductionMainNo=M-001。
    /// </summary>
    private static IFixedLengthWorkOrderService CreateCutMatchSvcMock(
        decimal[]? woLengths = null, decimal[]? mainNoLengths = null)
    {
        var mock = new Mock<IFixedLengthWorkOrderService>();
        var woSet = new HashSet<decimal>(woLengths ?? Array.Empty<decimal>());
        var mainSet = new HashSet<decimal>(mainNoLengths ?? Array.Empty<decimal>());
        mock.Setup(s => s.GetLengthsByWorkOrderNoAsync(It.IsAny<string>()))
            .ReturnsAsync(woSet);
        mock.Setup(s => s.GetLengthsByMainNoAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(mainSet);
        var maps = new FixedLengthLengthMaps
        {
            ByWorkOrderNo = new Dictionary<string, HashSet<decimal>>(StringComparer.OrdinalIgnoreCase)
            {
                ["WO-001"] = woSet
            },
            ByMainKey = new Dictionary<string, HashSet<decimal>>(StringComparer.OrdinalIgnoreCase)
            {
                ["SO-001|M-001"] = mainSet
            }
        };
        mock.Setup(s => s.GetLengthMapsAsync()).ReturnsAsync(maps);
        return mock.Object;
    }

    /// <summary>
    /// 构造「定尺 + 成品工序组」的批次（LengthStatus=Fixed，工序组 Spec==批次 Specification → 断切成品可判标识）。
    /// </summary>
    private async Task<ProductionBatch> SeedCutMatchBatchAsync(AppDbContext ctx, string batchNo = "BATCH-MATCH")
    {
        var batch = await SeedBatchAsync(ctx, batchNo);
        batch.LengthStatus = nameof(LengthStatus.Fixed);
        await ctx.SaveChangesAsync();
        await SeedProcessGroupAsync(ctx, batch.Id);
        return batch;
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
    public async Task CreateProductionRecordAsync_前端传非0序号_仍按工段步骤号对齐()
    {
        // 语义A：序号=工段步骤号，新增记录一旦定位工序组即强制对齐，忽略前端传入的 SequenceNumber
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);  // 冷轧拔=1
        var svc = CreateService(ctx);

        var result = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            SequenceNumber = 99,  // 前端错误值，应被工序组步骤号覆盖
            ExecDate = DateTime.Today,
            Quantity = 10,
            Weight = 1000m
        });

        result.SequenceNumber.Should().Be(1);
        var saved = await ctx.ProductionRecords.FirstAsync(r => r.Id == result.Id);
        saved.SequenceNumber.Should().Be(1);
    }

    [Fact]
    public async Task UpdateProductionRecordAsync_工序组工段步骤号变更后_重对齐序号()
    {
        // 语义A：更新时序号 = 工段步骤号（工序组编辑后纠正漂移）
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var pg = await SeedProcessGroupAsync(ctx, batch.Id);  // 冷轧拔=1
        var svc = CreateService(ctx);

        var created = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            ExecDate = DateTime.Today,
            Quantity = 10,
            Weight = 1000m
        });
        created.SequenceNumber.Should().Be(1);

        // 工序组被编辑：冷轧拔从 1 改为 2（前面插入工段）
        pg.ColdRollDraw = 2;
        await ctx.SaveChangesAsync();

        var updated = await svc.UpdateProductionRecordAsync(created.Id, new UpdateProductionRecordRequest
        {
            ExecDate = DateTime.Today,
            Quantity = 20
        });

        updated.SequenceNumber.Should().Be(2);
        var saved = await ctx.ProductionRecords.FirstAsync(r => r.Id == created.Id);
        saved.SequenceNumber.Should().Be(2);
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

    // ========== 定尺切割长度匹配标识（符合工单长度） ==========

    [Fact]
    public async Task CreateProductionRecordAsync_成品定尺非预成切长度属本工单号_完全匹配()
    {
        var ctx = CreateDbContext();
        await SeedCutMatchBatchAsync(ctx);
        var svc = CreateService(ctx, CreateCutMatchSvcMock(
            woLengths: new[] { 4000m, 8000m }, mainNoLengths: new[] { 4000m, 8000m, 6000m }));

        var result = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH-MATCH",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today,
            FinishedCutLength = 4000m
        });

        result.CutLengthMatchType.Should().Be(CutLengthMatchType.FullMatch);
        result.CutLengthMatchTypeDisplay.Should().Be("完全匹配");
        // 持久化到实体
        var saved = await ctx.ProductionRecords.FirstAsync(r => r.Id == result.Id);
        saved.CutLengthMatchType.Should().Be(nameof(CutLengthMatchType.FullMatch));
    }

    [Fact]
    public async Task CreateProductionRecordAsync_成品定尺非预成切仅属主号_主号匹配()
    {
        var ctx = CreateDbContext();
        await SeedCutMatchBatchAsync(ctx);
        var svc = CreateService(ctx, CreateCutMatchSvcMock(
            woLengths: new[] { 4000m, 8000m }, mainNoLengths: new[] { 4000m, 8000m, 6000m }));

        var result = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH-MATCH",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today,
            FinishedCutLength = 6000m // 仅属订单+主号，非本工单号 → 主号匹配
        });

        result.CutLengthMatchType.Should().Be(CutLengthMatchType.MainNoMatch);
        result.CutLengthMatchTypeDisplay.Should().Be("主号匹配");
    }

    [Fact]
    public async Task CreateProductionRecordAsync_预成切_标识不适用()
    {
        var ctx = CreateDbContext();
        await SeedCutMatchBatchAsync(ctx);
        var svc = CreateService(ctx, CreateCutMatchSvcMock(
            woLengths: new[] { 4000m, 8000m }, mainNoLengths: new[] { 4000m, 8000m }));

        var result = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH-MATCH",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today,
            FinishedCutLength = 6000m, // 不在正式定尺集合 → 预成切校验通过
            IsPreCut = true
        });

        result.IsPreCut.Should().BeTrue();
        result.CutLengthMatchType.Should().BeNull();
        result.CutLengthMatchTypeDisplay.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateProductionRecordAsync_非定尺批次_标识不适用()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-NONFIX"); // LengthStatus=NonFixed 默认
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx, CreateCutMatchSvcMock(
            woLengths: new[] { 4000m, 8000m }, mainNoLengths: new[] { 4000m, 8000m }));

        var result = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH-NONFIX",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today,
            FinishedCutLength = 4000m // 长度校验通过，但非定尺 → 不适用
        });

        result.LengthStatus.Should().Be(LengthStatus.NonFixed);
        result.CutLengthMatchType.Should().BeNull();
    }

    [Fact]
    public async Task CreateProductionRecordAsync_非成品切割_标识不适用()
    {
        var ctx = CreateDbContext();
        await SeedCutMatchBatchAsync(ctx);
        var svc = CreateService(ctx, CreateCutMatchSvcMock(
            woLengths: new[] { 4000m, 8000m }, mainNoLengths: new[] { 4000m, 8000m, 6000m }));

        var result = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH-MATCH",
            ProcessName = "60冷轧",
            ManufacturingSpec = "273*10", // ≠ 批次 Specification → 非成品
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today,
            FinishedCutLength = 4000m
        });

        result.ProductStatus.Should().NotBe(ProductStatuses.Finished);
        result.CutLengthMatchType.Should().BeNull();
    }

    [Fact]
    public async Task BatchCreateProductionRecordsAsync_按工单号主号集合_计算标识()
    {
        var ctx = CreateDbContext();
        var batch = await SeedCutMatchBatchAsync(ctx);
        // 批量创建校验要求：工序组须含「断切」工段 + 本次提交须先有「冷轧拔」记录
        var pg = await ctx.ProcessGroups.FirstAsync(p => p.ProductionBatchId == batch.Id);
        pg.Cut = 5;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, CreateCutMatchSvcMock(
            woLengths: new[] { 4000m, 8000m }, mainNoLengths: new[] { 4000m, 8000m, 6000m }));

        var results = await svc.BatchCreateProductionRecordsAsync(new List<CreateProductionRecordRequest>
        {
            new() { BatchNo = "BATCH-MATCH", ProcessName = "60冷轧", ManufacturingSpec = "219*8", SectionName = SectionKeys.ColdRollDraw, ExecDate = DateTime.Today, Quantity = 10 },
            new() { BatchNo = "BATCH-MATCH", ProcessName = "60冷轧", ManufacturingSpec = "219*8", SectionName = SectionKeys.Cut, ExecDate = DateTime.Today, FinishedCutLength = 4000m },
            new() { BatchNo = "BATCH-MATCH", ProcessName = "60冷轧", ManufacturingSpec = "219*8", SectionName = SectionKeys.Cut, ExecDate = DateTime.Today, FinishedCutLength = 6000m }
        });

        results.Should().HaveCount(3);
        results[1].CutLengthMatchType.Should().Be(CutLengthMatchType.FullMatch);
        results[2].CutLengthMatchType.Should().Be(CutLengthMatchType.MainNoMatch);
    }

    [Fact]
    public async Task BatchCreateProductionRecordsAsync_断切聚合重量超限_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedCutMatchBatchAsync(ctx);
        batch.CurrentValidWeight = 1000;
        await ctx.SaveChangesAsync();
        var pg = await ctx.ProcessGroups.FirstAsync(p => p.ProductionBatchId == batch.Id);
        pg.Cut = 5;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, CreateCutMatchSvcMock(
            woLengths: new[] { 4000m, 8000m }, mainNoLengths: new[] { 4000m, 8000m, 6000m }));

        // 单条各 600 ≤ 1000（单条校验通过），但同批次同工序组聚合 1200 > 1000 → 抛错
        var act = () => svc.BatchCreateProductionRecordsAsync(new List<CreateProductionRecordRequest>
        {
            new() { BatchNo = "BATCH-MATCH", ProcessName = "60冷轧", ManufacturingSpec = "219*8", SectionName = SectionKeys.ColdRollDraw, ExecDate = DateTime.Today, Quantity = 10 },
            new() { BatchNo = "BATCH-MATCH", ProcessName = "60冷轧", ManufacturingSpec = "219*8", SectionName = SectionKeys.Cut, ExecDate = DateTime.Today, FinishedCutLength = 4000m, Weight = 600m },
            new() { BatchNo = "BATCH-MATCH", ProcessName = "60冷轧", ManufacturingSpec = "219*8", SectionName = SectionKeys.Cut, ExecDate = DateTime.Today, FinishedCutLength = 6000m, Weight = 600m }
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*断切总加工重量(1200)*");
    }

    [Fact]
    public async Task BatchCreateProductionRecordsAsync_断切聚合含DB已有重量_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedCutMatchBatchAsync(ctx);
        batch.CurrentValidWeight = 1000;
        await ctx.SaveChangesAsync();
        var pg = await ctx.ProcessGroups.FirstAsync(p => p.ProductionBatchId == batch.Id);
        pg.Cut = 5;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, CreateCutMatchSvcMock(
            woLengths: new[] { 4000m, 8000m }, mainNoLengths: new[] { 4000m, 8000m, 6000m }));

        // DB 已有断切 700（单条路径仅单条 ≤ 校验，700 ≤ 1000 通过）
        await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH-MATCH",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today,
            FinishedCutLength = 4000m,
            Weight = 700m
        });

        // 本次再提交断切 400 → 700+400=1100 > 1000 → 抛错
        var act = () => svc.BatchCreateProductionRecordsAsync(new List<CreateProductionRecordRequest>
        {
            new() { BatchNo = "BATCH-MATCH", ProcessName = "60冷轧", ManufacturingSpec = "219*8", SectionName = SectionKeys.ColdRollDraw, ExecDate = DateTime.Today, Quantity = 10 },
            new() { BatchNo = "BATCH-MATCH", ProcessName = "60冷轧", ManufacturingSpec = "219*8", SectionName = SectionKeys.Cut, ExecDate = DateTime.Today, FinishedCutLength = 6000m, Weight = 400m }
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*断切总加工重量(1100)*");
    }

    [Fact]
    public async Task BatchCreateProductionRecordsAsync_断切聚合重量未超限_创建成功()
    {
        var ctx = CreateDbContext();
        var batch = await SeedCutMatchBatchAsync(ctx);
        batch.CurrentValidWeight = 1000;
        await ctx.SaveChangesAsync();
        var pg = await ctx.ProcessGroups.FirstAsync(p => p.ProductionBatchId == batch.Id);
        pg.Cut = 5;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, CreateCutMatchSvcMock(
            woLengths: new[] { 4000m, 8000m }, mainNoLengths: new[] { 4000m, 8000m, 6000m }));

        // 聚合 400+400=800 ≤ 1000 → 通过
        var results = await svc.BatchCreateProductionRecordsAsync(new List<CreateProductionRecordRequest>
        {
            new() { BatchNo = "BATCH-MATCH", ProcessName = "60冷轧", ManufacturingSpec = "219*8", SectionName = SectionKeys.ColdRollDraw, ExecDate = DateTime.Today, Quantity = 10 },
            new() { BatchNo = "BATCH-MATCH", ProcessName = "60冷轧", ManufacturingSpec = "219*8", SectionName = SectionKeys.Cut, ExecDate = DateTime.Today, FinishedCutLength = 4000m, Weight = 400m },
            new() { BatchNo = "BATCH-MATCH", ProcessName = "60冷轧", ManufacturingSpec = "219*8", SectionName = SectionKeys.Cut, ExecDate = DateTime.Today, FinishedCutLength = 6000m, Weight = 400m }
        });

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task UpdateProductionRecordAsync_修改长度_重算标识()
    {
        var ctx = CreateDbContext();
        await SeedCutMatchBatchAsync(ctx);
        var svc = CreateService(ctx, CreateCutMatchSvcMock(
            woLengths: new[] { 4000m, 8000m }, mainNoLengths: new[] { 4000m, 8000m, 6000m }));

        var created = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH-MATCH",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today,
            FinishedCutLength = 4000m
        });
        created.CutLengthMatchType.Should().Be(CutLengthMatchType.FullMatch);

        var result = await svc.UpdateProductionRecordAsync(created.Id, new UpdateProductionRecordRequest
        {
            ExecDate = DateTime.Today,
            FinishedCutLength = 6000m
        });

        result.CutLengthMatchType.Should().Be(CutLengthMatchType.MainNoMatch);
    }

    [Fact]
    public async Task UpdateProductionRecordAsync_改为预成切_标识置空()
    {
        var ctx = CreateDbContext();
        await SeedCutMatchBatchAsync(ctx);
        var svc = CreateService(ctx, CreateCutMatchSvcMock(
            woLengths: new[] { 4000m, 8000m }, mainNoLengths: new[] { 4000m, 8000m }));

        var created = await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH-MATCH",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut,
            ExecDate = DateTime.Today,
            FinishedCutLength = 4000m
        });
        created.CutLengthMatchType.Should().Be(CutLengthMatchType.FullMatch);

        var result = await svc.UpdateProductionRecordAsync(created.Id, new UpdateProductionRecordRequest
        {
            ExecDate = DateTime.Today,
            IsPreCut = true,
            FinishedCutLength = 6000m // 不在正式定尺集合 → 预成切校验通过
        });

        result.IsPreCut.Should().BeTrue();
        result.CutLengthMatchType.Should().BeNull();
    }

    [Fact]
    public async Task RecomputeCutLengthMatchByBatchAsync_批次工单号变更_重算匹配标识()
    {
        var ctx = CreateDbContext();
        var batch = await SeedCutMatchBatchAsync(ctx, "BATCH-RECOMPUTE");
        var pg = await ctx.ProcessGroups.FirstAsync(p => p.ProductionBatchId == batch.Id);
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id, ProcessGroupId = pg.Id, ProcessName = "60冷轧", ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Cut, SequenceNumber = 5, ExecDate = DateTime.Today,
            ProductStatus = ProductStatuses.Finished, LengthStatus = nameof(LengthStatus.Fixed),
            FinishedCutLength = 4000m, CutLengthMatchType = nameof(CutLengthMatchType.FullMatch) // 旧值（批次工单号变更后应重算）
        });
        await ctx.SaveChangesAsync();

        // 模拟批次编辑把工单号改为 WO-OTHER（不在定尺工单集合内 → 仅命中主号集合 → 主号匹配）
        var batchEntity = await ctx.ProductionBatches.FirstAsync();
        batchEntity.WorkOrderNo = "WO-OTHER";
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, CreateCutMatchSvcMock(
            woLengths: new[] { 4000m, 8000m }, mainNoLengths: new[] { 4000m, 8000m, 6000m }));
        var updated = await svc.RecomputeCutLengthMatchByBatchAsync(batch.Id);

        updated.Should().Be(1);
        var saved = await ctx.ProductionRecords.SingleAsync();
        saved.CutLengthMatchType.Should().Be(nameof(CutLengthMatchType.MainNoMatch));
    }

    [Fact]
    public async Task RecomputeCutLengthMatchByBatchAsync_无记录批次_返回0()
    {
        var ctx = CreateDbContext();
        var batch = await SeedCutMatchBatchAsync(ctx, "BATCH-RECOMPUTE-EMPTY");

        var svc = CreateService(ctx, CreateCutMatchSvcMock(
            woLengths: new[] { 4000m, 8000m }, mainNoLengths: new[] { 4000m, 8000m, 6000m }));
        var updated = await svc.RecomputeCutLengthMatchByBatchAsync(batch.Id);

        updated.Should().Be(0);
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

    [Fact]
    public async Task RefreshBatchTracking_首工段即检验_无记录_状态归为成检()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-INSP-FIRST");
        // 无任何生产记录/检验到料/入库，首工段即为"检验"
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 1,
            ProcessName = "成品检验",
            ManufacturingSpec = "219*8",
            Inspection = 1
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.Status.Should().Be(BatchStatus.InFinalInspection);
    }

    [Fact]
    public async Task BatchUpdateBatchTracking_首工段即检验_无记录_状态归为成检()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-INSP-BATCH");
        // 无任何生产记录/检验到料/入库，首工段即为"检验"（批量刷新路径）
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 1,
            ProcessName = "成品检验",
            ManufacturingSpec = "219*8",
            Inspection = 1
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.BatchUpdateBatchTrackingAsync(new[] { batch.Id });

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.Status.Should().Be(BatchStatus.InFinalInspection);
    }

    [Fact]
    public async Task RefreshBatchTracking_下工段成品检验_前工段完工_状态归为成检()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-GATE-DONE");
        // 工序组1 生产工段（矫直）完工 → 下工段=工序组2 的成品检验（ManufacturingSpec==批次规格）
        var pg1 = new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 1,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            Straighten = 1
        };
        ctx.ProcessGroups.Add(pg1);
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 2,
            ProcessName = "成品检验",
            ManufacturingSpec = "219*8",
            Inspection = 2
        });
        await ctx.SaveChangesAsync();
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg1.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Straighten, // 英文 Key
            SequenceNumber = 1,
            ExecDate = DateTime.Today
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.Status.Should().Be(BatchStatus.InFinalInspection);
        refreshed.RemainingWorkDays.Should().Be(0);
    }

    [Fact]
    public async Task RefreshBatchTracking_下工段检验属半成品组_不归成检()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-GATE-SEMI");
        // 工序组1（半成品规格，≠批次规格）内嵌过程检验：下工段虽为检验但不属成品规格组 → 不判成检
        var pg1 = new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 1,
            ProcessName = "60冷轧",
            ManufacturingSpec = "圆管坯",
            ColdRollDraw = 1,
            Inspection = 2
        };
        ctx.ProcessGroups.Add(pg1);
        await ctx.SaveChangesAsync();
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg1.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "圆管坯",
            SectionName = SectionKeys.ColdRollDraw,
            SequenceNumber = 1,
            ExecDate = DateTime.Today,
            Weight = 1000m // 完工量足够，仅用于排除"冷轧拔未完工"干扰
        });
        await ctx.SaveChangesAsync();
        batch.CurrentValidWeight = 1000;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.Status.Should().Be(BatchStatus.InProgress); // 下工段检验属半成品组，仍在产
    }

    [Fact]
    public async Task RefreshBatchTracking_当前工段未完工_下工段检验_不归成检()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-GATE-UNFINISHED");
        // 工序组1 冷轧拔未达完工重量（95%）→ 当前工段未完工，下工段虽为检验但不判成检
        var pg1 = new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 1,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            ColdRollDraw = 1,
            Inspection = 2
        };
        ctx.ProcessGroups.Add(pg1);
        await ctx.SaveChangesAsync();
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg1.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            SequenceNumber = 1,
            ExecDate = DateTime.Today
            // 无 Weight → 完工量 0 < 阈值 95
        });
        await ctx.SaveChangesAsync();
        batch.CurrentValidWeight = 100; // 完工阈值 = 100 × 0.95 = 95
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.Status.Should().Be(BatchStatus.InProgress); // 冷轧拔未完工，仍在产
    }

    [Fact]
    public async Task RefreshBatchTracking_余库料_下工段检验规格匹配_不归成检()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-GATE-SURPLUS");
        // 余库料制造物品，即使检验工段 ManufacturingSpec==批次规格，也属"过程检验" → 永不进成检
        batch.ManufacturingItem = nameof(MaterialType.Surplus);
        await ctx.SaveChangesAsync();
        // 工序组1 生产工段（矫直）完工 → 下工段=工序组2 的检验（ManufacturingSpec==批次规格）
        var pg1 = new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 1,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            Straighten = 1
        };
        ctx.ProcessGroups.Add(pg1);
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 2,
            ProcessName = "检验",
            ManufacturingSpec = "219*8",
            Inspection = 2
        });
        await ctx.SaveChangesAsync();
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg1.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Straighten, // 英文 Key
            SequenceNumber = 1,
            ExecDate = DateTime.Today
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.Status.Should().Be(BatchStatus.InProgress); // 余库料未入库仍"在产"，不与"成检"混淆
    }

    [Fact]
    public async Task RefreshBatchTracking_首工段非检验_无记录_状态归为未产()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-NOINSP-FIRST");
        // 首工段为冷轧拔（非检验），无记录 → 未产
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 1,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            ColdRollDraw = 1
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.Status.Should().Be(BatchStatus.None);
    }

    [Fact]
    public async Task RefreshBatchTracking_余库料_首工段即检验_无记录_状态归为未产()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-SURPLUS-INSP-FIRST");
        // 余库料制造物品，首工段即"检验"、无任何生产记录/检验到料/入库：
        // 非成品类检验属"过程检验"，永不进成检 → 未产（而非成检）
        batch.ManufacturingItem = nameof(MaterialType.Surplus);
        await ctx.SaveChangesAsync();
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 1,
            ProcessName = "成品检验",
            ManufacturingSpec = "219*8",
            Inspection = 1
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.Status.Should().Be(BatchStatus.None);
    }

    [Fact]
    public async Task BatchUpdateBatchTracking_余库料_首工段即检验_无记录_状态归为未产()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-SURPLUS-INSP-BATCH");
        // 批量刷新路径对称验证：余库料首工段即检验、无记录 → 未产
        batch.ManufacturingItem = nameof(MaterialType.Surplus);
        await ctx.SaveChangesAsync();
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 1,
            ProcessName = "成品检验",
            ManufacturingSpec = "219*8",
            Inspection = 1
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.BatchUpdateBatchTrackingAsync(new[] { batch.Id });

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.Status.Should().Be(BatchStatus.None);
    }

    [Fact]
    public async Task GetTrackingVisual_工段执行日期正确回填()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-VISUAL");
        var pg = await SeedProcessGroupAsync(ctx, batch.Id); // ColdRollDraw = 1（仅一个工段）
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw, // 存储为英文 Key
            SequenceNumber = 1,
            ExecDate = DateTime.Today.AddDays(-1)
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var visual = await svc.GetTrackingVisualAsync(batch.Id);

        var section = visual.ProcessGroups.Should().ContainSingle().Subject
            .Sections.Should().ContainSingle(s => s.SectionName == SectionDefs.ColdRollDraw).Subject;
        // 修复前：中文显示名与英文 Key 记录失配 → ExecDate 恒空
        section.ExecDate.Should().Be(DateTime.Today.AddDays(-1));
        section.Status.Should().Be(SectionStatus.Completed);
    }

    // ========== 当前设备 / 当前委外并存（2026-08-20） ==========

    [Fact]
    public async Task RefreshBatchTracking_设备与委外并存_未完工_互不覆盖()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-PARA-DEV-OUT");
        var pg = new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 1,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            Straighten = 5
        };
        ctx.ProcessGroups.Add(pg);
        await ctx.SaveChangesAsync();

        // 生产记录（矫直 seq=5，设备A）
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Straighten,
            SequenceNumber = 5,
            ExecDate = new DateTime(2026, 8, 1),
            EquipmentName = "设备A"
        });
        // 工段委外（seq=8，委外单位X，未回收）
        ctx.SectionOutsources.Add(new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Straighten,
            SequenceNumber = 8,
            OutsourceVendor = "委外单位X",
            SendOutDate = new DateTime(2026, 8, 2)
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        // 委外未回收 → 当前工段未完工 → 设备/委外并存互不覆盖
        refreshed.CurrentSectionCompleted.Should().BeFalse();
        refreshed.CurrentEquipmentName.Should().Be("设备A");
        refreshed.CurrentOutsource.Should().Be("委外单位X");
        // 截止执行日 = 生产/委外两路日期取最大
        refreshed.CurrentExecDate.Should().Be(new DateTime(2026, 8, 2));
    }

    [Fact]
    public async Task RefreshBatchTracking_生产工段完工_设备委外均清空()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-PARA-DONE");
        var pg = new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 1,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            Straighten = 5
        };
        ctx.ProcessGroups.Add(pg);
        await ctx.SaveChangesAsync();

        // 生产记录（矫直 seq=5，设备A）为最大序号 → 非冷轧拔工段 → 有记录即完工
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Straighten,
            SequenceNumber = 5,
            ExecDate = new DateTime(2026, 8, 1),
            EquipmentName = "设备A"
        });
        // 工段委外（seq=3，未回收）
        ctx.SectionOutsources.Add(new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Straighten,
            SequenceNumber = 3,
            OutsourceVendor = "委外单位X",
            SendOutDate = new DateTime(2026, 8, 2)
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        // 生产工段完工 → 当前设备/委外均清空
        refreshed.CurrentSectionCompleted.Should().BeTrue();
        refreshed.CurrentEquipmentName.Should().BeNull();
        refreshed.CurrentOutsource.Should().BeNull();
        // 截止执行日不被清空，仍取五路最大
        refreshed.CurrentExecDate.Should().Be(new DateTime(2026, 8, 2));
    }

    [Fact]
    public async Task RefreshBatchTracking_委外已回收_设备委外均清空()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-PARA-RECOVER");
        var pg = new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 1,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            Straighten = 3
        };
        ctx.ProcessGroups.Add(pg);
        await ctx.SaveChangesAsync();

        // 生产记录（矫直 seq=3，设备A）
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Straighten,
            SequenceNumber = 3,
            ExecDate = new DateTime(2026, 8, 1),
            EquipmentName = "设备A"
        });
        // 工段委外（seq=8，委外单位X，已回收）
        var outsource = new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Straighten,
            SequenceNumber = 8,
            OutsourceVendor = "委外单位X",
            SendOutDate = new DateTime(2026, 8, 2)
        };
        ctx.SectionOutsources.Add(outsource);
        await ctx.SaveChangesAsync();
        ctx.OutsourceRecoveries.Add(new OutsourceRecovery
        {
            SectionOutsourceId = outsource.Id,
            RecoveryDate = new DateTime(2026, 8, 3)
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        // 委外已回收 → 当前工段完工 → 设备/委外均清空
        refreshed.CurrentSectionCompleted.Should().BeTrue();
        refreshed.CurrentEquipmentName.Should().BeNull();
        refreshed.CurrentOutsource.Should().BeNull();
    }

    [Fact]
    public async Task RefreshBatchTracking_委外取序号最大者_未回收()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-PARA-MAXOUT");
        var pg = new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 1,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            Straighten = 3
        };
        ctx.ProcessGroups.Add(pg);
        await ctx.SaveChangesAsync();

        // 委外A seq=3、委外B seq=8（均未回收）→ 取序号最大的委外B
        ctx.SectionOutsources.Add(new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Straighten,
            SequenceNumber = 3,
            OutsourceVendor = "委外A",
            SendOutDate = new DateTime(2026, 8, 1)
        });
        ctx.SectionOutsources.Add(new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Straighten,
            SequenceNumber = 8,
            OutsourceVendor = "委外B",
            SendOutDate = new DateTime(2026, 8, 2)
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.CurrentSectionCompleted.Should().BeFalse();
        refreshed.CurrentOutsource.Should().Be("委外B");
        refreshed.CurrentExecDate.Should().Be(new DateTime(2026, 8, 2));
    }

    [Fact]
    public async Task RefreshBatchTracking_截止执行日_五路日期取最大()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-PARA-FIVEMAX");
        var pg = new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 1,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            Straighten = 4,
            Inspection = 6
        };
        ctx.ProcessGroups.Add(pg);
        await ctx.SaveChangesAsync();

        // ① 生产记录（矫直 seq=4，设备A，8/1）
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Straighten,
            SequenceNumber = 4,
            ExecDate = new DateTime(2026, 8, 1),
            EquipmentName = "设备A"
        });
        // ② 工段委外（seq=5，未回收，8/2）
        ctx.SectionOutsources.Add(new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Straighten,
            SequenceNumber = 5,
            OutsourceVendor = "委外单位X",
            SendOutDate = new DateTime(2026, 8, 2)
        });
        // ③ 过程检验（seq=6，设备B，8/3）
        ctx.ProcessInspections.Add(new ProcessInspection
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Inspection,
            SequenceNumber = 6,
            InspectionDate = new DateTime(2026, 8, 3),
            EquipmentName = "设备B"
        });
        // ④ 检验到料（pg.Inspection=6 → materialCheckSeq=6，8/4）
        ctx.MaterialReceiveChecks.Add(new MaterialReceiveCheck
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "检验",
            SequenceNumber = 1,
            BatchNo = batch.BatchNo,
            InspectionType = nameof(InspectionType.FormalInspection),
            ReceiveDate = new DateTime(2026, 8, 4)
        });
        // ⑤ 入缸（酸洗 seq=7，浸泡中，设备C，8/5）
        ctx.PicklingInRecords.Add(new PicklingInRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Pickle,
            SequenceNumber = 7,
            InDate = new DateTime(2026, 8, 5),
            Status = PicklingStatus.Soaking,
            EquipmentName = "设备C"
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        // 五路日期取最大 = 入缸 8/5
        refreshed.CurrentExecDate.Should().Be(new DateTime(2026, 8, 5));
        // 入缸浸泡中 → 未完工 → 设备/委外并存：设备取序号最大且非空 = 设备C，委外未回收保留
        refreshed.CurrentSectionCompleted.Should().BeFalse();
        refreshed.CurrentEquipmentName.Should().Be("设备C");
        refreshed.CurrentOutsource.Should().Be("委外单位X");
    }

    [Fact]
    public async Task RefreshBatchTracking_冷轧拔完工_生产记录加纯合格回收_达标()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-CRD-RECOVERED");
        var pg = await SeedProcessGroupAsync(ctx, batch.Id); // ColdRollDraw=1
        // 生产记录 500（单凭生产记录不足 950 阈值）
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            SequenceNumber = 1,
            ExecDate = DateTime.Today,
            Weight = 500m
        });
        // 委外冷轧拔 + 纯合格回收 500 → 500 + 500 = 1000 ≥ 950
        var os = new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            SequenceNumber = 1,
            OutsourceVendor = "委外厂A",
            SendOutDate = DateTime.Today,
            SendWeight = 1000m,
            Status = SectionOutsourceStatus.PendingRecovery
        };
        ctx.SectionOutsources.Add(os);
        ctx.OutsourceRecoveries.Add(new OutsourceRecovery
        {
            SectionOutsource = os,
            RecoveryDate = DateTime.Today,
            RecoveryWeight = 500m
        });
        await ctx.SaveChangesAsync();
        batch.CurrentValidWeight = 1000; // 阈值 950
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.CurrentSectionCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshBatchTracking_冷轧拔完工_不含未加工退回()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-CRD-UNPROCESSED");
        var pg = await SeedProcessGroupAsync(ctx, batch.Id);
        // 生产 500 + 纯合格 400 = 900 < 950 → 未完工；
        // 若错误计入未加工退回 100 → 1000 会误判完工，故断言 false 证明未加工退回未计入
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            SequenceNumber = 1,
            ExecDate = DateTime.Today,
            Weight = 500m
        });
        var os = new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            SequenceNumber = 1,
            OutsourceVendor = "委外厂A",
            SendOutDate = DateTime.Today,
            SendWeight = 1000m,
            Status = SectionOutsourceStatus.PendingRecovery
        };
        ctx.SectionOutsources.Add(os);
        ctx.OutsourceRecoveries.Add(new OutsourceRecovery
        {
            SectionOutsource = os,
            RecoveryDate = DateTime.Today,
            RecoveryWeight = 400m,
            UnprocessedWeight = 100m // 未加工退回，不计入完工
        });
        await ctx.SaveChangesAsync();
        batch.CurrentValidWeight = 1000; // 阈值 950
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.CurrentSectionCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task BatchUpdateBatchTrackingAsync_空集合_直接返回不抛异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        // 空集合触发新增的空集保护（防分片前空集也走一遍空查询），应直接返回
        await svc.BatchUpdateBatchTrackingAsync(new List<int>());
    }

    [Fact]
    public async Task BatchUpdateBatchTrackingAsync_超过1000批次_分片处理不抛异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        // 传 1005 个不存在的批次 ID（跨过 1000 分片边界）：分片循环正常执行，
        // batchDict 为空快速返回，验证 Chunk 分片后查询不因大 IN 列表抛异常
        var fakeIds = Enumerable.Range(900_000, 1005).ToList();
        await svc.BatchUpdateBatchTrackingAsync(fakeIds);
    }
}
