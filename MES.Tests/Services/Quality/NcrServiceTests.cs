using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MES.Core.Constants;
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


using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Quality;
using Microsoft.Extensions.Caching.Memory;

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
        return new(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<NcrService>.Instance, configMock.Object, new MemoryCache(new MemoryCacheOptions()));
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
            DeliveryState = "SolutionAnnealedAndPickled",
            LengthStatus = "Fixed",
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
            PipeCategory = MaterialType.OrderFinished,
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
        ctx.Ncrs.Add(new Ncr { ReportDate = DateTime.Today, BatchNo = "BATCH002", PipeCategory = MaterialType.OrderFinished, Status = NcrStatus.Processing });
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
        ctx.Ncrs.Add(new Ncr { ReportDate = DateTime.Today, BatchNo = "BATCH002", PipeCategory = MaterialType.OrderFinished, Status = NcrStatus.Processing });
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
            PipeCategory = MaterialType.OrderFinished,
            DefectiveQuantity = 5,
            DefectiveWeight = 30,
            ProblemDescription = "表面裂纹"
        });

        result.Should().NotBeNull();
        result.BatchNo.Should().Be("BATCH001");
        result.DefectiveQuantity.Should().Be(5);
        result.DefectiveWeight.Should().Be(30);
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
            PipeCategory = MaterialType.OrderFinished
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
            PipeCategory = MaterialType.OrderFinished,
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
            DefectiveWeight = 40,
            ProblemDescription = "更新描述"
        });

        result.DefectiveQuantity.Should().Be(20);
        result.DefectiveWeight.Should().Be(40);
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

    [Fact]
    public async Task LookupBatchAsync_批次含检验记录_返回次品支数重量合计()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        // 过程检验：返整 10 支理论重 20kg + 报废 3 支理论重 6kg
        ctx.ProcessInspections.Add(new ProcessInspection
        {
            ProductionBatchId = batch.Id,
            BatchNo = "BATCH001",
            ProcessName = "冷拔",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            InspectionItem = InspectionItem.Dimension.ToString(),
            InspectionDate = DateTime.Today,
            Quantity = 100,
            DefectReworkQuantity = 10,
            TheoreticalReworkWeight = 20,
            DefectScrapQuantity = 3,
            TheoreticalScrapWeight = 6,
            Inspector = "张三"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.LookupBatchAsync("BATCH001");

        result.Should().NotBeNull();
        result!.DefectiveQuantity.Should().Be(13);
        result.DefectiveWeight.Should().Be(26);
    }

    // ========== GetFilterContextsAsync ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        ctx.Ncrs.Add(new Ncr
        {
            ReportDate = DateTime.Today,
            BatchNo = "BATCH001",
            PipeCategory = MaterialType.OrderFinished,
            Status = NcrStatus.Processing,
            ReportDepartment = "质检部",
            PlantGrade = "304"
        });
        ctx.Ncrs.Add(new Ncr
        {
            ReportDate = DateTime.Today,
            BatchNo = "BATCH002",
            PipeCategory = MaterialType.WorkInProgress,
            Status = NcrStatus.Closed,
            ReportDepartment = "生产部",
            PlantGrade = "316L"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["ReportDepartment"].Should().BeEquivalentTo(new[] { "生产部", "质检部" }, opts => opts.WithStrictOrdering());
        contexts["PlantGrade"].Should().BeEquivalentTo(new[] { "304", "316L" }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["ReportDepartment"].Should().BeEmpty();
    }

    // ========== GetPendingChecksAsync ==========

    [Fact]
    public async Task GetPendingChecksAsync_过程检验触发_卡片含次品重量()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        ctx.ProcessInspections.Add(new ProcessInspection
        {
            ProductionBatchId = batch.Id,
            BatchNo = "BATCH001",
            ProcessName = "冷拔",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            InspectionItem = InspectionItem.Dimension.ToString(),
            InspectionDate = DateTime.Today,
            Quantity = 100,
            DefectReworkQuantity = 10,
            TheoreticalReworkWeight = 25,
            Inspector = "张三"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        result.Should().Contain(r => r.DisposalMethod == DisposalMethod.Rework
            && r.DefectQuantity == 10 && r.DefectiveWeight == 25);
    }

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
            SectionName = SectionKeys.ColdRollDraw,
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
            SectionName = SectionKeys.ColdRollDraw,
            InspectionItem = InspectionItem.Dimension.ToString(),
            InspectionDate = DateTime.Today,
            Quantity = 100,
            DefectReworkQuantity = 10,
            Inspector = "张三"
        });
        ctx.Ncrs.Add(new Ncr
        {
            ReportDate = DateTime.Today,
            BatchNo = "BATCH001",
            PipeCategory = MaterialType.OrderFinished,
            DisposalMethod = DisposalMethod.Rework,
            SourceInspectionItem = InspectionItem.Dimension.ToString(),
            Status = NcrStatus.Processing
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        result.Should().BeEmpty();
    }

    // ========== GetMonthlySummaryAsync ==========

    [Fact]
    public async Task GetMonthlySummaryAsync_按反馈日期分月_三级分组聚合()
    {
        var ctx = CreateDbContext();
        var year = DateTime.Today.Year;
        ctx.Ncrs.AddRange(
            new Ncr
            {
                ReportDate = new DateTime(year, 1, 10), BatchNo = "B1", PipeCategory = MaterialType.OrderFinished,
                ResponsibilityCategory = NcrResponsibilityKeys.ProductionInternal, ResponsibleDept = "生产一部",
                DisposalMethod = DisposalMethod.Rework, DefectiveQuantity = 10, DefectiveWeight = 50, Status = NcrStatus.Processing
            },
            new Ncr
            {
                ReportDate = new DateTime(year, 1, 20), BatchNo = "B2", PipeCategory = MaterialType.OrderFinished,
                ResponsibilityCategory = NcrResponsibilityKeys.ProductionInternal, ResponsibleDept = "生产一部",
                DisposalMethod = DisposalMethod.WarehouseEntry, DefectiveQuantity = 5, DefectiveWeight = 30, Status = NcrStatus.Processing
            },
            new Ncr
            {
                ReportDate = new DateTime(year, 2, 5), BatchNo = "B3", PipeCategory = MaterialType.OrderFinished,
                ResponsibilityCategory = NcrResponsibilityKeys.ProductionInternal, ResponsibleDept = "生产一部",
                DisposalMethod = DisposalMethod.Rework, DefectiveQuantity = 3, DefectiveWeight = 20, Status = NcrStatus.Processing
            });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetMonthlySummaryAsync();

        result.MonthLabels.Should().HaveCount(12);
        result.MonthLabels[0].Should().Be($"{year}-01");
        result.CurrentMonthIndex.Should().Be(DateTime.Today.Month - 1);
        // 同 类别×部门 两种处置方式 → 2 行，责任类别/部门正确归一
        result.Rows.Should().HaveCount(2);
        result.Rows.Should().OnlyContain(r => r.CategoryDisplay == "生产-厂内");
        result.Rows.Should().OnlyContain(r => r.ResponsibleDept == "生产一部");
        result.Rows.Should().OnlyContain(r => !string.IsNullOrEmpty(r.DisposalMethodDisplay));
        // 返整行：1月=10支/50kg，2月=3支/20kg，合计 13支/70kg
        var rework = result.Rows.Single(r => r.DisposalMethod == DisposalMethod.Rework);
        rework.Months.Should().HaveCount(12);
        rework.Months[0].Quantity.Should().Be(10);
        rework.Months[0].Weight.Should().Be(50);
        rework.Months[1].Quantity.Should().Be(3);
        rework.Months[1].Weight.Should().Be(20);
        rework.TotalQuantity.Should().Be(13);
        rework.TotalWeight.Should().Be(70);
        // 入库行：1月=5支/30kg
        var warehouse = result.Rows.Single(r => r.DisposalMethod == DisposalMethod.WarehouseEntry);
        warehouse.TotalQuantity.Should().Be(5);
        warehouse.TotalWeight.Should().Be(30);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_空值归未填写_全量守恒()
    {
        var ctx = CreateDbContext();
        var year = DateTime.Today.Year;
        ctx.Ncrs.AddRange(
            new Ncr
            {
                ReportDate = new DateTime(year, 3, 1), BatchNo = "B1", PipeCategory = MaterialType.OrderFinished,
                ResponsibilityCategory = null, ResponsibleDept = null, DisposalMethod = null,
                DefectiveQuantity = 7, DefectiveWeight = 25, Status = NcrStatus.Processing
            },
            new Ncr
            {
                ReportDate = new DateTime(year, 3, 2), BatchNo = "B2", PipeCategory = MaterialType.OrderFinished,
                ResponsibilityCategory = NcrResponsibilityKeys.MaterialTubeBlank, ResponsibleDept = "原料库",
                DisposalMethod = DisposalMethod.Scrap, DefectiveQuantity = 2, DefectiveWeight = 8, Status = NcrStatus.Processing
            });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetMonthlySummaryAsync();

        result.Rows.Should().HaveCount(2);
        // 空值行归「未填写」
        var emptyRow = result.Rows.Single(r => r.CategoryDisplay == "未填写");
        emptyRow.ResponsibleDept.Should().Be("未填写");
        emptyRow.DisposalMethodDisplay.Should().Be("未填写");
        emptyRow.Months[2].Quantity.Should().Be(7);
        emptyRow.TotalWeight.Should().Be(25);
        // 全量守恒：两行次品支数/重量合计 = 录入合计
        result.Rows.Sum(r => r.TotalQuantity).Should().Be(9);
        result.Rows.Sum(r => r.TotalWeight ?? 0).Should().Be(33);
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_跨年不统计()
    {
        var ctx = CreateDbContext();
        ctx.Ncrs.Add(new Ncr
        {
            ReportDate = DateTime.Today.AddYears(-1),
            BatchNo = "OLD",
            PipeCategory = MaterialType.OrderFinished,
            ResponsibilityCategory = NcrResponsibilityKeys.ProductionInternal,
            DisposalMethod = DisposalMethod.Rework,
            DefectiveQuantity = 99,
            DefectiveWeight = 500,
            Status = NcrStatus.Processing
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetMonthlySummaryAsync();

        result.Rows.Should().BeEmpty();
    }
}
