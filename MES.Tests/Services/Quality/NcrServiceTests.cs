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
using MES.Services.Quality;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// NCR 不合格品报告服务测试：CRUD、状态变更、批次调取、筛选上下文、待处理卡片
/// </summary>
public class NcrServiceTests : TestBase
{
    private NcrService CreateService(AppDbContext ctx)
    {
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        return new(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<NcrService>.Instance, configMock.Object);
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
            SettlementMethod = "过磅",
            StandardCode = "GB/T 14976",
            DeliveryState = "固溶酸洗",
            LengthStatus = "定尺",
            TechnicalRequirements = "NORMAL",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1)
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    private async Task<Ncr> SeedNcrAsync(AppDbContext ctx, NcrStatus status = NcrStatus.Processing)
    {
        var ncr = new Ncr
        {
            ReportDate = DateTime.Today,
            BatchNo = "BATCH001",
            PipeCategory = PipeCategory.OrderFinished,
            DefectiveQuantity = 10,
            Status = status
        };
        ctx.Ncrs.Add(ncr);
        await ctx.SaveChangesAsync();
        return ncr;
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
        await SeedNcrAsync(ctx);
        await SeedBatchAsync(ctx, "BATCH002");
        ctx.Ncrs.Add(new Ncr { ReportDate = DateTime.Today, BatchNo = "BATCH002", PipeCategory = PipeCategory.OrderFinished, Status = NcrStatus.Processing });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "BATCH001" });

        result.Items.Should().HaveCount(1);
        result.Items[0].BatchNo.Should().Be("BATCH001");
    }

    [Fact]
    public async Task GetAllAsync_关键字无匹配_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedNcrAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetAllAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "NONEXISTENT" });

        result.Items.Should().BeEmpty();
    }

    // ========== GetAllListAsync ==========

    [Fact]
    public async Task GetAllListAsync_返回全部数据()
    {
        var ctx = CreateDbContext();
        await SeedNcrAsync(ctx);
        ctx.Ncrs.Add(new Ncr { ReportDate = DateTime.Today, BatchNo = "BATCH002", PipeCategory = PipeCategory.OrderFinished, Status = NcrStatus.Processing });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetAllListAsync();

        result.Should().HaveCount(2);
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        var ncr = await SeedNcrAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(ncr.Id);

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

        var result = await svc.CreateAsync(new CreateNcrRequest
        {
            ReportDate = DateTime.Today,
            BatchNo = "BATCH001",
            PipeCategory = PipeCategory.OrderFinished,
            DefectiveQuantity = 5,
            ProblemDescription = "表面裂纹"
        });

        result.Should().NotBeNull();
        result.BatchNo.Should().Be("BATCH001");
        result.Status.Should().Be(NcrStatus.Processing);

        var saved = await ctx.Ncrs.FirstAsync();
        saved.BatchNo.Should().Be("BATCH001");
    }

    [Fact]
    public async Task CreateAsync_自动填充批次冗余字段()
    {
        var ctx = CreateDbContext();
        await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateNcrRequest
        {
            ReportDate = DateTime.Today,
            BatchNo = "BATCH001",
            PipeCategory = PipeCategory.OrderFinished
        });

        result.WorkOrderNo.Should().Be("WO-001");
        result.PlantGrade.Should().Be("304");
        result.Specification.Should().Be("219*8");
    }

    [Fact]
    public async Task CreateAsync_三条件满足_自动关闭()
    {
        var ctx = CreateDbContext();
        await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateNcrRequest
        {
            ReportDate = DateTime.Today,
            BatchNo = "BATCH001",
            PipeCategory = PipeCategory.OrderFinished,
            DisposalIsCompleted = true,
            PersonIsCompleted = true,
            VerifyResult = VerifyResult.Passed
        });

        result.Status.Should().Be(NcrStatus.Closed);
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_成功更新()
    {
        var ctx = CreateDbContext();
        var ncr = await SeedNcrAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(ncr.Id, new UpdateNcrRequest
        {
            ReportDate = DateTime.Today,
            DefectiveQuantity = 20,
            ProblemDescription = "更新描述"
        });

        result.DefectiveQuantity.Should().Be(20);
        result.ProblemDescription.Should().Be("更新描述");
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateNcrRequest { ReportDate = DateTime.Today });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task UpdateAsync_条件满足_自动关闭()
    {
        var ctx = CreateDbContext();
        var ncr = await SeedNcrAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(ncr.Id, new UpdateNcrRequest
        {
            ReportDate = DateTime.Today,
            DisposalIsCompleted = true,
            PersonIsCompleted = true,
            VerifyResult = VerifyResult.Passed
        });

        result.Status.Should().Be(NcrStatus.Closed);
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var ncr = await SeedNcrAsync(ctx);
        var svc = CreateService(ctx);

        await svc.DeleteAsync(ncr.Id);

        var deleted = await ctx.Ncrs.FindAsync(ncr.Id);
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

    // ========== UpdateStatusAsync ==========

    [Fact]
    public async Task UpdateStatusAsync_成功变更()
    {
        var ctx = CreateDbContext();
        var ncr = await SeedNcrAsync(ctx);
        // 设置关闭必要条件
        ncr.DisposalIsCompleted = true;
        ncr.PersonIsCompleted = true;
        ncr.VerifyResult = VerifyResult.Passed;
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateStatusAsync(ncr.Id, new UpdateNcrStatusRequest
        {
            Status = NcrStatus.Closed
        });

        result.Status.Should().Be(NcrStatus.Closed);
    }

    [Fact]
    public async Task UpdateStatusAsync_关闭时检查必要条件()
    {
        var ctx = CreateDbContext();
        var ncr = await SeedNcrAsync(ctx);
        var svc = CreateService(ctx);

        var act = () => svc.UpdateStatusAsync(ncr.Id, new UpdateNcrStatusRequest
        {
            Status = NcrStatus.Closed
        });

        // 默认 DisposalIsCompleted/PersonIsCompleted 为 false，VerifyResult 为 null → 不能关闭
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*处置未完结*");
    }

    [Fact]
    public async Task UpdateStatusAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateStatusAsync(999, new UpdateNcrStatusRequest { Status = NcrStatus.Closed });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== LookupBatchAsync ==========

    [Fact]
    public async Task LookupBatchAsync_存在_返回批次信息()
    {
        var ctx = CreateDbContext();
        await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.LookupBatchAsync("BATCH001");

        result.Should().NotBeNull();
        result!.WorkOrderNo.Should().Be("WO-001");
        result.PlantGrade.Should().Be("304");
    }

    [Fact]
    public async Task LookupBatchAsync_不存在_返回Null()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.LookupBatchAsync("NONEXISTENT");

        result.Should().BeNull();
    }

    // ========== GetFilterContextsAsync ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        ctx.Ncrs.Add(new Ncr
        {
            ReportDate = DateTime.Today, BatchNo = "BATCH001",
            PipeCategory = PipeCategory.OrderFinished, Status = NcrStatus.Processing,
            ReportDepartment = "质检部", PlantGrade = "304"
        });
        ctx.Ncrs.Add(new Ncr
        {
            ReportDate = DateTime.Today, BatchNo = "BATCH002",
            PipeCategory = PipeCategory.Intermediate, Status = NcrStatus.Closed,
            ReportDepartment = "生产部", PlantGrade = "316L"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("pipecategory");
        contexts["reportdepartment"].Should().BeEquivalentTo(new[] { "质检部", "生产部" }, opts => opts.WithStrictOrdering());
        contexts["plantgrade"].Should().BeEquivalentTo(new[] { "304", "316L" }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["pipecategory"].Should().BeEmpty();
        contexts["status"].Should().BeEmpty();
    }

    // ========== GetPendingChecksAsync ==========

    [Fact]
    public async Task GetPendingChecksAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingChecksAsync_过程检验触发_返回卡片()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        ctx.ProcessInspections.Add(new ProcessInspection
        {
            ProductionBatchId = batch.Id,
            BatchNo = "BATCH001",
            ProcessName = "冷拔",
            ManufacturingSpec = "219*8",
            SectionName = "冷轧拔",
            InspectionItem = InspectionItem.Dimension.ToString(),
            InspectionDate = DateTime.Today,
            Quantity = 100,
            DefectReworkQuantity = 10,
            Inspector = "张三"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        result.Should().NotBeEmpty();
        result.Should().Contain(r => r.DisposalMethod == DisposalMethod.Rework);
        result[0].BatchNo.Should().Be("BATCH001");
    }

    [Fact]
    public async Task GetPendingChecksAsync_排除已有NCR记录()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        ctx.ProcessInspections.Add(new ProcessInspection
        {
            ProductionBatchId = batch.Id,
            BatchNo = "BATCH001",
            ProcessName = "冷拔",
            ManufacturingSpec = "219*8",
            SectionName = "冷轧拔",
            InspectionItem = InspectionItem.Dimension.ToString(),
            InspectionDate = DateTime.Today,
            Quantity = 100,
            DefectReworkQuantity = 10,
            Inspector = "张三"
        });
        ctx.Ncrs.Add(new Ncr
        {
            ReportDate = DateTime.Today, BatchNo = "BATCH001",
            PipeCategory = PipeCategory.OrderFinished,
            DisposalMethod = DisposalMethod.Rework,
            SourceInspectionItem = InspectionItem.Dimension.ToString(),
            Status = NcrStatus.Processing
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        result.Should().BeEmpty();
    }
}
