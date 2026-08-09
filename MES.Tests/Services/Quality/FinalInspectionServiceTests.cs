using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
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
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Services.Quality;
using MES.Tests.Tests;


using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Quality;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Tests.Services;

/// <summary>
/// 成品检验服务测试：CRUD、关键字搜索、日期筛选、批次调取、批量创建
/// </summary>
public class FinalInspectionServiceTests : TestBase
{
    private FinalInspectionService CreateService(AppDbContext ctx, IFixedLengthWorkOrderService? fixedLengthSvc = null)
    {
        var workOrderExecMock = new Mock<IWorkOrderExecutionService>();
        var qptMock = new Mock<IQualityProcessTrackingService>();
        return new(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<FinalInspectionService>.Instance, workOrderExecMock.Object, qptMock.Object, fixedLengthSvc ?? CreateFixedLengthSvcMock(), new MemoryCache(new MemoryCacheOptions()));
    }

    /// <summary>
    /// 构造一个定尺长度集合可配置的 IFixedLengthWorkOrderService Mock。
    /// 默认返回空集合（等价于非定尺主号，跳过校验）。
    /// GetLengthsByWorkOrderNoAsync/GetLengthMapsAsync 与 GetLengthsByMainNoAsync 一致，
    /// 供「符合工单长度」匹配标识计算使用（WorkOrderNo=WO-001 / 主键=SO-001|M-001）。
    /// </summary>
    private static IFixedLengthWorkOrderService CreateFixedLengthSvcMock(params decimal[] lengths)
    {
        var set = new HashSet<decimal>(lengths);
        var mock = new Mock<IFixedLengthWorkOrderService>();
        mock.Setup(s => s.GetLengthsByMainNoAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new HashSet<decimal>(set));
        mock.Setup(s => s.GetLengthsByWorkOrderNoAsync(It.IsAny<string>()))
            .ReturnsAsync(new HashSet<decimal>(set));
        mock.Setup(s => s.GetLengthMapsAsync())
            .ReturnsAsync(new FixedLengthLengthMaps
            {
                ByWorkOrderNo = { ["WO-001"] = new HashSet<decimal>(set) },
                ByMainKey = { ["SO-001|M-001"] = new HashSet<decimal>(set) }
            });
        return mock.Object;
    }

    private async Task<ProductionBatch> SeedBatchAsync(AppDbContext ctx, string batchNo = "BATCH001", string lengthStatus = "NonFixed")
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
            LengthStatus = lengthStatus,
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

    private async Task<FinalInspection> SeedInspectionAsync(AppDbContext ctx, string batchNo = "BATCH001",
        InspectionItem item = InspectionItem.Dimension, DateTime? date = null)
    {
        var batch = await ctx.ProductionBatches.FirstOrDefaultAsync(b => b.BatchNo == batchNo);
        if (batch == null) batch = await SeedBatchAsync(ctx, batchNo);

        var entity = new FinalInspection
        {
            InspectionItem = item,
            InspectionDate = date ?? DateTime.Today,
            BatchNo = batchNo,
            ProductionBatchId = batch.Id,
            Quantity = 10,
            Weight = 1000,
            QualifiedQuantity = 9,
            QualifiedWeight = 950
        };
        ctx.FinalInspections.Add(entity);
        await ctx.SaveChangesAsync();
        return entity;
    }

    /// <summary>
    /// 构造一条成检到料（批次无成检到料时，成品检验不允许提交）
    /// </summary>
    private async Task SeedMrCheckAsync(AppDbContext ctx, ProductionBatch batch, string inspectionType = nameof(InspectionType.FormalInspection))
    {
        ctx.MaterialReceiveChecks.Add(new MaterialReceiveCheck
        {
            ProductionBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            ReceiveDate = DateTime.Today,
            ProcessGroupId = batch.Id, // InMemory 不校验外键，取唯一值即可
            ProcessName = "检验",
            SequenceNumber = 1,
            InspectionType = inspectionType
        });
        await ctx.SaveChangesAsync();
    }

    // ========== GetAllAsync ==========

    [Fact]
    public async Task GetAllAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAllAsync_按批次号搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedInspectionAsync(ctx, batchNo: "BATCH001");
        await SeedInspectionAsync(ctx, batchNo: "BATCH002");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "BATCH001" });

        result.Items.Should().HaveCount(1);
        result.Items[0].BatchNo.Should().Be("BATCH001");
    }

    [Fact]
    public async Task GetAllAsync_关键字无匹配_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedInspectionAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "NONEXISTENT" });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_按日期筛选_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedInspectionAsync(ctx, date: new DateTime(2024, 1, 15));
        await SeedInspectionAsync(ctx, date: new DateTime(2024, 2, 20));
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            InspectionDateFrom = new DateTime(2024, 2, 1),
            InspectionDateTo = new DateTime(2024, 2, 28)
        });

        result.Items.Should().HaveCount(1);
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        await SeedInspectionAsync(ctx);
        var id = await ctx.FinalInspections.Select(f => f.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(id);

        result.Should().NotBeNull();
        result!.BatchNo.Should().Be("BATCH001");
    }

    [Fact]
    public async Task GetByIdAsync_不存在_返回Null()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(999);

        result.Should().BeNull();
    }

    // ========== CreateAsync ==========

    [Fact]
    public async Task CreateAsync_成功创建()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedMrCheckAsync(ctx, batch);
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateFinalInspectionRequest
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            Quantity = 20,
            Weight = 2000,
            QualifiedQuantity = 18,
            QualifiedWeight = 1800,
            DefectWarehouseQuantity = 2
        });

        result.Should().NotBeNull();
        result.BatchNo.Should().Be("BATCH001");
        result.Quantity.Should().Be(20);

        var saved = await ctx.FinalInspections.FirstAsync();
        saved.Quantity.Should().Be(20);
    }

    [Fact]
    public async Task CreateAsync_定尺长度不在集合_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, lengthStatus: "Fixed");
        await SeedMrCheckAsync(ctx, batch);
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var act = () => svc.CreateAsync(new CreateFinalInspectionRequest
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            Quantity = 20,
            QualifiedQuantity = 20,
            FixedLength = "6000mm"
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*成品检验定尺长度(6000mm)不属于该订单号+主号(SO-001/M-001)下的定尺长度*");
    }

    [Fact]
    public async Task CreateAsync_定尺长度在集合_成功创建()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, lengthStatus: "Fixed");
        await SeedMrCheckAsync(ctx, batch);
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var result = await svc.CreateAsync(new CreateFinalInspectionRequest
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            Quantity = 20,
            QualifiedQuantity = 20,
            FixedLength = "4000mm"
        });

        result.Should().NotBeNull();
        result.FixedLength.Should().Be("4000mm");
    }

    [Fact]
    public async Task CreateAsync_定尺主号无定尺集合_跳过校验()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, lengthStatus: "Fixed");
        await SeedMrCheckAsync(ctx, batch);
        var svc = CreateService(ctx, CreateFixedLengthSvcMock()); // 空集合 = 主号下无定尺工单，跳过

        var result = await svc.CreateAsync(new CreateFinalInspectionRequest
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            Quantity = 20,
            QualifiedQuantity = 20,
            FixedLength = "6000mm"
        });

        result.Should().NotBeNull();
        result.FixedLength.Should().Be("6000mm");
    }

    [Fact]
    public async Task CreateAsync_预成检_定尺长度不在集合_跳过归属校验()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, lengthStatus: "Fixed");
        await SeedMrCheckAsync(ctx, batch, nameof(InspectionType.PreInspection));
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var result = await svc.CreateAsync(new CreateFinalInspectionRequest
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            Quantity = 20,
            QualifiedQuantity = 20,
            FixedLength = "6000mm", // 不在集合，但预成检跳过归属校验
            InspectionType = MES.Core.Enums.InspectionType.PreInspection
        });

        result.Should().NotBeNull();
        result.FixedLength.Should().Be("6000mm");
        result.InspectionType.Should().Be(MES.Core.Enums.InspectionType.PreInspection);
    }

    [Fact]
    public async Task CreateAsync_定尺长度格式不正确_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, lengthStatus: "Fixed");
        await SeedMrCheckAsync(ctx, batch);
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var act = () => svc.CreateAsync(new CreateFinalInspectionRequest
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            Quantity = 20,
            QualifiedQuantity = 20,
            FixedLength = "abc"
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*定尺长度格式不正确(abc)*");
    }

    [Fact]
    public async Task CreateAsync_无成检到料_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        await SeedBatchAsync(ctx); // 批次无成检到料
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateFinalInspectionRequest
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            Quantity = 20,
            QualifiedQuantity = 20
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*无成检到料*");
    }

    [Fact]
    public async Task CreateAsync_支数不平衡_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedMrCheckAsync(ctx, batch);
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateFinalInspectionRequest
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            Quantity = 20,
            QualifiedQuantity = 18
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*检验支数(20) ≠ 合格支数(18)*");
    }

    [Fact]
    public async Task CreateAsync_让步放行大于合格支数_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedMrCheckAsync(ctx, batch);
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateFinalInspectionRequest
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            Quantity = 20,
            QualifiedQuantity = 20,
            QualifiedConcessionQuantity = 21
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*让步放行支数(21)不能大于合格支数(20)*");
    }

    [Fact]
    public async Task CreateAsync_指定到料不含的成检类型_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedMrCheckAsync(ctx, batch, nameof(InspectionType.FormalInspection)); // 到料只有正式成检
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateFinalInspectionRequest
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            Quantity = 20,
            QualifiedQuantity = 20,
            InspectionType = MES.Core.Enums.InspectionType.PreInspection
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*成检到料不含*");
    }

    [Fact]
    public async Task CreateAsync_指定到料含的成检类型_成功()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedMrCheckAsync(ctx, batch, nameof(InspectionType.FormalInspection));
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateFinalInspectionRequest
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            Quantity = 20,
            QualifiedQuantity = 20,
            InspectionType = MES.Core.Enums.InspectionType.FormalInspection
        });

        result.InspectionType.Should().Be(MES.Core.Enums.InspectionType.FormalInspection);
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_成功更新()
    {
        var ctx = CreateDbContext();
        await SeedInspectionAsync(ctx);
        var id = await ctx.FinalInspections.Select(f => f.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateFinalInspectionRequest
        {
            InspectionDate = DateTime.Today,
            Quantity = 15,
            QualifiedQuantity = 14,
            DefectReworkQuantity = 1
        });

        result.Quantity.Should().Be(15);
        result.QualifiedQuantity.Should().Be(14);
        result.DefectReworkQuantity.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateFinalInspectionRequest { InspectionDate = DateTime.Today });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task UpdateAsync_定尺长度不在集合_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        await SeedBatchAsync(ctx, lengthStatus: "Fixed");
        await SeedInspectionAsync(ctx);
        var id = await ctx.FinalInspections.Select(f => f.Id).FirstAsync();
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var act = () => svc.UpdateAsync(id, new UpdateFinalInspectionRequest
        {
            InspectionDate = DateTime.Today,
            Quantity = 10,
            QualifiedQuantity = 10,
            FixedLength = "6000mm"
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*成品检验定尺长度(6000mm)不属于该订单号+主号(SO-001/M-001)下的定尺长度*");
    }

    [Fact]
    public async Task UpdateAsync_预成检_定尺长度不在集合_跳过归属校验()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, lengthStatus: "Fixed");
        await SeedMrCheckAsync(ctx, batch, nameof(InspectionType.PreInspection));
        await SeedInspectionAsync(ctx); // 默认 InspectionType=null
        var id = await ctx.FinalInspections.Select(f => f.Id).FirstAsync();
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var result = await svc.UpdateAsync(id, new UpdateFinalInspectionRequest
        {
            InspectionDate = DateTime.Today,
            Quantity = 10,
            QualifiedQuantity = 10,
            FixedLength = "6000mm", // 不在集合，但预成检跳过归属校验
            InspectionType = MES.Core.Enums.InspectionType.PreInspection
        });

        result.Should().NotBeNull();
        result.FixedLength.Should().Be("6000mm");
        result.InspectionType.Should().Be(MES.Core.Enums.InspectionType.PreInspection);
    }

    [Fact]
    public async Task UpdateAsync_改成检类型为到料集合内_成功()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedMrCheckAsync(ctx, batch, nameof(InspectionType.FormalInspection));
        await SeedInspectionAsync(ctx); // 默认 InspectionType=null
        var id = await ctx.FinalInspections.Select(f => f.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateFinalInspectionRequest
        {
            InspectionDate = DateTime.Today,
            Quantity = 10,
            QualifiedQuantity = 10,
            InspectionType = MES.Core.Enums.InspectionType.FormalInspection
        });

        result.InspectionType.Should().Be(MES.Core.Enums.InspectionType.FormalInspection);
    }

    [Fact]
    public async Task UpdateAsync_改成检类型为到料集合外_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedMrCheckAsync(ctx, batch, nameof(InspectionType.FormalInspection)); // 到料只有正式成检
        await SeedInspectionAsync(ctx);
        var id = await ctx.FinalInspections.Select(f => f.Id).FirstAsync();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(id, new UpdateFinalInspectionRequest
        {
            InspectionDate = DateTime.Today,
            Quantity = 10,
            QualifiedQuantity = 10,
            InspectionType = MES.Core.Enums.InspectionType.PreInspection
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*不含*");
    }

    [Fact]
    public async Task UpdateAsync_不传成检类型_保留原值()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedMrCheckAsync(ctx, batch, nameof(InspectionType.FormalInspection));
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = batch.BatchNo,
            ProductionBatchId = batch.Id,
            InspectionType = nameof(InspectionType.FormalInspection),
            Quantity = 10,
            QualifiedQuantity = 10
        });
        await ctx.SaveChangesAsync();
        var id = await ctx.FinalInspections.Select(f => f.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateFinalInspectionRequest
        {
            InspectionDate = DateTime.Today,
            Quantity = 10,
            QualifiedQuantity = 10
        });

        result.InspectionType.Should().Be(MES.Core.Enums.InspectionType.FormalInspection);
    }

    // ========== BatchCreateAsync ==========

    [Fact]
    public async Task BatchCreateAsync_成功批量创建()
    {
        var ctx = CreateDbContext();
        var b1 = await SeedBatchAsync(ctx, "BATCH001");
        var b2 = await SeedBatchAsync(ctx, "BATCH002");
        await SeedMrCheckAsync(ctx, b1);
        await SeedMrCheckAsync(ctx, b2);
        var svc = CreateService(ctx);

        var result = await svc.BatchCreateAsync(new List<CreateFinalInspectionRequest>
        {
            new() { InspectionItem = InspectionItem.Dimension, InspectionDate = DateTime.Today, BatchNo = "BATCH001", Quantity = 10, QualifiedQuantity = 10 },
            new() { InspectionItem = InspectionItem.HydrostaticPressure, InspectionDate = DateTime.Today, BatchNo = "BATCH002", Quantity = 20, QualifiedQuantity = 20 }
        });

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task BatchCreateAsync_空列表_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.BatchCreateAsync(new List<CreateFinalInspectionRequest>());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task BatchCreateAsync_批次不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.BatchCreateAsync(new List<CreateFinalInspectionRequest>
        {
            new() { InspectionItem = InspectionItem.Dimension, InspectionDate = DateTime.Today, BatchNo = "NONEXISTENT" }
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task BatchCreateAsync_定尺长度不在集合_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        await SeedBatchAsync(ctx, "BATCH001", "Fixed");
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var act = () => svc.BatchCreateAsync(new List<CreateFinalInspectionRequest>
        {
            new() { InspectionItem = InspectionItem.Dimension, InspectionDate = DateTime.Today, BatchNo = "BATCH001", Quantity = 10, QualifiedQuantity = 10, FixedLength = "6000mm" }
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*第1行：成品检验定尺长度(6000mm)不属于该订单号+主号(SO-001/M-001)下的定尺长度*");
    }

    [Fact]
    public async Task BatchCreateAsync_预成检_定尺长度不在集合_跳过归属校验()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH001", "Fixed");
        await SeedMrCheckAsync(ctx, batch, nameof(InspectionType.PreInspection));
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var result = await svc.BatchCreateAsync(new List<CreateFinalInspectionRequest>
        {
            new() { InspectionItem = InspectionItem.Dimension, InspectionDate = DateTime.Today, BatchNo = "BATCH001", Quantity = 10, QualifiedQuantity = 10, FixedLength = "6000mm", InspectionType = MES.Core.Enums.InspectionType.PreInspection }
        });

        result.Should().HaveCount(1);
        result[0].FixedLength.Should().Be("6000mm");
        result[0].InspectionType.Should().Be(MES.Core.Enums.InspectionType.PreInspection);
    }

    [Fact]
    public async Task BatchCreateAsync_无成检到料_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        await SeedBatchAsync(ctx, "BATCH001"); // 批次无成检到料
        var svc = CreateService(ctx);

        var act = () => svc.BatchCreateAsync(new List<CreateFinalInspectionRequest>
        {
            new() { InspectionItem = InspectionItem.Dimension, InspectionDate = DateTime.Today, BatchNo = "BATCH001", Quantity = 10, QualifiedQuantity = 10 }
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*无成检到料*");
    }

    // ========== LookupBatchAsync ==========

    [Fact]
    public async Task LookupBatchAsync_存在_返回批次信息()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedMrCheckAsync(ctx, batch);
        var svc = CreateService(ctx);

        var result = await svc.LookupBatchAsync("BATCH001");

        result.Should().NotBeNull();
        result!.ProductionBatchId.Should().Be(batch.Id);
        result.ManufacturingItem.Should().Be("OrderFinished");
        result.InspectionType.Should().Be(MES.Core.Enums.InspectionType.FormalInspection);
    }

    [Fact]
    public async Task LookupBatchAsync_无到料_成检类型为空()
    {
        var ctx = CreateDbContext();
        await SeedBatchAsync(ctx); // 批次无成检到料
        var svc = CreateService(ctx);

        var result = await svc.LookupBatchAsync("BATCH001");

        result.Should().NotBeNull();
        result!.InspectionType.Should().BeNull();
    }

    [Fact]
    public async Task LookupBatchAsync_不存在_返回Null()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.LookupBatchAsync("NONEXISTENT");

        result.Should().BeNull();
    }

    [Fact]
    public async Task LookupBatchAsync_空参数_返回Null()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.LookupBatchAsync("");

        result.Should().BeNull();
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        await SeedInspectionAsync(ctx);
        var id = await ctx.FinalInspections.Select(f => f.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var deleted = await ctx.FinalInspections.FindAsync(id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== B10/B11 专项测试 ==========

    [Fact]
    public async Task GetAllAsync_按更新时间排序_成功()
    {
        var ctx = CreateDbContext();
        // 第一条记录的 UpdatedTime 默认会被设为当前时间
        var i1 = await SeedInspectionAsync(ctx, batchNo: "BATCH001");
        // 等待短暂时间再创建第二条，确保 UpdatedTime 不同
        await Task.Delay(100);
        var i2 = await SeedInspectionAsync(ctx, batchNo: "BATCH002");
        var svc = CreateService(ctx);

        // 降序：最新的在前
        var resultAsc = await svc.GetAllAsync(new QueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "updatedtime", IsDescending = false });

        resultAsc.Items.Should().HaveCount(2);
        resultAsc.Items[0].Id.Should().Be(i1.Id);
    }

    [Fact]
    public async Task GetAllAsync_关键词搜索炉号_返回匹配()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH001");
        batch.SourceHeatNo = "FUR-001";
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            ProductionBatchId = batch.Id,
            Quantity = 10,
            Weight = 1000
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "FUR-001" });

        result.Items.Should().HaveCount(1);
        result.Items[0].FurnaceNo.Should().Be("FUR-001");
    }

    [Fact]
    public async Task GetAllAsync_关键词搜索备注_返回匹配()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH001");
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            ProductionBatchId = batch.Id,
            Quantity = 10,
            Weight = 1000,
            Remark = "测试备注"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "测试备注" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Remark.Should().Be("测试备注");
    }

    // ========== 筛选测试（FilterDescriptor） ==========

    [Fact]
    public async Task GetAllAsync_Filters_BatchNoContains_返回匹配()
    {
        var ctx = CreateDbContext();
        var batch1 = await SeedBatchAsync(ctx, "BATCH001");
        var batch2 = await SeedBatchAsync(ctx, "BATCH002");
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            ProductionBatchId = batch1.Id,
            Quantity = 10
        });
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH002",
            ProductionBatchId = batch2.Id,
            Quantity = 20
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "BatchNo", Operator = "contains", Value = "BATCH001" }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].BatchNo.Should().Be("BATCH001");
    }

    [Fact]
    public async Task GetAllAsync_Filters_Keyword_返回匹配()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH001");
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            ProductionBatchId = batch.Id,
            Quantity = 10,
            Operator = "操作员A"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Keyword = "操作员A"
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].Operator.Should().Be("操作员A");
    }

    [Fact]
    public async Task GetAllAsync_Filters_NoMatch_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedInspectionAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "BatchNo", Operator = "contains", Value = "NONEXISTENT" }
            }
        });

        result.Items.Should().BeEmpty();
    }

    // ========== GetFilterContextsAsync ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        var batch1 = await SeedBatchAsync(ctx, "BATCH001");
        var batch2 = await SeedBatchAsync(ctx, "BATCH002");
        batch2.PlantGrade = "316L";
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            ProductionBatchId = batch1.Id,
            Quantity = 10
        });
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH002",
            ProductionBatchId = batch2.Id,
            Quantity = 20
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("BatchNo");
        contexts["BatchNo"].Should().BeEquivalentTo(new[] { "BATCH001", "BATCH002" }, opts => opts.WithStrictOrdering());
        contexts.Should().ContainKey("PlantGrade");
        contexts["PlantGrade"].Should().BeEquivalentTo(new[] { "304", "316L" }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["BatchNo"].Should().BeEmpty();
        contexts["PlantGrade"].Should().BeEmpty();
        contexts.Should().NotContainKey("ManufacturingItem");
    }

    [Fact]
    public async Task GetFilterContextsAsync_Nullable字段排除null()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            ProductionBatchId = batch.Id,
            Quantity = 10
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["BatchNo"].Should().HaveCount(1);
        contexts["TagNo"].Should().BeEmpty();
    }

    // ========== GetFinalInspectionHealthSummaryAsync ==========

    [Fact]
    public async Task GetFinalInspectionHealthSummaryAsync_成检类型与到料不符_分类列出生产编号()
    {
        var ctx = CreateDbContext();

        // 批次1：有成检到料（正式成检）；两条成品检验：正式=正常，预成检=成检类型疑问
        var batch1 = await SeedBatchAsync(ctx, "BATCH001");
        await SeedMrCheckAsync(ctx, batch1, nameof(InspectionType.FormalInspection));
        await AddFinalInspection(ctx, batch1, nameof(InspectionType.FormalInspection));
        await AddFinalInspection(ctx, batch1, nameof(InspectionType.PreInspection));

        // 批次2：无成检到料却有成品检验 → 无成检到料
        var batch2 = await SeedBatchAsync(ctx, "BATCH002");
        await AddFinalInspection(ctx, batch2, nameof(InspectionType.FormalInspection));

        var svc = CreateService(ctx);

        var summary = await svc.GetFinalInspectionHealthSummaryAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        summary.TotalCount.Should().Be(3);
        summary.InspectionTypeMismatchBatchNos.Should().Contain("BATCH001");
        summary.NoMaterialCheckBatchNos.Should().Contain("BATCH002");
        summary.InspectionTypeMismatchCount.Should().Be(1);
        summary.NoMaterialCheckCount.Should().Be(1);
        summary.NormalCount.Should().Be(1);
        summary.IssueCount.Should().Be(2);
    }

    // ========== 定尺切割长度匹配标识（CutLengthMatchType）==========

    /// <summary>
    /// 构造 wo 集合与 main 集合不同的 Mock，用于区分「完全匹配」与「主号匹配」。
    /// </summary>
    private static IFixedLengthWorkOrderService CreateFixedLengthSvcMockWithDiff(
        decimal[] woLengths, decimal[] mainLengths)
    {
        var mock = new Mock<IFixedLengthWorkOrderService>();
        mock.Setup(s => s.GetLengthsByMainNoAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new HashSet<decimal>(mainLengths));
        mock.Setup(s => s.GetLengthsByWorkOrderNoAsync(It.IsAny<string>()))
            .ReturnsAsync(new HashSet<decimal>(woLengths));
        mock.Setup(s => s.GetLengthMapsAsync())
            .ReturnsAsync(new FixedLengthLengthMaps
            {
                ByWorkOrderNo = { ["WO-001"] = new HashSet<decimal>(woLengths) },
                ByMainKey = { ["SO-001|M-001"] = new HashSet<decimal>(mainLengths) }
            });
        return mock.Object;
    }

    [Fact]
    public async Task CreateAsync_正式成检定尺_长度命中本工单号集合_完全匹配()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, lengthStatus: "Fixed");
        await SeedMrCheckAsync(ctx, batch); // 默认正式成检
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var result = await svc.CreateAsync(new CreateFinalInspectionRequest
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            Quantity = 20,
            QualifiedQuantity = 20,
            FixedLength = "4000mm" // 属于本工单号定尺集合
        });

        result.CutLengthMatchType.Should().Be(CutLengthMatchType.FullMatch);
        result.CutLengthMatchTypeDisplay.Should().Be("完全匹配");

        var saved = await ctx.FinalInspections.FirstAsync();
        saved.CutLengthMatchType.Should().Be(nameof(CutLengthMatchType.FullMatch));
    }

    [Fact]
    public async Task CreateAsync_正式成检定尺_仅命中同主号集合_主号匹配()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, lengthStatus: "Fixed");
        await SeedMrCheckAsync(ctx, batch);
        // 6000 仅在同主号集合（跨次号），不在本工单号集合
        var svc = CreateService(ctx, CreateFixedLengthSvcMockWithDiff(new[] { 4000m, 8000m }, new[] { 4000m, 8000m, 6000m }));

        var result = await svc.CreateAsync(new CreateFinalInspectionRequest
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            Quantity = 20,
            QualifiedQuantity = 20,
            FixedLength = "6000mm"
        });

        result.CutLengthMatchType.Should().Be(CutLengthMatchType.MainNoMatch);
        result.CutLengthMatchTypeDisplay.Should().Be("主号匹配");
    }

    [Fact]
    public async Task CreateAsync_预成检_匹配标识为空()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, lengthStatus: "Fixed");
        await SeedMrCheckAsync(ctx, batch, nameof(InspectionType.PreInspection));
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var result = await svc.CreateAsync(new CreateFinalInspectionRequest
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            Quantity = 20,
            QualifiedQuantity = 20,
            FixedLength = "4000mm", // 预成检不计算匹配标识
            InspectionType = MES.Core.Enums.InspectionType.PreInspection
        });

        result.CutLengthMatchType.Should().BeNull();
        result.CutLengthMatchTypeDisplay.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_非定尺批次_匹配标识为空()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, lengthStatus: "NonFixed"); // 非定尺
        await SeedMrCheckAsync(ctx, batch);
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var result = await svc.CreateAsync(new CreateFinalInspectionRequest
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            Quantity = 20,
            QualifiedQuantity = 20
        });

        result.CutLengthMatchType.Should().BeNull();
    }

    [Fact]
    public async Task BatchCreateAsync_正式成检定尺_计算匹配标识()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, lengthStatus: "Fixed");
        await SeedMrCheckAsync(ctx, batch);
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var result = await svc.BatchCreateAsync(new List<CreateFinalInspectionRequest>
        {
            new()
            {
                InspectionItem = InspectionItem.Dimension,
                InspectionDate = DateTime.Today,
                BatchNo = "BATCH001",
                Quantity = 20,
                QualifiedQuantity = 20,
                FixedLength = "8000mm"
            }
        });

        result[0].CutLengthMatchType.Should().Be(CutLengthMatchType.FullMatch);
        var saved = await ctx.FinalInspections.FirstAsync();
        saved.CutLengthMatchType.Should().Be(nameof(CutLengthMatchType.FullMatch));
    }

    [Fact]
    public async Task UpdateAsync_改定尺长度_重算匹配标识()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, lengthStatus: "Fixed");
        await SeedMrCheckAsync(ctx, batch);
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            ProductionBatchId = batch.Id,
            InspectionType = nameof(InspectionType.FormalInspection),
            FixedLength = "4000mm",
            Quantity = 10,
            QualifiedQuantity = 10
        });
        await ctx.SaveChangesAsync();
        var id = await ctx.FinalInspections.Select(f => f.Id).FirstAsync();

        var svc = CreateService(ctx, CreateFixedLengthSvcMockWithDiff(new[] { 4000m, 8000m }, new[] { 4000m, 8000m, 6000m }));

        // 6000 命中主号集合但非本工单号集合 → 重算为「主号匹配」
        var result = await svc.UpdateAsync(id, new UpdateFinalInspectionRequest
        {
            InspectionDate = DateTime.Today,
            Quantity = 10,
            QualifiedQuantity = 10,
            FixedLength = "6000mm"
        });

        result.CutLengthMatchType.Should().Be(CutLengthMatchType.MainNoMatch);
        var saved = await ctx.FinalInspections.FirstAsync();
        saved.CutLengthMatchType.Should().Be(nameof(CutLengthMatchType.MainNoMatch));
    }

    [Fact]
    public async Task UpdateAsync_预成检_重算匹配标识置空()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, lengthStatus: "Fixed");
        await SeedMrCheckAsync(ctx, batch, nameof(InspectionType.PreInspection));
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            ProductionBatchId = batch.Id,
            InspectionType = nameof(InspectionType.PreInspection),
            FixedLength = "4000mm",
            CutLengthMatchType = nameof(CutLengthMatchType.FullMatch), // 模拟历史残留
            Quantity = 10,
            QualifiedQuantity = 10
        });
        await ctx.SaveChangesAsync();
        var id = await ctx.FinalInspections.Select(f => f.Id).FirstAsync();
        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));

        var result = await svc.UpdateAsync(id, new UpdateFinalInspectionRequest
        {
            InspectionDate = DateTime.Today,
            Quantity = 10,
            QualifiedQuantity = 10,
            FixedLength = "4000mm"
        });

        result.CutLengthMatchType.Should().BeNull();
        var saved = await ctx.FinalInspections.FirstAsync();
        saved.CutLengthMatchType.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAllCutLengthMatchAsync_正式成检回填_预成检保持空()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, lengthStatus: "Fixed");
        await SeedMrCheckAsync(ctx, batch); // 正式成检到料
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            ProductionBatchId = batch.Id,
            InspectionType = nameof(InspectionType.FormalInspection),
            FixedLength = "4000mm",
            Quantity = 10,
            QualifiedQuantity = 10
        });
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.VisualInspection,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            ProductionBatchId = batch.Id,
            InspectionType = nameof(InspectionType.PreInspection),
            FixedLength = "4000mm",
            Quantity = 10,
            QualifiedQuantity = 10
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));
        var updated = await svc.RefreshAllCutLengthMatchAsync();

        updated.Should().Be(1);
        var formal = await ctx.FinalInspections.SingleAsync(f => f.InspectionType == nameof(InspectionType.FormalInspection));
        formal.CutLengthMatchType.Should().Be(nameof(CutLengthMatchType.FullMatch));
        var pre = await ctx.FinalInspections.SingleAsync(f => f.InspectionType == nameof(InspectionType.PreInspection));
        pre.CutLengthMatchType.Should().BeNull();
    }

    [Fact]
    public async Task RecomputeCutLengthMatchByBatchAsync_批次LengthStatus改非定尺_匹配标识置空()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, lengthStatus: "Fixed");
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = batch.BatchNo,
            ProductionBatchId = batch.Id,
            InspectionType = nameof(InspectionType.FormalInspection),
            FixedLength = "4000mm",
            Quantity = 10,
            QualifiedQuantity = 10,
            CutLengthMatchType = nameof(CutLengthMatchType.FullMatch) // 旧值（批次编辑后应置空）
        });
        await ctx.SaveChangesAsync();

        // 模拟批次编辑把 LengthStatus 从定尺改为非定尺
        var batchEntity = await ctx.ProductionBatches.FirstAsync();
        batchEntity.LengthStatus = "NonFixed";
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));
        var updated = await svc.RecomputeCutLengthMatchByBatchAsync(batch.Id);

        updated.Should().Be(1);
        var saved = await ctx.FinalInspections.SingleAsync();
        saved.CutLengthMatchType.Should().BeNull();
    }

    [Fact]
    public async Task RecomputeCutLengthMatchByBatchAsync_批次LengthStatus改定尺_匹配标识重算()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, lengthStatus: "NonFixed");
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = batch.BatchNo,
            ProductionBatchId = batch.Id,
            InspectionType = nameof(InspectionType.FormalInspection),
            FixedLength = "4000mm",
            Quantity = 10,
            QualifiedQuantity = 10
        });
        await ctx.SaveChangesAsync();

        // 模拟批次编辑把 LengthStatus 从非定尺改为定尺
        var batchEntity = await ctx.ProductionBatches.FirstAsync();
        batchEntity.LengthStatus = "Fixed";
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, CreateFixedLengthSvcMock(4000m, 8000m));
        var updated = await svc.RecomputeCutLengthMatchByBatchAsync(batch.Id);

        updated.Should().Be(1);
        var saved = await ctx.FinalInspections.SingleAsync();
        saved.CutLengthMatchType.Should().Be(nameof(CutLengthMatchType.FullMatch));
    }

    private async Task AddFinalInspection(AppDbContext ctx, ProductionBatch batch, string? inspectionType)
    {
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = batch.BatchNo,
            ProductionBatchId = batch.Id,
            InspectionType = inspectionType,
            Quantity = 10
        });
        await ctx.SaveChangesAsync();
    }
}
