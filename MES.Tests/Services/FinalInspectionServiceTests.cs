using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 成品检验服务测试：CRUD、关键字搜索、日期筛选、批次调取、批量创建
/// </summary>
public class FinalInspectionServiceTests : TestBase
{
    private FinalInspectionService CreateService(AppDbContext ctx)
        => new(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<FinalInspectionService>.Instance);

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
            Weight = 1000m,
            QualifiedQuantity = 9,
            QualifiedWeight = 950m
        };
        ctx.FinalInspections.Add(entity);
        await ctx.SaveChangesAsync();
        return entity;
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
            PageIndex = 1, PageSize = 20,
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
        await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateFinalInspectionRequest
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            Quantity = 20,
            Weight = 2000m,
            QualifiedQuantity = 18,
            QualifiedWeight = 1800m
        });

        result.Should().NotBeNull();
        result.BatchNo.Should().Be("BATCH001");
        result.Quantity.Should().Be(20);

        var saved = await ctx.FinalInspections.FirstAsync();
        saved.Quantity.Should().Be(20);
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
            QualifiedQuantity = 14
        });

        result.Quantity.Should().Be(15);
        result.QualifiedQuantity.Should().Be(14);
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateFinalInspectionRequest { InspectionDate = DateTime.Today });
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

    // ========== LookupBatchAsync ==========

    [Fact]
    public async Task LookupBatchAsync_存在_返回批次信息()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.LookupBatchAsync("BATCH001");

        result.Should().NotBeNull();
        result!.ProductionBatchId.Should().Be(batch.Id);
        result.MaterialName.Should().Be("不锈钢管");
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
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension,
            InspectionDate = DateTime.Today,
            BatchNo = "BATCH001",
            ProductionBatchId = batch.Id,
            Quantity = 10,
            Weight = 1000m,
            FurnaceNo = "FUR-001"
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
            Weight = 1000m,
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
            InspectionItem = InspectionItem.Dimension, InspectionDate = DateTime.Today,
            BatchNo = "BATCH001", ProductionBatchId = batch1.Id, Quantity = 10
        });
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension, InspectionDate = DateTime.Today,
            BatchNo = "BATCH002", ProductionBatchId = batch2.Id, Quantity = 20
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1, PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "BatchNo", Operator = "contains", Value = "BATCH001" }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].BatchNo.Should().Be("BATCH001");
    }

    [Fact]
    public async Task GetAllAsync_Filters_MaterialNameIn_返回匹配()
    {
        var ctx = CreateDbContext();
        var batch1 = await SeedBatchAsync(ctx, "B001");
        var batch2 = await SeedBatchAsync(ctx, "B002");
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension, InspectionDate = DateTime.Today,
            BatchNo = "B001", ProductionBatchId = batch1.Id, Quantity = 10, MaterialName = "不锈钢"
        });
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension, InspectionDate = DateTime.Today,
            BatchNo = "B002", ProductionBatchId = batch2.Id, Quantity = 20, MaterialName = "碳钢"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1, PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "MaterialName", Operator = "in", Values = new List<string> { "不锈钢" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].MaterialName.Should().Be("不锈钢");
    }

    [Fact]
    public async Task GetAllAsync_Filters_NoMatch_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedInspectionAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1, PageSize = 20,
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
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension, InspectionDate = DateTime.Today,
            BatchNo = "BATCH001", ProductionBatchId = batch1.Id, Quantity = 10, PlantGrade = "304"
        });
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension, InspectionDate = DateTime.Today,
            BatchNo = "BATCH002", ProductionBatchId = batch2.Id, Quantity = 20, PlantGrade = "316L"
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
        contexts["MaterialName"].Should().BeEmpty();
    }

    [Fact]
    public async Task GetFilterContextsAsync_Nullable字段排除null()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Dimension, InspectionDate = DateTime.Today,
            BatchNo = "BATCH001", ProductionBatchId = batch.Id, Quantity = 10,
            MaterialName = null, TagNo = null
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["BatchNo"].Should().HaveCount(1);
        contexts["MaterialName"].Should().BeEmpty();
        contexts["TagNo"].Should().BeEmpty();
    }
}
