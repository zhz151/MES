using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 工段委外服务测试：委外发出CRUD、委外回收、批量操作、状态更新
/// </summary>
public class SectionOutsourceServiceTests : TestBase
{
    private SectionOutsourceService CreateService(AppDbContext ctx)
    {
        var mockProductionRecordService = new Mock<IProductionRecordService>();
        return new SectionOutsourceService(ctx,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SectionOutsourceService>.Instance,
            mockProductionRecordService.Object);
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
            ManufacturingItem = "管",
            WorkOrderNo = "WO-001",
            SalesOrderNo = "SO-001",
            ProductionMainNo = "M-001",
            OrderItemIds = "1",
            Salesman = "张三",
            SettlementMethod = "现款现货",
            StandardCode = "GB/T 14976",
            DeliveryState = "冷拔态",
            LengthStatus = "不定尺",
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

    private async Task<SectionOutsource> SeedOutsourceAsync(AppDbContext ctx, int batchId,
        string vendor = "委外厂A", SectionOutsourceStatus status = SectionOutsourceStatus.PendingRecovery)
    {
        var entity = new SectionOutsource
        {
            ProductionBatchId = batchId,
            ProcessName = "冷轧",
            ManufacturingSpec = "219*8",
            SectionName = "冷轧拔",
            SequenceNumber = 1,
            OutsourceVendor = vendor,
            SendOutDate = DateTime.Today,
            SendQuantity = 10,
            SendWeight = 1000m,
            Status = status
        };
        ctx.SectionOutsources.Add(entity);
        await ctx.SaveChangesAsync();
        return entity;
    }

    // ========== GetPagedAsync ==========

    [Fact]
    public async Task GetPagedAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_按关键字搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH001");
        await SeedOutsourceAsync(ctx, batch.Id, vendor: "委外厂A");
        await SeedOutsourceAsync(ctx, batch.Id, vendor: "委外厂B");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "委外厂A" });

        result.Items.Should().HaveCount(1);
        result.Items[0].OutsourceVendor.Should().Be("委外厂A");
    }

    [Fact]
    public async Task GetPagedAsync_关键字无匹配_返回空列表()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedOutsourceAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "NONEXISTENT" });

        result.Items.Should().BeEmpty();
    }

    // ========== GetByIdsAsync ==========

    [Fact]
    public async Task GetByIdsAsync_存在_返回列表()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedOutsourceAsync(ctx, batch.Id);
        var id = await ctx.SectionOutsources.Select(s => s.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdsAsync(new[] { id });

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(id);
    }

    [Fact]
    public async Task GetByIdsAsync_空数组_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdsAsync(Array.Empty<int>());

        result.Should().BeEmpty();
    }

    // ========== CreateAsync ==========

    [Fact]
    public async Task CreateAsync_成功创建()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        // 需要工序组包含"冷轧拔"工段，以便 ResolveSequenceNumber 能解析
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            SequenceNumber = 1,
            ProcessName = "冷轧",
            ManufacturingSpec = "219*8",
            ColdRollDraw = 1
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateSectionOutsourceRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "冷轧",
            ManufacturingSpec = "219*8",
            SectionName = "冷轧拔",
            OutsourceVendor = "委外厂A",
            SendOutDate = DateTime.Today,
            SendQuantity = 10,
            SendWeight = 1000m
        });

        result.Should().NotBeNull();
        result.OutsourceVendor.Should().Be("委外厂A");
        result.SendQuantity.Should().Be(10);
    }

    [Fact]
    public async Task CreateAsync_批次不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateSectionOutsourceRequest
        {
            BatchNo = "NONEXISTENT", ProcessName = "冷轧",
            ManufacturingSpec = "219*8", SectionName = "冷轧拔",
            OutsourceVendor = "委外厂A", SendOutDate = DateTime.Today
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== BatchCreateAsync ==========

    [Fact]
    public async Task BatchCreateAsync_成功批量创建()
    {
        var ctx = CreateDbContext();
        await SeedBatchAsync(ctx, "BATCH001");
        await SeedBatchAsync(ctx, "BATCH002");
        var svc = CreateService(ctx);

        var result = await svc.BatchCreateAsync(new List<CreateSectionOutsourceRequest>
        {
            new() { BatchNo = "BATCH001", ProcessName = "冷轧", ManufacturingSpec = "219*8",
                SectionName = "冷轧拔", OutsourceVendor = "委外厂A", SendOutDate = DateTime.Today },
            new() { BatchNo = "BATCH002", ProcessName = "酸洗", ManufacturingSpec = "219*8",
                SectionName = "酸洗", OutsourceVendor = "委外厂B", SendOutDate = DateTime.Today }
        });

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task BatchCreateAsync_空列表_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.BatchCreateAsync(new List<CreateSectionOutsourceRequest>());

        result.Should().BeEmpty();
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_成功更新()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedOutsourceAsync(ctx, batch.Id);
        var id = await ctx.SectionOutsources.Select(s => s.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateSectionOutsourceRequest
        {
            OutsourceVendor = "新委外厂",
            SendQuantity = 15,
            SendWeight = 1500m
        });

        result.OutsourceVendor.Should().Be("新委外厂");
        result.SendQuantity.Should().Be(15);
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateSectionOutsourceRequest());
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedOutsourceAsync(ctx, batch.Id);
        var id = await ctx.SectionOutsources.Select(s => s.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var deleted = await ctx.SectionOutsources.FindAsync(id);
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

    // ========== 委外回收 ==========

    [Fact]
    public async Task CreateRecoveryAsync_成功创建()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var outsource = await SeedOutsourceAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        var result = await svc.CreateRecoveryAsync(new CreateOutsourceRecoveryRequest
        {
            SectionOutsourceId = outsource.Id,
            RecoveryDate = DateTime.Today,
            RecoveryQuantity = 8,
            RecoveryWeight = 800m
        });

        result.Should().NotBeNull();
        result.RecoveryQuantity.Should().Be(8);
    }

    [Fact]
    public async Task GetRecoveriesAsync_有数据_返回列表()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var outsource = await SeedOutsourceAsync(ctx, batch.Id);
        ctx.OutsourceRecoveries.Add(new OutsourceRecovery
        {
            SectionOutsourceId = outsource.Id,
            RecoveryDate = DateTime.Today,
            RecoveryQuantity = 5,
            RecoveryWeight = 500m
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetRecoveriesAsync(outsource.Id);

        result.Should().HaveCount(1);
        result[0].RecoveryQuantity.Should().Be(5);
    }

    [Fact]
    public async Task UpdateRecoveryAsync_成功更新()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var outsource = await SeedOutsourceAsync(ctx, batch.Id);
        var recovery = new OutsourceRecovery
        {
            SectionOutsourceId = outsource.Id,
            RecoveryDate = DateTime.Today,
            RecoveryQuantity = 5,
            RecoveryWeight = 500m
        };
        ctx.OutsourceRecoveries.Add(recovery);
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateRecoveryAsync(recovery.Id, new UpdateOutsourceRecoveryRequest
        {
            RecoveryQuantity = 8,
            RecoveryWeight = 800m
        });

        result.RecoveryQuantity.Should().Be(8);
        result.RecoveryWeight.Should().Be(800m);
    }

    [Fact]
    public async Task DeleteRecoveryAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var outsource = await SeedOutsourceAsync(ctx, batch.Id);
        var recovery = new OutsourceRecovery
        {
            SectionOutsourceId = outsource.Id,
            RecoveryDate = DateTime.Today,
            RecoveryQuantity = 5
        };
        ctx.OutsourceRecoveries.Add(recovery);
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        await svc.DeleteRecoveryAsync(recovery.Id);

        var deleted = await ctx.OutsourceRecoveries.FindAsync(recovery.Id);
        deleted.Should().BeNull();
    }

    // ========== B11 专项测试 ==========

    [Fact]
    public async Task GetPagedAsync_关键词搜索规格_返回匹配()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-SPEC");
        // 创建带不同规格的委外记录
        ctx.SectionOutsources.Add(new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessName = "冷轧",
            ManufacturingSpec = "273*10",
            PlantGrade = "304",
            OutsourceSpec = "273*10",
            SectionName = "冷轧拔",
            SequenceNumber = 1,
            OutsourceVendor = "委外厂A",
            SendOutDate = DateTime.Today,
            SendQuantity = 10,
            SendWeight = 1000m,
            Status = SectionOutsourceStatus.PendingRecovery
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "273*10" });

        result.Items.Should().HaveCount(1);
        result.Items[0].ManufacturingSpec.Should().Be("273*10");
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索备注_返回匹配()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-REM");
        ctx.SectionOutsources.Add(new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessName = "冷轧",
            ManufacturingSpec = "219*8",
            SectionName = "冷轧拔",
            SequenceNumber = 1,
            OutsourceVendor = "委外厂A",
            SendOutDate = DateTime.Today,
            SendQuantity = 10,
            SendWeight = 1000m,
            Status = SectionOutsourceStatus.PendingRecovery,
            Remark = "工段委外备注"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "工段委外" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Remark.Should().Be("工段委外备注");
    }
}
