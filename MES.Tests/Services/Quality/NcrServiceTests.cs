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
        => CreateService(ctx, Mock.Of<IAttachmentStorage>());

    private NcrService CreateService(AppDbContext ctx, IAttachmentStorage storage)
    {
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        return new(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<NcrService>.Instance, configMock.Object, new MemoryCache(new MemoryCacheOptions()), storage);
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

    /// <summary>过程检验记录（记录级：定位键含该记录 Id）</summary>
    private static ProcessInspection NewProcessInspection(int batchId, int quantity, int rework,
        string processName = "冷拔", int concession = 0, string? concessionRemark = null)
        => new()
        {
            ProductionBatchId = batchId,
            BatchNo = "BATCH001",
            ProcessName = processName,
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            InspectionItem = InspectionItem.Dimension.ToString(),
            InspectionDate = DateTime.Today,
            Quantity = quantity,
            DefectReworkQuantity = rework,
            QualifiedConcessionQuantity = concession,
            ConcessionRemark = concessionRemark,
            Inspector = "张三"
        };

    /// <summary>成品检验记录（组定位 = 批次 + 成检类型 + 检验项目）</summary>
    private static FinalInspection NewFinalInspection(int batchId, InspectionType inspectionType)
        => new()
        {
            ProductionBatchId = batchId,
            BatchNo = "BATCH001",
            InspectionItem = InspectionItem.Dimension,
            InspectionType = inspectionType.ToString(),
            InspectionDate = DateTime.Today,
            Quantity = 100,
            QualifiedQuantity = 80,
            DefectReworkQuantity = 20,
            Operator = "张三"
        };

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

    [Fact]
    public async Task CreateAsync_处置方式中文归一为字典Key_流向原样落库()
    {
        var ctx = CreateDbContext();
        await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateNcrRequest
        {
            ReportDate = DateTime.Today,
            BatchNo = "BATCH001",
            PipeCategory = MaterialType.OrderFinished,
            FlowDirection = FlowDirection.Rework,
            DisposalMethod = "入次品库(报废)"
        });

        // 处置方式 = 字典 Key（中文反查归一），流向 = 枚举原样
        result.DisposalMethod.Should().Be(NcrDisposalKeys.Scrap);
        result.FlowDirection.Should().Be(FlowDirection.Rework);
        var saved = await ctx.Ncrs.FirstAsync();
        saved.DisposalMethod.Should().Be(NcrDisposalKeys.Scrap);
        saved.FlowDirection.Should().Be(FlowDirection.Rework);
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
        // 批次主键回带（建单页「批次执行进度」入口依赖）
        result.ProductionBatchId.Should().BeGreaterThan(0);
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

    // ========== GetSourcePhotosAsync（来源照片：记录级一对一）==========

    [Fact]
    public async Task GetSourcePhotosAsync_过程检验记录键_只返回该条记录的照片()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var target = NewProcessInspection(batch.Id, quantity: 100, rework: 20);
        var other = NewProcessInspection(batch.Id, quantity: 100, rework: 20);
        ctx.ProcessInspections.AddRange(target, other);
        await ctx.SaveChangesAsync();

        ctx.ProcessInspectionAttachments.AddRange(
            new ProcessInspectionAttachment { ProcessInspectionId = target.Id, FileName = "a.jpg", StoredName = "s-a.jpg", ContentType = "image/jpeg", SizeBytes = 10, SortOrder = 1 },
            new ProcessInspectionAttachment { ProcessInspectionId = other.Id, FileName = "b.jpg", StoredName = "s-b.jpg", ContentType = "image/jpeg", SizeBytes = 10, SortOrder = 1 });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var groupKey = $"{nameof(NcrPendingSourceType.ProcessInspection)}|{batch.Id}|冷拔||{InspectionItem.Dimension}|{target.Id}";
        var result = await svc.GetSourcePhotosAsync(groupKey, null);

        var group = result.Should().ContainSingle().Which;
        group.RecordId.Should().Be(target.Id);
        group.Photos.Should().ContainSingle().Which.FileName.Should().Be("a.jpg");
    }

    [Fact]
    public async Task GetSourcePhotosAsync_该记录无照片_返回空列表()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var inspection = NewProcessInspection(batch.Id, quantity: 100, rework: 20);
        ctx.ProcessInspections.Add(inspection);
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var groupKey = $"{nameof(NcrPendingSourceType.ProcessInspection)}|{batch.Id}|冷拔||{InspectionItem.Dimension}|{inspection.Id}";
        var result = await svc.GetSourcePhotosAsync(groupKey, null);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSourcePhotosAsync_成品检验记录键_返回该条记录照片()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var inspection = NewFinalInspection(batch.Id, InspectionType.FormalInspection);
        ctx.FinalInspections.Add(inspection);
        await ctx.SaveChangesAsync();

        ctx.FinalInspectionAttachments.Add(new FinalInspectionAttachment
        {
            FinalInspectionId = inspection.Id, FileName = "f.jpg", StoredName = "s-f.jpg",
            ContentType = "image/jpeg", SizeBytes = 10, SortOrder = 1
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var groupKey = $"{nameof(NcrPendingSourceType.FinalInspection)}|{batch.Id}||{InspectionType.FormalInspection}|{InspectionItem.Dimension}|{inspection.Id}";
        var result = await svc.GetSourcePhotosAsync(groupKey, null);

        var group = result.Should().ContainSingle().Which;
        group.Kind.Should().Be(nameof(NcrPendingSourceType.FinalInspection));
        group.Photos.Should().ContainSingle().Which.FileName.Should().Be("f.jpg");
    }

    [Fact]
    public async Task GetSourcePhotosAsync_不合格反馈_返回反馈单照片()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var feedback = new NonconformingFeedback
        {
            ReportDate = DateTime.Today,
            Reporter = "李四",
            SourceType = nameof(NonconformingFeedbackSourceType.ProductionSection),
            ProductionBatchId = batch.Id,
            BatchNo = "BATCH001",
            ProcessGroupId = 1,
            ProcessName = "冷拔",
            SectionName = SectionKeys.ColdRollDraw,
            DefectQuantity = 3
        };
        ctx.NonconformingFeedbacks.Add(feedback);
        await ctx.SaveChangesAsync();

        ctx.NonconformingFeedbackAttachments.Add(new NonconformingFeedbackAttachment
        {
            FeedbackId = feedback.Id, FileName = "fb.jpg", StoredName = "s-fb.jpg",
            ContentType = "image/jpeg", SizeBytes = 10, SortOrder = 1
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetSourcePhotosAsync(null, feedback.Id);

        var group = result.Should().ContainSingle().Which;
        group.Kind.Should().Be(nameof(NcrPendingSourceType.NonconformingFeedback));
        group.Photos.Should().ContainSingle().Which.FileName.Should().Be("fb.jpg");
    }

    [Fact]
    public async Task GetSourcePhotosAsync_存量旧5段键_返回空列表()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var inspection = NewProcessInspection(batch.Id, quantity: 100, rework: 20);
        ctx.ProcessInspections.Add(inspection);
        await ctx.SaveChangesAsync();

        ctx.ProcessInspectionAttachments.Add(new ProcessInspectionAttachment
        {
            ProcessInspectionId = inspection.Id, FileName = "a.jpg", StoredName = "s-a.jpg",
            ContentType = "image/jpeg", SizeBytes = 10, SortOrder = 1
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        // 存量 5 段旧键无检验记录 Id，无法记录级定位 → 不展示入口
        var legacyKey = $"{nameof(NcrPendingSourceType.ProcessInspection)}|{batch.Id}|冷拔||{InspectionItem.Dimension}";
        var result = await svc.GetSourcePhotosAsync(legacyKey, null);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSourcePhotosAsync_键与参数均为空_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetSourcePhotosAsync(null, null);

        result.Should().BeEmpty();
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
            Quantity = 90,
            DefectReworkQuantity = 10,
            TheoreticalReworkWeight = 25,
            Inspector = "张三"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        result.Should().Contain(r => r.FlowDirection == FlowDirection.Rework
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
            Quantity = 90,
            DefectReworkQuantity = 10,
            Inspector = "张三"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        result.Should().NotBeEmpty();
        result.Should().Contain(r => r.FlowDirection == FlowDirection.Rework);
        result[0].BatchNo.Should().Be("BATCH001");
    }

    [Fact]
    public async Task GetPendingChecksAsync_该条记录已建NCR_不再列出()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var inspection = NewProcessInspection(batch.Id, quantity: 90, rework: 10);
        ctx.ProcessInspections.Add(inspection);
        await ctx.SaveChangesAsync();

        ctx.Ncrs.Add(new Ncr
        {
            ReportDate = DateTime.Today,
            BatchNo = "BATCH001",
            PipeCategory = MaterialType.OrderFinished,
            FlowDirection = FlowDirection.Rework,
            SourceInspectionItem = InspectionItem.Dimension.ToString(),
            // 记录级定位键：{来源类型}|{批次Id}|{工序}|{成检类型}|{检验项目}|{检验记录Id}
            SourceGroupKey = $"{nameof(NcrPendingSourceType.ProcessInspection)}|{batch.Id}|冷拔||{InspectionItem.Dimension}|{inspection.Id}",
            Status = NcrStatus.Processing
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingChecksAsync_该条记录已建NCR_同维度其它记录仍列出()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var handled = NewProcessInspection(batch.Id, quantity: 90, rework: 10);
        ctx.ProcessInspections.Add(handled);
        await ctx.SaveChangesAsync();

        ctx.Ncrs.Add(new Ncr
        {
            ReportDate = DateTime.Today,
            BatchNo = "BATCH001",
            PipeCategory = MaterialType.OrderFinished,
            SourceGroupKey = $"{nameof(NcrPendingSourceType.ProcessInspection)}|{batch.Id}|冷拔||{InspectionItem.Dimension}|{handled.Id}",
            Status = NcrStatus.Processing
        });
        var other = NewProcessInspection(batch.Id, quantity: 90, rework: 10);
        ctx.ProcessInspections.Add(other);
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        // 记录级去重：同一批次同一工序的另一条检验记录不受影响
        var row = result.Should().ContainSingle().Which;
        row.InspectionRecordId.Should().Be(other.Id);
    }

    [Fact]
    public async Task GetPendingChecksAsync_存量旧键NCR_该维度全部记录不再列出()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        ctx.ProcessInspections.Add(NewProcessInspection(batch.Id, quantity: 90, rework: 10));
        ctx.Ncrs.Add(new Ncr
        {
            ReportDate = DateTime.Today,
            BatchNo = "BATCH001",
            PipeCategory = MaterialType.OrderFinished,
            // 记录级改造前的 5 段旧键：视为该维度全部检验记录已处理
            SourceGroupKey = $"{nameof(NcrPendingSourceType.ProcessInspection)}|{batch.Id}|冷拔||{InspectionItem.Dimension}",
            Status = NcrStatus.Processing
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingChecksAsync_不合格反馈_未生成NCR_无条件列出()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        ctx.NonconformingFeedbacks.Add(new NonconformingFeedback
        {
            ReportDate = DateTime.Today,
            Reporter = "李四",
            DataSource = "MANUAL",
            SourceType = nameof(NonconformingFeedbackSourceType.ProductionSection),
            ProductionBatchId = batch.Id,
            BatchNo = "BATCH001",
            WorkOrderNo = "WO-001",
            ProcessGroupId = 1,
            ProcessName = "60冷轧",
            ManufacturingSpec = "168*6",
            SectionName = SectionKeys.Straighten,
            SequenceNumber = 1,
            PlantGrade = "304",
            IncomingQuantity = 100,
            IncomingWeight = 1000m,
            DefectQuantity = 10,
            DefectWeight = 100,
            ProblemDescription = "表面划伤"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        // 人工上报不受阈值约束：即便不合格支数远低于阈值也必须列出
        var item = result.Should().ContainSingle(r =>
            r.SourceType == nameof(NcrPendingSourceType.NonconformingFeedback)).Which;
        item.BatchNo.Should().Be("BATCH001");
        item.FlowDirection.Should().BeNull();           // 流向留空（人工上报无检验记录带出）
        item.DefectQuantity.Should().Be(10);
        item.TotalQuantity.Should().Be(100);            // 分母 = 来料支数
        item.Percentage.Should().Be(10m);               // 10 / 100
        item.NonconformingFeedbackId.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPendingChecksAsync_不合格反馈_来料支数为空_占比为0()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        ctx.NonconformingFeedbacks.Add(new NonconformingFeedback
        {
            ReportDate = DateTime.Today,
            Reporter = "李四",
            SourceType = nameof(NonconformingFeedbackSourceType.ProductionSection),
            ProductionBatchId = batch.Id,
            BatchNo = "BATCH001",
            ProcessGroupId = 1,
            ProcessName = "60冷轧",
            SectionName = SectionKeys.Straighten,
            DefectQuantity = 3
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        var item = result.Should().ContainSingle(r =>
            r.SourceType == nameof(NcrPendingSourceType.NonconformingFeedback)).Which;
        item.TotalQuantity.Should().Be(0);
        item.Percentage.Should().Be(0m);
    }

    [Fact]
    public async Task GetPendingChecksAsync_不合格反馈_已生成NCR_不再列出()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var feedback = new NonconformingFeedback
        {
            ReportDate = DateTime.Today,
            Reporter = "李四",
            SourceType = nameof(NonconformingFeedbackSourceType.ProductionSection),
            ProductionBatchId = batch.Id,
            BatchNo = "BATCH001",
            ProcessGroupId = 1,
            ProcessName = "60冷轧",
            SectionName = SectionKeys.Straighten,
            DefectQuantity = 10
        };
        ctx.NonconformingFeedbacks.Add(feedback);
        await ctx.SaveChangesAsync();

        ctx.Ncrs.Add(new Ncr
        {
            ReportDate = DateTime.Today,
            BatchNo = "BATCH001",
            PipeCategory = MaterialType.OrderFinished,
            NonconformingFeedbackId = feedback.Id,
            Status = NcrStatus.Processing
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        result.Should().NotContain(r => r.SourceType == nameof(NcrPendingSourceType.NonconformingFeedback));
    }

    // ---------- 阈值口径（单条检验记录严格大于绝对支数与占比）----------

    [Fact]
    public async Task GetPendingChecksAsync_恰好等于绝对支数阈值_不列出()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        // 5 支 / 6 支 = 83%(远超占比阈值)，隔离出「绝对支数须严格大于」这一条
        ctx.ProcessInspections.Add(NewProcessInspection(batch.Id, quantity: 6, rework: 5));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingChecksAsync_超过绝对支数阈值_列出()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        ctx.ProcessInspections.Add(NewProcessInspection(batch.Id, quantity: 6, rework: 6));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        result.Should().ContainSingle().Which.Bucket.Should().Be(NcrPendingBucket.OverageMissing);
    }

    [Fact]
    public async Task GetPendingChecksAsync_恰好等于占比阈值_不列出()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        // 10 > 5 但 10/100 = 10% 并非严格大于，隔离出占比阈值边界
        ctx.ProcessInspections.Add(NewProcessInspection(batch.Id, quantity: 100, rework: 10));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingChecksAsync_同批次同工序_单条不超则均不列出()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        // 记录级口径：单条 10/100=10%（不过占比）、5 支（不过支数）→ 均不列出（不再按组合计 15/110=13.6% 触发）
        ctx.ProcessInspections.Add(NewProcessInspection(batch.Id, quantity: 100, rework: 10));
        ctx.ProcessInspections.Add(NewProcessInspection(batch.Id, quantity: 10, rework: 5));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingChecksAsync_同批次同工序_多条各自成行()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        ctx.ProcessInspections.Add(NewProcessInspection(batch.Id, quantity: 100, rework: 20));
        ctx.ProcessInspections.Add(NewProcessInspection(batch.Id, quantity: 80, rework: 16));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        // 同一批次同一工序的 2 条检验记录各自成行（记录级），定位键互不相同
        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.DefectQuantity == 20 || r.DefectQuantity == 16);
        result.Select(r => r.TotalQuantity).Should().BeEquivalentTo(new[] { 100, 80 });
        result.Select(r => r.GroupKey).Should().OnlyHaveUniqueItems();
        result.Should().OnlyContain(r => r.InspectionRecordId > 0);
    }

    [Fact]
    public async Task GetPendingChecksAsync_同批次不同工序_各自成行()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        ctx.ProcessInspections.Add(NewProcessInspection(batch.Id, quantity: 100, rework: 20, processName: "冷拔"));
        ctx.ProcessInspections.Add(NewProcessInspection(batch.Id, quantity: 100, rework: 20, processName: "矫直"));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        result.Should().HaveCount(2);
        result.Select(r => r.GroupKey).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GetPendingChecksAsync_让步放行计入分子_可单独触发()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        // 无任何流向支，仅让步放行 20 支：仍计入不合格合计并触发，但次品支数与流向保持 0/null
        ctx.ProcessInspections.Add(NewProcessInspection(batch.Id, quantity: 100, rework: 0, concession: 20));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        var row = result.Should().ContainSingle().Which;
        row.ConcessionQuantity.Should().Be(20);
        row.DefectQuantity.Should().Be(0);
        row.FlowDirection.Should().BeNull();
        row.Percentage.Should().Be(20m);
    }

    [Fact]
    public async Task GetPendingChecksAsync_让步说明_取该条记录()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        ctx.ProcessInspections.Add(NewProcessInspection(batch.Id, quantity: 50, rework: 10, concessionRemark: "壁厚超差"));
        ctx.ProcessInspections.Add(NewProcessInspection(batch.Id, quantity: 50, rework: 10, concessionRemark: "外径超差"));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        // 记录级：每条记录只带自己的让步说明，不再跨记录拼接
        result.Should().HaveCount(2);
        result.Select(r => r.ConcessionRemark).Should().BeEquivalentTo(new[] { "壁厚超差", "外径超差" });
    }

    [Fact]
    public async Task GetPendingChecksAsync_成品检验_成检类型不同_各自成行()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        ctx.FinalInspections.Add(NewFinalInspection(batch.Id, InspectionType.PreInspection));
        ctx.FinalInspections.Add(NewFinalInspection(batch.Id, InspectionType.FormalInspection));
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.SourceType == nameof(NcrPendingSourceType.FinalInspection));
        result.Select(r => r.InspectionType)
            .Should().BeEquivalentTo(new[] { nameof(InspectionType.PreInspection), nameof(InspectionType.FormalInspection) });
    }

    [Fact]
    public async Task GetPendingChecksAsync_已有主动反馈_被动行不再列出()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        ctx.ProcessInspections.Add(NewProcessInspection(batch.Id, quantity: 100, rework: 20));
        ctx.NonconformingFeedbacks.Add(new NonconformingFeedback
        {
            ReportDate = DateTime.Today,
            Reporter = "李四",
            SourceType = nameof(NonconformingFeedbackSourceType.ProductionSection),
            ProductionBatchId = batch.Id,
            BatchNo = "BATCH001",
            ProcessGroupId = 1,
            ProcessName = "冷拔",
            SectionName = SectionKeys.ColdRollDraw,
            DefectQuantity = 3
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        // 被动行被主动反馈覆盖；主动反馈本身仍作为「正常提交」列出
        result.Should().ContainSingle()
            .Which.Bucket.Should().Be(NcrPendingBucket.NormalSubmitted);
    }

    [Fact]
    public async Task GetPendingChecksAsync_已有主动NCR_主动反馈不再列出()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var feedback = new NonconformingFeedback
        {
            ReportDate = DateTime.Today,
            Reporter = "李四",
            SourceType = nameof(NonconformingFeedbackSourceType.ProductionSection),
            ProductionBatchId = batch.Id,
            BatchNo = "BATCH001",
            ProcessGroupId = 1,
            ProcessName = "冷拔",
            SectionName = SectionKeys.ColdRollDraw,
            DefectQuantity = 3
        };
        ctx.NonconformingFeedbacks.Add(feedback);
        await ctx.SaveChangesAsync();
        ctx.Ncrs.Add(new Ncr
        {
            ReportDate = DateTime.Today,
            BatchNo = "BATCH001",
            PipeCategory = MaterialType.OrderFinished,
            NonconformingFeedbackId = feedback.Id,
            Status = NcrStatus.Processing
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        result.Should().NotContain(r => r.Bucket == NcrPendingBucket.NormalSubmitted);
    }

    // ---------- 忽略（登记即忽略） ----------

    [Fact]
    public async Task CreateAsync_忽略状态_存为忽略档()
    {
        var ctx = CreateDbContext();
        await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateNcrRequest
        {
            ReportDate = DateTime.Today,
            BatchNo = "BATCH001",
            PipeCategory = MaterialType.OrderFinished,
            Status = NcrStatus.Ignored
        });

        result.Status.Should().Be(NcrStatus.Ignored);
        (await ctx.Ncrs.SingleAsync()).Status.Should().Be(NcrStatus.Ignored);
    }

    [Fact]
    public async Task CreateAsync_非忽略状态_抛业务异常()
    {
        var ctx = CreateDbContext();
        await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateNcrRequest
        {
            ReportDate = DateTime.Today,
            BatchNo = "BATCH001",
            PipeCategory = MaterialType.OrderFinished,
            Status = NcrStatus.Pending
        });

        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task GetPendingChecksAsync_该条记录已登记忽略NCR_不再列出()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var inspection = NewProcessInspection(batch.Id, quantity: 100, rework: 20);
        ctx.ProcessInspections.Add(inspection);
        await ctx.SaveChangesAsync();

        ctx.Ncrs.Add(new Ncr
        {
            ReportDate = DateTime.Today,
            BatchNo = "BATCH001",
            PipeCategory = MaterialType.OrderFinished,
            SourceGroupKey = $"{nameof(NcrPendingSourceType.ProcessInspection)}|{batch.Id}|冷拔||{InspectionItem.Dimension}|{inspection.Id}",
            Status = NcrStatus.Ignored
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPendingChecksAsync();

        result.Should().NotContain(r => r.Bucket == NcrPendingBucket.OverageMissing);
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
                DisposalMethod = NcrDisposalKeys.Rework, DefectiveQuantity = 10, DefectiveWeight = 50, Status = NcrStatus.Processing
            },
            new Ncr
            {
                ReportDate = new DateTime(year, 1, 20), BatchNo = "B2", PipeCategory = MaterialType.OrderFinished,
                ResponsibilityCategory = NcrResponsibilityKeys.ProductionInternal, ResponsibleDept = "生产一部",
                DisposalMethod = NcrDisposalKeys.InProcessWarehouse, DefectiveQuantity = 5, DefectiveWeight = 30, Status = NcrStatus.Processing
            },
            new Ncr
            {
                ReportDate = new DateTime(year, 2, 5), BatchNo = "B3", PipeCategory = MaterialType.OrderFinished,
                ResponsibilityCategory = NcrResponsibilityKeys.ProductionInternal, ResponsibleDept = "生产一部",
                DisposalMethod = NcrDisposalKeys.Rework, DefectiveQuantity = 3, DefectiveWeight = 20, Status = NcrStatus.Processing
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
        var rework = result.Rows.Single(r => r.DisposalMethod == NcrDisposalKeys.Rework);
        rework.Months.Should().HaveCount(12);
        rework.Months[0].Quantity.Should().Be(10);
        rework.Months[0].Weight.Should().Be(50);
        rework.Months[1].Quantity.Should().Be(3);
        rework.Months[1].Weight.Should().Be(20);
        rework.TotalQuantity.Should().Be(13);
        rework.TotalWeight.Should().Be(70);
        // 入在制库行：1月=5支/30kg
        var warehouse = result.Rows.Single(r => r.DisposalMethod == NcrDisposalKeys.InProcessWarehouse);
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
                DisposalMethod = NcrDisposalKeys.Scrap, DefectiveQuantity = 2, DefectiveWeight = 8, Status = NcrStatus.Processing
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
            DisposalMethod = NcrDisposalKeys.Rework,
            DefectiveQuantity = 99,
            DefectiveWeight = 500,
            Status = NcrStatus.Processing
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetMonthlySummaryAsync();

        result.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMonthlySummaryAsync_忽略档不纳入统计()
    {
        var ctx = CreateDbContext();
        var today = DateTime.Today;
        ctx.Ncrs.AddRange(
            new Ncr
            {
                ReportDate = today,
                BatchNo = "B1",
                PipeCategory = MaterialType.OrderFinished,
                ResponsibilityCategory = NcrResponsibilityKeys.ProductionInternal,
                ResponsibleDept = "生产部",
                DisposalMethod = NcrDisposalKeys.Rework,
                DefectiveQuantity = 10,
                DefectiveWeight = 100,
                Status = NcrStatus.Processing
            },
            new Ncr
            {
                ReportDate = today,
                BatchNo = "B2",
                PipeCategory = MaterialType.OrderFinished,
                ResponsibilityCategory = NcrResponsibilityKeys.ProductionInternal,
                ResponsibleDept = "生产部",
                DisposalMethod = NcrDisposalKeys.Rework,
                DefectiveQuantity = 77,
                DefectiveWeight = 700,
                Status = NcrStatus.Ignored
            });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetMonthlySummaryAsync();

        // 忽略档整行不参与：合计只累计 Processing 那条
        var row = result.Rows.Should().ContainSingle().Subject;
        row.TotalQuantity.Should().Be(10);
        row.TotalWeight.Should().Be(100);
    }

    // ========== 单据式打印（含关联不合格反馈照片） ==========

    /// <summary>1×1 PNG（最小合法图片，用于验证照片嵌入渲染路径）</summary>
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    /// <summary>附件存储 mock：读取返回最小合法 PNG</summary>
    private static Mock<IAttachmentStorage> CreateStorageMock()
    {
        var storage = new Mock<IAttachmentStorage>();
        storage.SetupGet(s => s.MaxFileSizeBytes).Returns(5 * 1024 * 1024);
        storage.Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Convert.FromBase64String(TinyPngBase64));
        return storage;
    }

    /// <summary>建关联反馈单 + 2 张照片，并把 NCR 挂到该反馈单</summary>
    private static async Task SeedFeedbackWithPhotosAsync(AppDbContext ctx, Ncr ncr)
    {
        var feedback = new NonconformingFeedback
        {
            ReportDate = DateTime.Today,
            Reporter = "张三",
            SourceType = nameof(NonconformingFeedbackSourceType.ProductionSection),
            BatchNo = ncr.BatchNo,
            ProcessName = "60冷轧",
            SectionName = "ColdRollDraw"
        };
        ctx.NonconformingFeedbacks.Add(feedback);
        await ctx.SaveChangesAsync();

        ctx.NonconformingFeedbackAttachments.Add(new NonconformingFeedbackAttachment
        {
            FeedbackId = feedback.Id,
            FileName = "问题1.jpg",
            StoredName = "s1.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 3,
            SortOrder = 0
        });
        ctx.NonconformingFeedbackAttachments.Add(new NonconformingFeedbackAttachment
        {
            FeedbackId = feedback.Id,
            FileName = "问题2.jpg",
            StoredName = "s2.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 3,
            SortOrder = 1
        });
        ncr.NonconformingFeedbackId = feedback.Id;
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task PrintSelectedAsync_无关联反馈_生成PDF()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var ctx = CreateDbContext();
        var ncr = await SeedNcrAsync(ctx);
        var svc = CreateService(ctx);

        var pdf = await svc.PrintSelectedAsync(new[] { ncr.Id }, new List<PrintColumnDef>());

        pdf.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task PrintSelectedAsync_含关联反馈照片_嵌入图片不抛异常()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var ctx = CreateDbContext();
        var ncr = await SeedNcrAsync(ctx);
        await SeedFeedbackWithPhotosAsync(ctx, ncr);
        var storage = CreateStorageMock();
        var svc = CreateService(ctx, storage.Object);

        var pdf = await svc.PrintSelectedAsync(new[] { ncr.Id }, new List<PrintColumnDef>());

        pdf.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        storage.Verify(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task PrintSelectedAsync_选中记录不存在_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.PrintSelectedAsync(new[] { 9999 }, new List<PrintColumnDef>());

        await act.Should().ThrowAsync<BusinessException>();
    }
}
