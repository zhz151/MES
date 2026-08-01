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
            SectionName = "冷轧拔",
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
            SectionName = "冷轧拔",
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
            SectionName = "冷轧拔",
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
            SectionName = "断切",
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
            SectionName = "断切",
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
            SectionName = "断切",
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
            SectionName = "断切",
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
            SectionName = "冷轧拔",
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
            SectionName = "冷轧拔",
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
            SectionName = "冷轧拔",
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
            SectionName = "冷轧拔",
            ExecDate = DateTime.Today,
            Quantity = 10
        });
        await svc.CreateProductionRecordAsync(new CreateProductionRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "冷拔",
            ManufacturingSpec = "219*8",
            SectionName = "冷轧拔",
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
            SectionName = "冷轧拔",
            ExecDate = DateTime.Today,
            Quantity = 10
        });

        var result = await svc.GetAllProductionRecordsAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "SectionName", Operator = "in", Values = new List<string> { "冷轧拔" } }
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
            SectionName = "冷轧拔",
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
            SectionName = "冷轧拔",
            ExecDate = DateTime.Today,
            Quantity = 10
        });

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("BatchNo");
        contexts["BatchNo"].Should().Contain("BATCH001");
        contexts.Should().ContainKey("ProcessName");
        contexts["ProcessName"].Should().Contain("60冷轧");
        contexts["SectionName"].Should().Contain("冷轧拔");
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
            SectionName = "冷轧拔",
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
                SectionName = SectionDefs.Cut,
                SequenceNumber = 5,
                ExecDate = DateTime.Today,
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
        refreshed.CutDoubt.Should().BeFalse(); // |100-100|/100 = 0% ≤ 5% → 正常
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
        refreshed.CutDoubt.Should().BeTrue(); // |90-100|/100 = 10% > 5% → 疑问
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
    public async Task RefreshCutTracking_成切执行否_有需求无记录()
    {
        var ctx = CreateDbContext();
        var batch = await SeedCutBatchAsync(ctx, withCutRecord: false);
        var svc = CreateService(ctx);
        await svc.RefreshBatchTrackingFieldsAsync(batch.Id);

        var refreshed = await ctx.ProductionBatches.AsNoTracking().FirstAsync(b => b.Id == batch.Id);
        refreshed.CutRequirement.Should().BeTrue();
        refreshed.CutExecution.Should().BeFalse();
        refreshed.CutQuantity.Should().BeNull();
        refreshed.CutDoubt.Should().BeNull();
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
        refreshed.CutDoubt.Should().BeFalse();
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
        refreshed.CutDoubt.Should().BeTrue(); // |80-100|/100 = 20% > 5% → 疑问
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
        refreshed.CutDoubt.Should().BeTrue(); // |90-100|/100 = 10% > 5% → 疑问
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
            SectionName = SectionDefs.ColdRollDraw,
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
