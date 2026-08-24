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
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Exceptions;
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
using MES.Core.Models;
using MES.Services.Quality;
using MES.Tests.Tests;
using System.Reflection;


using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Quality;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Tests.Services;

/// <summary>
/// 过程检验服务测试：CRUD、关键字搜索、日期筛选、批量创建
/// </summary>
public class ProcessInspectionServiceTests : TestBase
{
    /// <summary>
    /// 重置 ProcessInspectionService 的静态筛选上下文缓存（InMemory 测试隔离）
    /// </summary>
    private static void ResetFilterContextCache()
    {
        var cacheField = typeof(ProcessInspectionService).GetField("_filterContextCache",
            BindingFlags.Static | BindingFlags.NonPublic);
        cacheField?.SetValue(null, null);
        var expiryField = typeof(ProcessInspectionService).GetField("_filterContextCacheExpiry",
            BindingFlags.Static | BindingFlags.NonPublic);
        expiryField?.SetValue(null, DateTime.MinValue);
    }

    private ProcessInspectionService CreateService(AppDbContext ctx)
    {
        var mockProductionRecordService = new Mock<IProductionRecordService>();
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        return new ProcessInspectionService(ctx,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProcessInspectionService>.Instance,
            mockProductionRecordService.Object,
            configMock.Object,
            new MemoryCache(new MemoryCacheOptions()),
            CreateProcessDefinitionServiceMock());
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

    private async Task SeedInspectionAsync(AppDbContext ctx, string batchNo = "BATCH001",
        string processName = "60冷轧", string sectionName = SectionKeys.ColdRollDraw)
    {
        var batch = await ctx.ProductionBatches.FirstOrDefaultAsync(b => b.BatchNo == batchNo);
        if (batch == null) batch = await SeedBatchAsync(ctx, batchNo);

        ctx.ProcessInspections.Add(new ProcessInspection
        {
            ProductionBatchId = batch.Id,
            BatchNo = batchNo,
            ProcessName = processName,
            ManufacturingSpec = "219*8",
            SectionName = sectionName,
            SequenceNumber = 1,
            InspectionDate = DateTime.Today,
            Quantity = 10,
            Weight = 1000m
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
        await SeedInspectionAsync(ctx, batchNo: "BATCH001");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            InspectionDateFrom = DateTime.Today.AddDays(-1),
            InspectionDateTo = DateTime.Today.AddDays(1)
        });

        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllAsync_WorkOrderNo投影搜索筛选排序()
    {
        var ctx = CreateDbContext();
        var batch1 = await SeedBatchAsync(ctx, batchNo: "B001");
        batch1.WorkOrderNo = "WO-AAA";
        var batch2 = await SeedBatchAsync(ctx, batchNo: "B002");
        batch2.WorkOrderNo = "WO-BBB";
        await ctx.SaveChangesAsync();
        await SeedInspectionAsync(ctx, batchNo: "B001");
        await SeedInspectionAsync(ctx, batchNo: "B002");
        var svc = CreateService(ctx);

        // 投影：列表返回 DTO 的 WorkOrderNo 取自批次导航属性
        var all = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20 });
        all.Items.Select(i => i.WorkOrderNo).Should().BeEquivalentTo(new[] { "WO-AAA", "WO-BBB" });

        // 关键字搜索命中工单号
        var kw = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "WO-AAA" });
        kw.Items.Should().ContainSingle(i => i.BatchNo == "B001");

        // WorkOrderNo 列筛选
        var filtered = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "WorkOrderNo", Operator = "in", Values = new List<string> { "WO-BBB" } }
            }
        });
        filtered.Items.Should().ContainSingle(i => i.BatchNo == "B002");

        // 升序/降序排序
        var asc = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, SortBy = "workorderno", IsDescending = false });
        asc.Items.First().WorkOrderNo.Should().Be("WO-AAA");
        var desc = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, SortBy = "workorderno", IsDescending = true });
        desc.Items.First().WorkOrderNo.Should().Be("WO-BBB");
    }

    // ========== BatchCreateAsync ==========

    [Fact]
    public async Task BatchCreateAsync_成功创建()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var pg = await SeedProcessGroupAsync(ctx, batch.Id);
        // 冷轧/冷拔前置校验：先创建一条冷轧拔生产记录
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            SectionName = SectionKeys.ColdRollDraw,
            ProductStatus = ProductStatuses.Finished
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.BatchCreateAsync(new List<CreateProcessInspectionRequest>
        {
            new()
            {
                BatchNo = "BATCH001",
                ProcessName = "60冷轧",
                ManufacturingSpec = "219*8",
                SectionName = SectionKeys.Inspection,
                InspectionDate = DateTime.Today,
                Quantity = 10,
                QualifiedQuantity = 10,
                Weight = 1000m
            }
        });

        result.Should().HaveCount(1);
        result[0].Quantity.Should().Be(10);
    }

    [Fact]
    public async Task BatchCreateAsync_空列表_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.BatchCreateAsync(new List<CreateProcessInspectionRequest>());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task BatchCreateAsync_批次不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.BatchCreateAsync(new List<CreateProcessInspectionRequest>
        {
            new()
            {
                BatchNo = "NONEXISTENT", ProcessName = "60冷轧",
                ManufacturingSpec = "219*8", SectionName = SectionKeys.ColdRollDraw,
                InspectionDate = DateTime.Today
            }
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task BatchCreateAsync_同工序组不同检验项目_可分别创建()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var pg = await SeedProcessGroupAsync(ctx, batch.Id);
        // 冷轧/冷拔前置校验：先创建一条冷轧拔生产记录
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            SectionName = SectionKeys.ColdRollDraw,
            ProductStatus = ProductStatuses.Finished
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.BatchCreateAsync(new List<CreateProcessInspectionRequest>
        {
            new()
            {
                BatchNo = "BATCH001",
                ProcessName = "60冷轧",
                ManufacturingSpec = "219*8",
                SectionName = SectionKeys.Inspection,
                InspectionDate = DateTime.Today,
                InspectionItem = InspectionItem.Dimension,
                Quantity = 10,
                QualifiedQuantity = 10,
                Weight = 1000m
            },
            new()
            {
                BatchNo = "BATCH001",
                ProcessName = "60冷轧",
                ManufacturingSpec = "219*8",
                SectionName = SectionKeys.Inspection,
                InspectionDate = DateTime.Today,
                InspectionItem = InspectionItem.VisualInspection,
                Quantity = 10,
                QualifiedQuantity = 10,
                Weight = 1000m
            }
        });

        result.Should().HaveCount(2);
        result.Select(r => r.InspectionItem).Should().Contain(InspectionItem.Dimension);
        result.Select(r => r.InspectionItem).Should().Contain(InspectionItem.VisualInspection);
    }

    [Fact]
    public async Task BatchCreateAsync_同工序组同检验项目_判重复_抛出()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var pg = await SeedProcessGroupAsync(ctx, batch.Id);
        // 冷轧/冷拔前置校验：先创建一条冷轧拔生产记录
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "60冷轧",
            SectionName = SectionKeys.ColdRollDraw,
            ProductStatus = ProductStatuses.Finished
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        // 先落库一条「尺寸」过程检验
        var first = await svc.BatchCreateAsync(new List<CreateProcessInspectionRequest>
        {
            new()
            {
                BatchNo = "BATCH001",
                ProcessName = "60冷轧",
                ManufacturingSpec = "219*8",
                SectionName = SectionKeys.Inspection,
                InspectionDate = DateTime.Today,
                InspectionItem = InspectionItem.Dimension,
                Quantity = 10,
                QualifiedQuantity = 10,
                Weight = 1000m
            }
        });
        first.Should().HaveCount(1);

        // 同工序组同检验项目再次提交 → 判重
        var actDup = () => svc.BatchCreateAsync(new List<CreateProcessInspectionRequest>
        {
            new()
            {
                BatchNo = "BATCH001",
                ProcessName = "60冷轧",
                ManufacturingSpec = "219*8",
                SectionName = SectionKeys.Inspection,
                InspectionDate = DateTime.Today,
                InspectionItem = InspectionItem.Dimension,
                Quantity = 10,
                QualifiedQuantity = 10,
                Weight = 1000m
            }
        });
        await actDup.Should().ThrowAsync<BusinessException>().WithMessage("*已存在*");

        // 同工序组换检验项目 → 可创建
        var second = await svc.BatchCreateAsync(new List<CreateProcessInspectionRequest>
        {
            new()
            {
                BatchNo = "BATCH001",
                ProcessName = "60冷轧",
                ManufacturingSpec = "219*8",
                SectionName = SectionKeys.Inspection,
                InspectionDate = DateTime.Today,
                InspectionItem = InspectionItem.VisualInspection,
                Quantity = 10,
                QualifiedQuantity = 10,
                Weight = 1000m
            }
        });
        second.Should().HaveCount(1);
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_成功更新()
    {
        var ctx = CreateDbContext();
        await SeedInspectionAsync(ctx);
        var id = await ctx.ProcessInspections.Select(p => p.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateProcessInspectionRequest
        {
            InspectionDate = DateTime.Today,
            Quantity = 14,
            QualifiedQuantity = 14
        });

        result.Quantity.Should().Be(14);
        result.QualifiedQuantity.Should().Be(14);
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateProcessInspectionRequest { InspectionDate = DateTime.Today });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        await SeedInspectionAsync(ctx);
        var id = await ctx.ProcessInspections.Select(p => p.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var deleted = await ctx.ProcessInspections.FindAsync(id);
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

    // ========== B11 专项测试 ==========

    [Fact]
    public async Task GetAllAsync_关键词搜索规格_返回匹配()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH001");
        ctx.ProcessInspections.Add(new ProcessInspection
        {
            ProductionBatchId = batch.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "273*10",
            SectionName = SectionKeys.ColdRollDraw,
            SequenceNumber = 1,
            InspectionDate = DateTime.Today,
            Quantity = 10,
            Weight = 1000m
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "273" });

        result.Items.Should().HaveCount(1);
        result.Items[0].ManufacturingSpec.Should().Be("273*10");
    }

    [Fact]
    public async Task GetAllAsync_关键词搜索备注_返回匹配()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH001");
        ctx.ProcessInspections.Add(new ProcessInspection
        {
            ProductionBatchId = batch.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            SequenceNumber = 1,
            InspectionDate = DateTime.Today,
            Quantity = 10,
            Weight = 1000m,
            Remark = "过程备注测试"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "过程备注" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Remark.Should().Be("过程备注测试");
    }

    // ========== 筛选测试（FilterDescriptor） ==========

    [Fact]
    public async Task GetAllAsync_Filters_BatchNoIn_返回匹配()
    {
        var ctx = CreateDbContext();
        await SeedInspectionAsync(ctx, batchNo: "BATCH001");
        await SeedInspectionAsync(ctx, batchNo: "BATCH002");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "BatchNo", Operator = "in", Values = new List<string> { "BATCH001" } }
            }
        });

        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllAsync_Filters_ProcessNameContains_返回匹配()
    {
        var ctx = CreateDbContext();
        await SeedInspectionAsync(ctx, batchNo: "BATCH001", processName: "60冷轧");
        await SeedInspectionAsync(ctx, batchNo: "BATCH002", processName: "冷拔");
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "ProcessName", Operator = "contains", Value = "冷轧" }
            }
        });

        result.Items.Should().HaveCount(1);
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
                new() { Field = "ProcessName", Operator = "contains", Value = "NONEXISTENT" }
            }
        });

        result.Items.Should().BeEmpty();
    }

    // ========== GetFilterContextsAsync ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        ResetFilterContextCache();
        var ctx = CreateDbContext();
        await SeedInspectionAsync(ctx, batchNo: "BATCH001", processName: "60冷轧", sectionName: SectionKeys.ColdRollDraw);
        await SeedInspectionAsync(ctx, batchNo: "BATCH002", processName: "冷拔", sectionName: SectionKeys.ColdRollDraw);
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("BatchNo");
        contexts["BatchNo"].Should().BeEquivalentTo(new[] { "BATCH001", "BATCH002" }, opts => opts.WithStrictOrdering());
        contexts.Should().ContainKey("ProcessName");
        contexts["ProcessName"].Should().BeEquivalentTo(new[] { "60冷轧", "冷拔" }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空列表()
    {
        ResetFilterContextCache();
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
        ResetFilterContextCache();
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH001");
        ctx.ProcessInspections.Add(new ProcessInspection
        {
            ProductionBatchId = batch.Id,
            ProcessName = "60冷轧",
            SectionName = SectionKeys.ColdRollDraw,
            BatchNo = "BATCH001",
            SequenceNumber = 1,
            InspectionDate = DateTime.Today,
            Quantity = 10,
            EquipmentName = null,
            Inspector = null,
            Remark = null
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["BatchNo"].Should().HaveCount(1);
        contexts["EquipmentName"].Should().BeEmpty();
        contexts["Remark"].Should().BeEmpty();
    }
}
