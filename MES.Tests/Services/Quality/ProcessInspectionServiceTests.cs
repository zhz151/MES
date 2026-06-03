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
/// 过程检验服务测试：CRUD、关键字搜索、日期筛选、批量创建
/// </summary>
public class ProcessInspectionServiceTests : TestBase
{
    private ProcessInspectionService CreateService(AppDbContext ctx)
    {
        var mockProductionRecordService = new Mock<IProductionRecordService>();
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        return new ProcessInspectionService(ctx,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProcessInspectionService>.Instance,
            mockProductionRecordService.Object,
            configMock.Object);
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
        string processName = "60冷轧", string sectionName = "冷轧拔")
    {
        var batch = await ctx.ProductionBatches.FirstOrDefaultAsync(b => b.BatchNo == batchNo);
        if (batch == null) batch = await SeedBatchAsync(ctx, batchNo);

        ctx.ProcessInspections.Add(new ProcessInspection
        {
            ProductionBatchId = batch.Id,
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
            PageIndex = 1, PageSize = 20,
            InspectionDateFrom = DateTime.Today.AddDays(-1),
            InspectionDateTo = DateTime.Today.AddDays(1)
        });

        result.Items.Should().HaveCount(1);
    }

    // ========== BatchCreateAsync ==========

    [Fact]
    public async Task BatchCreateAsync_成功创建()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        var result = await svc.BatchCreateAsync(new List<CreateProcessInspectionRequest>
        {
            new()
            {
                BatchNo = "BATCH001",
                ProcessName = "60冷轧",
                ManufacturingSpec = "219*8",
                SectionName = "冷轧拔",
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
                ManufacturingSpec = "219*8", SectionName = "冷轧拔",
                InspectionDate = DateTime.Today
            }
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
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
            SectionName = "冷轧拔",
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
            SectionName = "冷轧拔",
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
            PageIndex = 1, PageSize = 20,
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
            PageIndex = 1, PageSize = 20,
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
            PageIndex = 1, PageSize = 20,
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
        await SeedInspectionAsync(ctx, batchNo: "BATCH001", processName: "60冷轧", sectionName: "冷轧拔");
        await SeedInspectionAsync(ctx, batchNo: "BATCH002", processName: "冷拔", sectionName: "冷轧拔");
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
        ctx.ProcessInspections.Add(new ProcessInspection
        {
            ProductionBatchId = batch.Id, ProcessName = "60冷轧", SectionName = "冷轧拔",
            SequenceNumber = 1, InspectionDate = DateTime.Today, Quantity = 10,
            EquipmentName = null, Inspector = null, Remark = null
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["BatchNo"].Should().HaveCount(1);
        contexts["EquipmentName"].Should().BeEmpty();
        contexts["Remark"].Should().BeEmpty();
    }
}
