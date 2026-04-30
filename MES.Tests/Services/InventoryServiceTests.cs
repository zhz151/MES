using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Services;
using MES.Tests.Tests;
using MES.Data;
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
        return new InventoryService(ctx, httpMock.Object);
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
        result.BatchNo.Should().StartWith("STK");
        result.RemainingQuantity.Should().Be(10);
        result.RemainingWeight.Should().Be(1000m);
        result.WarehouseName.Should().Be("测试仓库");
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
            OutboundType = "销售",
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
            OutboundType = "销售",
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
            OutboundType = "销售",
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
            OutboundType = "销售",
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
            OutboundType = "销售",
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
            OutboundWeight = 500m, OutboundType = "销售",
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
    public async Task HardDeleteInventoryBatchAsync_删除批次和关联出库记录()
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
            InventoryBatchId = batch.Id, OutboundQuantity = 3,
            OutboundWeight = 300m, OutboundType = "销售",
            TargetCompany = "客户X", OutboundDate = DateTime.Today
        });

        await svc.HardDeleteInventoryBatchAsync(batch.Id);

        // 批次和出库记录都应被物理删除
        var getAct = () => svc.GetByIdAsync(batch.Id);
        await getAct.Should().ThrowAsync<BusinessException>().WithMessage("批次不存在");

        var records = await svc.GetOutboundRecordsAsync(new OutboundQueryParams
        {
            InventoryBatchId = batch.Id,
            PageIndex = 0,
            PageSize = 10
        });
        records.Items.Should().BeEmpty();
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
            OutboundWeight = 200m, OutboundType = "销售",
            TargetCompany = "客户A", OutboundDate = DateTime.Today
        });
        await svc.OutboundAsync(new CreateOutboundRequest
        {
            InventoryBatchId = batch.Id, OutboundQuantity = 3,
            OutboundWeight = 300m, OutboundType = "调拨",
            TargetCompany = "客户B", OutboundDate = DateTime.Today
        });

        var result = await svc.GetOutboundRecordsAsync(new OutboundQueryParams
        {
            OutboundType = "销售",
            PageIndex = 0,
            PageSize = 10
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].OutboundType.Should().Be("销售");
        result.Items[0].BatchNo.Should().Be(batch.BatchNo);
        result.TotalCount.Should().Be(1);
    }
}
