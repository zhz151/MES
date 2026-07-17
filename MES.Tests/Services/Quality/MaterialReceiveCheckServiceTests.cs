using FluentAssertions;
using Microsoft.Extensions.Logging;
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
using MES.Services.Quality;
using MES.Tests.Tests;
using Moq;


using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Batch;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Tests.Services.Quality;

/// <summary>
/// 检验到料（成检到料）服务测试
/// </summary>
public class MaterialReceiveCheckServiceTests : TestBase
{
    private MaterialReceiveCheckService CreateService(AppDbContext ctx)
    {
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        var qptMock = new Mock<IQualityProcessTrackingService>();
        return new(ctx, configMock.Object, qptMock.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MaterialReceiveCheckService>.Instance, new MemoryCache(new MemoryCacheOptions()));
    }

    private async Task<ProductionBatch> SeedBatchAsync(AppDbContext ctx, string batchNo = "BATCH001")
    {
        var batch = new ProductionBatch
        {
            BatchNo = batchNo,
            WorkOrderNo = "WO001",
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            OrderItemIds = "[]",
            MaterialName = "不锈钢管",
            PlantGrade = "304",
            Specification = "219*8",
            Status = BatchStatus.InProgress,
            ProductionType = "InProcess",
            ManufacturingItem = "OrderFinished",
            CurrentValidQty = 100,
            CurrentValidWeight = 5000m,
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

    // ========== 检验到料 ==========

    [Fact]
    public async Task CreateMaterialReceiveCheckAsync_成功创建()
    {
        var ctx = CreateDbContext();
        await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateMaterialReceiveCheckAsync(new CreateMaterialReceiveCheckRequest
        {
            BatchNo = "BATCH001",
            ReceiveDate = DateTime.Today
        });

        result.Should().NotBeNull();
        result.BatchNo.Should().Be("BATCH001");
    }

    [Fact]
    public async Task GetMaterialReceiveCheckAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        await svc.CreateMaterialReceiveCheckAsync(new CreateMaterialReceiveCheckRequest
        {
            BatchNo = "BATCH001",
            ReceiveDate = DateTime.Today
        });

        var result = await svc.GetMaterialReceiveCheckAsync(batch.Id);

        result.Should().NotBeNull();
        result!.BatchNo.Should().Be("BATCH001");
    }

    [Fact]
    public async Task GetMaterialReceiveCheckAsync_不存在_返回Null()
    {
        var ctx = CreateDbContext();
        await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetMaterialReceiveCheckAsync(999);

        result.Should().BeNull();
    }
}
