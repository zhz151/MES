using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Services.Warehouse;
using MES.Tests.Tests;
using MES.Data;
using MES.Data.Entities;
using Moq;

namespace MES.Tests.Services;

/// <summary>
/// 库存服务测试：入库、出库、批次扣减、异常分支
/// </summary>
public class InventoryServiceTests : TestBase
{
    private InventoryService CreateService(AppDbContext ctx)
    {
        var httpMock = new Mock<IHttpContextAccessor>();
        httpMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        var workOrderExecMock = new Mock<IWorkOrderExecutionService>();
        var loggerMock = new Mock<ILogger<InventoryService>>();
        var qptMock = new Mock<IQualityProcessTrackingService>();
        return new InventoryService(ctx, httpMock.Object, configMock.Object, workOrderExecMock.Object, qptMock.Object, loggerMock.Object, Mock.Of<IMemoryCache>());
    }

    // ========== 入库 ==========

    [Fact]
    public async Task InboundAsync_仓库不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = 999,
            MaterialType = "无缝管",
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = "采购",
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("仓库不存在");
    }

    [Fact]
    public async Task InboundAsync_成功入库_剩余量等于初始量()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = "无缝管",
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = "采购",
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m,
            InboundDate = DateTime.Today
        });

        result.Should().NotBeNull();
        result.BatchNo.Should().StartWith("CK");
        result.RemainingQuantity.Should().Be(10);
        result.RemainingWeight.Should().Be(1000m);
    }

    // ========== 出库 ==========

    [Fact]
    public async Task OutboundAsync_批次不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = 999,
            OutboundQuantity = 1,
            OutboundWeight = 100m,
            OutboundType = "SalesOut",
            TargetCompany = "客户X",
            OutboundDate = DateTime.Today
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("批次不存在");
    }

    [Fact]
    public async Task OutboundAsync_数量不足_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = "无缝管",
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = "采购",
            SourceName = "供应商A",
            InitialQuantity = 5,
            InitialWeight = 500m
        });

        var act = () => svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 10,
            OutboundWeight = 100m,
            OutboundType = "SalesOut",
            TargetCompany = "客户X",
            OutboundDate = DateTime.Today
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*剩余支数不足*");
    }

    [Fact]
    public async Task OutboundAsync_重量不足_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = "无缝管",
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = "采购",
            SourceName = "供应商A",
            InitialQuantity = 5,
            InitialWeight = 500m
        });

        var act = () => svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 1,
            OutboundWeight = 600m,
            OutboundType = "SalesOut",
            TargetCompany = "客户X",
            OutboundDate = DateTime.Today
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*剩余重量不足*");
    }

    [Fact]
    public async Task OutboundAsync_成功出库_剩余量正确扣减()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = "无缝管",
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = "采购",
            SourceName = "供应商A",
            InitialQuantity = 10,
            InitialWeight = 1000m
        });

        var record = await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id,
            OutboundQuantity = 3,
            OutboundWeight = 300m,
            OutboundType = "SalesOut",
            TargetCompany = "客户X",
            OutboundDate = DateTime.Today
        });

        record.Should().NotBeNull();
        record.OutboundQuantity.Should().Be(3);
        record.OutboundWeight.Should().Be(300m);

        // 验证批次剩余量已更新
        var updated = await svc.GetByIdAsync(batch.Id);
        updated.RemainingQuantity.Should().Be(7);
        updated.RemainingWeight.Should().Be(700m);
    }

    // ========== 批量出库 ==========

    [Fact]
    public async Task BatchOutboundAsync_部分批次库存不足_事务全部回滚()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var b1 = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id, MaterialType = "无缝管",
            PlantGrade = "Q345B", Specification = "219*8",
            InboundSource = "采购", SourceName = "供应商A",
            InitialQuantity = 2, InitialWeight = 200m
        });

        var b2 = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id, MaterialType = "无缝管",
            PlantGrade = "Q345B", Specification = "219*8",
            InboundSource = "采购", SourceName = "供应商A",
            InitialQuantity = 5, InitialWeight = 500m
        });

        var act = () => svc.BatchOutboundAsync(new BatchOutboundRequest
        {
            OutboundType = "SalesOut",
            TargetCompany = "客户Y",
            OutboundDate = DateTime.Today,
            Items = new List<OutboundItemRequest>
            {
                new() { InventoryBatchId = b1.Id, OutboundQuantity = 1, OutboundWeight = 100m },
                new() { InventoryBatchId = b2.Id, OutboundQuantity = 10, OutboundWeight = 100m } // 不足
            }
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*剩余支数不足*");

        // 验证事务回滚：第一笔出库也被回滚
        var updatedB1 = await svc.GetByIdAsync(b1.Id);
        updatedB1.RemainingQuantity.Should().Be(2);
    }

    // ========== 批量入库 ==========

    [Fact]
    public async Task BatchInboundAsync_仓库不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.BatchInboundAsync(new BatchInboundRequest
        {
            WarehouseId = 999,
            MaterialType = "无缝管",
            Rows = new List<InboundRow>
            {
                new() { InitialQuantity = 1, InitialWeight = 100m }
            }
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("仓库不存在");
    }

    [Fact]
    public async Task BatchInboundAsync_成功批量入库()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.BatchInboundAsync(new BatchInboundRequest
        {
            WarehouseId = wh.Id,
            MaterialType = "无缝管",
            PlantGrade = "Q345B",
            Specification = "219*8",
            InboundSource = "采购",
            SourceName = "供应商A",
            Rows = new List<InboundRow>
            {
                new() { InitialQuantity = 5, InitialWeight = 500m },
                new() { InitialQuantity = 10, InitialWeight = 1000m }
            }
        });

        result.Should().NotBeNull();
        result.SuccessCount.Should().Be(2);
        result.BatchNos.Should().HaveCount(2);
    }

    // ========== 查询 ==========

    [Fact]
    public async Task GetPagedAsync_关键字搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id, MaterialType = "无缝管",
            PlantGrade = "Q345B", Specification = "219*8",
            InboundSource = "采购", SourceName = "供应商A",
            InitialQuantity = 10, InitialWeight = 1000m
        });

        await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id, MaterialType = "焊管",
            PlantGrade = "Q235B", Specification = "159*6",
            InboundSource = "采购", SourceName = "供应商B",
            InitialQuantity = 20, InitialWeight = 2000m
        });

        var result = await svc.GetPagedAsync(new InventoryQueryParams
        {
            Keyword = "无缝管",
            PageIndex = 0,
            PageSize = 10
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].MaterialType.Should().Be("无缝管");
    }

    [Fact]
    public async Task GetPagedAsync_OnlyWithStock_只返回有库存批次()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id, MaterialType = "无缝管",
            PlantGrade = "Q345B", Specification = "219*8",
            InboundSource = "采购", SourceName = "供应商A",
            InitialQuantity = 10, InitialWeight = 1000m
        });

        var b2 = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id, MaterialType = "焊管",
            PlantGrade = "Q235B", Specification = "159*6",
            InboundSource = "采购", SourceName = "供应商B",
            InitialQuantity = 5, InitialWeight = 500m
        });

        // 把 b2 出库到零
        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = b2.Id, OutboundQuantity = 5,
            OutboundWeight = 500m, OutboundType = "SalesOut",
            TargetCompany = "客户X", OutboundDate = DateTime.Today
        });

        var result = await svc.GetPagedAsync(new InventoryQueryParams
        {
            OnlyWithStock = true,
            PageIndex = 0,
            PageSize = 10
        });

        result.Items.Should().HaveCount(1);
    }

    // ========== 更新 ==========

    [Fact]
    public async Task UpdateInventoryBatchAsync_批次不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateInventoryBatchAsync(999, new UpdateInventoryBatchRequest
        {
            BatchNo = "NEW-BATCH"
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("入库批次不存在");
    }

    [Fact]
    public async Task UpdateInventoryBatchAsync_修改数量_剩余量同步更新()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id, MaterialType = "无缝管",
            PlantGrade = "Q345B", Specification = "219*8",
            InboundSource = "采购", SourceName = "供应商A",
            InitialQuantity = 10, InitialWeight = 1000m
        });

        var updated = await svc.UpdateInventoryBatchAsync(batch.Id, new UpdateInventoryBatchRequest
        {
            InitialQuantity = 20,
            InitialWeight = 2000m
        });

        updated.InitialQuantity.Should().Be(20);
        updated.RemainingQuantity.Should().Be(20);
        updated.InitialWeight.Should().Be(2000m);
        updated.RemainingWeight.Should().Be(2000m);
    }

    // ========== 物理删除 ==========

    [Fact]
    public async Task HardDeleteInventoryBatchAsync_批次不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.HardDeleteInventoryBatchAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("入库批次不存在");
    }

    [Fact]
    public async Task HardDeleteInventoryBatchAsync_有出库记录_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id, MaterialType = "无缝管",
            PlantGrade = "Q345B", Specification = "219*8",
            InboundSource = "采购", SourceName = "供应商A",
            InitialQuantity = 10, InitialWeight = 1000m
        });

        var outRecord = await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id, OutboundQuantity = 3,
            OutboundWeight = 300m, OutboundType = "SalesOut",
            TargetCompany = "客户X", OutboundDate = DateTime.Today
        });

        // 有出库记录时无法删除批次
        var act = () => svc.HardDeleteInventoryBatchAsync(batch.Id);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*存在出库记录*");

        // 先删除出库记录，再删除批次
        await svc.HardDeleteOutboundRecordAsync(outRecord.Id);
        await svc.HardDeleteInventoryBatchAsync(batch.Id);

        // 验证已删除
        var getAct = () => svc.GetByIdAsync(batch.Id);
        await getAct.Should().ThrowAsync<BusinessException>().WithMessage("批次不存在");
    }

    // ========== 出库记录 ==========

    [Fact]
    public async Task GetOutboundRecordsAsync_按条件筛选()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id, MaterialType = "无缝管",
            PlantGrade = "Q345B", Specification = "219*8",
            InboundSource = "采购", SourceName = "供应商A",
            InitialQuantity = 10, InitialWeight = 1000m
        });

        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id, OutboundQuantity = 2,
            OutboundWeight = 200m, OutboundType = "SalesOut",
            TargetCompany = "客户A", OutboundDate = DateTime.Today
        });
        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id, OutboundQuantity = 3,
            OutboundWeight = 300m, OutboundType = "TransferOut",
            TargetCompany = "客户B", OutboundDate = DateTime.Today
        });

        var result = await svc.GetOutboundRecordsAsync(new OutboundQueryParams
        {
            OutboundType = "SalesOut",
            PageIndex = 0,
            PageSize = 10
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].OutboundType.Should().Be("SalesOut");
        result.Items[0].BatchNo.Should().Be(batch.BatchNo);
        result.TotalCount.Should().Be(1);
    }

    // ========== B11 专项测试 ==========

    [Fact]
    public async Task GetPagedAsync_关键词搜索区域_返回匹配()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id, MaterialType = "无缝管",
            PlantGrade = "Q345B", Specification = "219*8",
            InboundSource = "采购", SourceName = "供应商A",
            InitialQuantity = 10, InitialWeight = 1000m
        });

        var batch = await ctx.InventoryBatches.OrderByDescending(b => b.Id).FirstAsync();
        batch.LocationArea = "A区-3排";
        await ctx.SaveChangesAsync();

        var result = await svc.GetPagedAsync(new InventoryQueryParams
        { PageIndex = 0, PageSize = 10, Keyword = "A区" });

        result.Items.Should().HaveCount(1);
        result.Items[0].LocationArea.Should().Be("A区-3排");
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索备注_返回匹配()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id, MaterialType = "无缝管",
            PlantGrade = "Q345B", Specification = "219*8",
            InboundSource = "采购", SourceName = "供应商A",
            InitialQuantity = 10, InitialWeight = 1000m
        });

        var batch = await ctx.InventoryBatches.OrderByDescending(b => b.Id).FirstAsync();
        batch.Remark = "库存批次备注";
        await ctx.SaveChangesAsync();

        var result = await svc.GetPagedAsync(new InventoryQueryParams
        { PageIndex = 0, PageSize = 10, Keyword = "库存批次" });

        result.Items[0].Remark.Should().Be("库存批次备注");
    }

    [Fact]
    public async Task GetPagedAsync_按是否关联工单排序_成功()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id, MaterialType = "无缝管",
            PlantGrade = "Q345B", Specification = "219*8",
            InboundSource = "采购", SourceName = "供应商A",
            InitialQuantity = 10, InitialWeight = 1000m
        });
        await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id, MaterialType = "焊管",
            PlantGrade = "Q235B", Specification = "159*6",
            InboundSource = "采购", SourceName = "供应商B",
            InitialQuantity = 20, InitialWeight = 2000m,
            SalesOrderNo = "SO-001"
        });

        var batches = await ctx.InventoryBatches.OrderBy(b => b.Id).ToListAsync();
        batches[0].IsLinkedToWorkOrder = false;
        batches[1].IsLinkedToWorkOrder = true;
        await ctx.SaveChangesAsync();

        var resultAsc = await svc.GetPagedAsync(new InventoryQueryParams
        { PageIndex = 0, PageSize = 20, SortBy = "islinkedtoworkorder", IsDescending = false });

        resultAsc.Items[0].IsLinkedToWorkOrder.Should().BeFalse();
        resultAsc.Items[1].IsLinkedToWorkOrder.Should().BeTrue();
    }

    // ========== 出库记录 B10 专项测试 ==========

    [Fact]
    public async Task GetOutboundRecordsAsync_按源单号排序_成功()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id, MaterialType = "无缝管",
            PlantGrade = "Q345B", Specification = "219*8",
            InboundSource = "采购", SourceName = "供应商A",
            InitialQuantity = 10, InitialWeight = 1000m
        });

        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id, OutboundQuantity = 1,
            OutboundWeight = 100m, OutboundType = "SalesOut",
            TargetCompany = "客户X", OutboundDate = DateTime.Today
        });
        // Add a second with different order so we can test ordering
        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id, OutboundQuantity = 2,
            OutboundWeight = 200m, OutboundType = "TransferOut",
            TargetCompany = "客户Y", OutboundDate = DateTime.Today
        });

        // Update source order numbers for sort testing
        var records = await ctx.OutboundRecords.OrderBy(r => r.Id).ToListAsync();
        records[0].SourceOrderNo = "B-SO";
        records[1].SourceOrderNo = "A-SO";
        await ctx.SaveChangesAsync();

        var result = await svc.GetOutboundRecordsAsync(new OutboundQueryParams
        { PageIndex = 0, PageSize = 20, SortBy = "sourceorderno", IsDescending = false });

        result.Items[0].SourceOrderNo.Should().Be("A-SO");
        result.Items[1].SourceOrderNo.Should().Be("B-SO");
    }

    [Fact]
    public async Task GetOutboundRecordsAsync_按备注排序_成功()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id, MaterialType = "无缝管",
            PlantGrade = "Q345B", Specification = "219*8",
            InboundSource = "采购", SourceName = "供应商A",
            InitialQuantity = 10, InitialWeight = 1000m
        });

        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id, OutboundQuantity = 1,
            OutboundWeight = 100m, OutboundType = "SalesOut",
            TargetCompany = "客户X", OutboundDate = DateTime.Today
        });
        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id, OutboundQuantity = 2,
            OutboundWeight = 200m, OutboundType = "TransferOut",
            TargetCompany = "客户Y", OutboundDate = DateTime.Today
        });

        var records = await ctx.OutboundRecords.OrderBy(r => r.Id).ToListAsync();
        records[0].Remark = "B备注";
        records[1].Remark = "A备注";
        await ctx.SaveChangesAsync();

        var result = await svc.GetOutboundRecordsAsync(new OutboundQueryParams
        { PageIndex = 0, PageSize = 20, SortBy = "remark", IsDescending = false });

        result.Items[0].Remark.Should().Be("A备注");
        result.Items[1].Remark.Should().Be("B备注");
    }

    [Fact]
    public async Task GetOutboundRecordsAsync_关键词搜索出库类型_返回匹配()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        var svc = CreateService(ctx);

        var batch = await svc.InboundAsync(new CreateInboundRequest
        {
            WarehouseId = wh.Id, MaterialType = "无缝管",
            PlantGrade = "Q345B", Specification = "219*8",
            InboundSource = "采购", SourceName = "供应商A",
            InitialQuantity = 10, InitialWeight = 1000m
        });

        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id, OutboundQuantity = 1,
            OutboundWeight = 100m, OutboundType = "SalesOut",
            TargetCompany = "客户X", OutboundDate = DateTime.Today
        });
        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id, OutboundQuantity = 2,
            OutboundWeight = 200m, OutboundType = "TransferOut",
            TargetCompany = "客户Y", OutboundDate = DateTime.Today
        });

        var result = await svc.GetOutboundRecordsAsync(new OutboundQueryParams
        { PageIndex = 0, PageSize = 20, Keyword = "TransferOut" });

        result.Items.Should().HaveCount(1);
        result.Items[0].OutboundType.Should().Be("TransferOut");
    }

    // ========== 库存筛选上下文 ==========

    [Fact]
    public async Task GetInventoryFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        // 直接插入 InventoryBatch
        ctx.InventoryBatches.AddRange(
            new InventoryBatch { BatchNo = "CK001", WarehouseId = wh.Id, MaterialType = "无缝管", PlantGrade = "Q345B", Specification = "219*8", InboundSource = "采购", SourceName = "供应商A", InboundDate = DateTime.Today, InitialQuantity = 10, InitialWeight = 1000m, RemainingQuantity = 10, RemainingWeight = 1000m, IsLinkedToWorkOrder = false },
            new InventoryBatch { BatchNo = "CK002", WarehouseId = wh.Id, MaterialType = "焊管", PlantGrade = "Q235B", Specification = "159*6", InboundSource = "采购", SourceName = "供应商B", InboundDate = DateTime.Today, InitialQuantity = 20, InitialWeight = 2000m, RemainingQuantity = 20, RemainingWeight = 2000m, IsLinkedToWorkOrder = true, SurfaceCondition = "酸洗", HeatNo = "H001", LocationArea = "A区", LocationRack = "R01" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetInventoryFilterContextsAsync();

        result.Should().ContainKeys("BatchNo", "InboundDate", "MaterialType", "SourceName", "PlantGrade", "Specification", "IsLinkedToWorkOrder");
        result["BatchNo"].Should().BeEquivalentTo(new[] { "CK001", "CK002" }, options => options.WithStrictOrdering());
        result["MaterialType"].Should().BeEquivalentTo(new[] { "无缝管", "焊管" });
        result["IsLinkedToWorkOrder"].Should().BeEquivalentTo(new[] { "False", "True" });
        result["SurfaceCondition"].Should().Contain("酸洗");
        result["HeatNo"].Should().Contain("H001");
        result["LocationArea"].Should().Contain("A区");
    }

    [Fact]
    public async Task GetInventoryFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetInventoryFilterContextsAsync();

        result.Should().NotBeNull();
        foreach (var kvp in result)
            kvp.Value.Should().BeEmpty($"字段 {kvp.Key} 应返回空列表");
    }

    // ========== 出库筛选上下文 ==========

    [Fact]
    public async Task GetOutboundFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        var wh = await SeedWarehouseAsync(ctx);
        // 需要 InventoryBatch 才能创建 OutboundRecord
        var batch = new InventoryBatch { BatchNo = "CK001", WarehouseId = wh.Id, MaterialType = "无缝管", PlantGrade = "Q345B", Specification = "219*8", InboundSource = "采购", SourceName = "供应商A", InboundDate = DateTime.Today, InitialQuantity = 10, InitialWeight = 1000m, RemainingQuantity = 10, RemainingWeight = 1000m };
        ctx.InventoryBatches.Add(batch);
        await ctx.SaveChangesAsync();
        ctx.OutboundRecords.AddRange(
            new OutboundRecord { InventoryBatchId = batch.Id, BatchNo = batch.BatchNo, OutboundType = Core.Enums.OutboundType.SalesOut, SourceOrderNo = "SO001", TargetCompany = "客户A", OutboundQuantity = 2, OutboundWeight = 200m, OutboundDate = DateTime.Today, CreatedBy = "user1" },
            new OutboundRecord { InventoryBatchId = batch.Id, BatchNo = batch.BatchNo, OutboundType = Core.Enums.OutboundType.TransferOut, SourceOrderNo = null, TargetCompany = null, OutboundQuantity = 3, OutboundWeight = 300m, OutboundDate = DateTime.Today, CreatedBy = "user2", Remark = "调拨" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetOutboundFilterContextsAsync();

        result.Should().ContainKeys("BatchNo", "OutboundType", "SourceOrderNo", "TargetCompany", "Remark", "CreatedBy");
        result["BatchNo"].Should().Contain("CK001");
        result["OutboundType"].Should().Contain("SalesOut").And.Contain("TransferOut");
        result["SourceOrderNo"].Should().HaveCount(1).And.Contain("SO001");
        result["TargetCompany"].Should().HaveCount(1).And.Contain("客户A");
        result["Remark"].Should().HaveCount(1).And.Contain("调拨");
        // AppDbContext.SaveChangesAsync 将 CreatedBy 覆盖为 "system"（无 HttpContext 时）
        result["CreatedBy"].Should().AllBe("system");
    }

    [Fact]
    public async Task GetOutboundFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetOutboundFilterContextsAsync();

        result.Should().NotBeNull();
        foreach (var kvp in result)
            kvp.Value.Should().BeEmpty($"字段 {kvp.Key} 应返回空列表");
    }
}
