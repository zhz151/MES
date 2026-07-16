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
    private ProductionRecordService CreateService(AppDbContext ctx)
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
            new MemoryCache(new MemoryCacheOptions()));
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
            ManufacturingItem = "OrderFinishedProduct",
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

    // ========== 工段委外 ==========

    [Fact]
    public async Task CreateSectionOutsourceAsync_成功创建()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        var result = await svc.CreateSectionOutsourceAsync(new CreateSectionOutsourceRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = "冷轧拔",
            OutsourceVendor = "委外厂A",
            SendOutDate = DateTime.Today,
            SendQuantity = 10,
            SendWeight = 1000m
        });

        result.Should().NotBeNull();
        result.OutsourceVendor.Should().Be("委外厂A");
    }

    [Fact]
    public async Task GetSectionOutsourcesAsync_无数据_返回空()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetSectionOutsourcesAsync(batch.Id, new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().BeEmpty();
    }

    // ========== 委外回收 ==========

    [Fact]
    public async Task CreateOutsourceRecoveryAsync_成功创建()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        // 先创建委外发出
        var sectionOutsource = new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = "冷轧拔",
            SequenceNumber = 1,
            OutsourceVendor = "委外厂A",
            SendOutDate = DateTime.Today,
            SendQuantity = 10,
            SendWeight = 1000m,
            Status = SectionOutsourceStatus.PendingRecovery
        };
        ctx.SectionOutsources.Add(sectionOutsource);
        await ctx.SaveChangesAsync();

        var result = await svc.CreateOutsourceRecoveryAsync(new CreateOutsourceRecoveryRequest
        {
            SectionOutsourceId = sectionOutsource.Id,
            RecoveryDate = DateTime.Today,
            RecoveryQuantity = 8,
            RecoveryWeight = 800m
        });

        result.Should().NotBeNull();
        result.RecoveryQuantity.Should().Be(8);
    }

    [Fact]
    public async Task DeleteOutsourceRecoveryAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        var sectionOutsource = new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = "冷轧拔",
            SequenceNumber = 1,
            OutsourceVendor = "委外厂A",
            SendOutDate = DateTime.Today,
            Status = SectionOutsourceStatus.PendingRecovery
        };
        ctx.SectionOutsources.Add(sectionOutsource);
        await ctx.SaveChangesAsync();

        var recovery = await svc.CreateOutsourceRecoveryAsync(new CreateOutsourceRecoveryRequest
        {
            SectionOutsourceId = sectionOutsource.Id,
            RecoveryDate = DateTime.Today,
            RecoveryQuantity = 8
        });

        await svc.DeleteOutsourceRecoveryAsync(recovery.Id);

        var deleted = await ctx.OutsourceRecoveries.FindAsync(recovery.Id);
        deleted.Should().BeNull();
    }

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
}
