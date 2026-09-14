using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MES.Core.Constants;
using MES.Core.DTOs.Quality;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.Infrastructure;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Quality;
using MES.Services.Quality;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 不合格反馈单服务测试：CRUD、重量理论计算、批次带出、附件上限与磁盘联动。
/// </summary>
public class NonconformingFeedbackServiceTests : TestBase
{
    private readonly Mock<IAttachmentStorage> _storage = new();
    private readonly Mock<IProcessDefinitionService> _processDefinitionService = new();
    private readonly Mock<ISectionNameDisplayService> _sectionNameDisplayService = new();

    public NonconformingFeedbackServiceTests()
    {
        _storage.SetupGet(s => s.MaxFileSizeBytes).Returns(5 * 1024 * 1024);
        _storage.Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("stored.jpg");
        _storage.Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });
        _storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // 打印用：工序/工段显示名映射（测试不关心中文，返回空表走常量兜底）
        _processDefinitionService.Setup(s => s.GetProcessNameMapAsync())
            .ReturnsAsync(new Dictionary<string, string>());
        _sectionNameDisplayService.Setup(s => s.GetSectionNameMapAsync())
            .ReturnsAsync(new Dictionary<string, string>());
    }

    private NonconformingFeedbackService CreateService(AppDbContext ctx)
        => new(ctx, NullLogger<NonconformingFeedbackService>.Instance,
            new MemoryCache(new MemoryCacheOptions()), _storage.Object,
            _processDefinitionService.Object, _sectionNameDisplayService.Object);

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

    private async Task<ProcessGroup> SeedProcessGroupAsync(
        AppDbContext ctx, int batchId, string processName = "60冷轧",
        string? spec = "168*6", int seq = 1, int? straighten = 2)
    {
        var pg = new ProcessGroup
        {
            ProductionBatchId = batchId,
            BatchNo = "BATCH001",
            SequenceNumber = seq,
            ProcessName = processName,
            ManufacturingSpec = spec,
            Straighten = straighten
        };
        ctx.ProcessGroups.Add(pg);
        await ctx.SaveChangesAsync();
        return pg;
    }

    private static CreateNonconformingFeedbackRequest CreateRequest(
        int? defectWeight = null, int incomingQty = 100, decimal incomingWeight = 1000m, int defectQty = 10)
        => new()
        {
            ReportDate = DateTime.Today,
            Reporter = "李四",
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "168*6",
            SourceType = nameof(NonconformingFeedbackSourceType.ProductionSection),
            SectionName = SectionKeys.Straighten,
            IncomingQuantity = incomingQty,
            IncomingWeight = incomingWeight,
            DefectQuantity = defectQty,
            DefectWeight = defectWeight,
            ProblemDescription = "表面划伤"
        };

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
    public async Task GetAllAsync_关键字匹配工单号与问题描述()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        await svc.CreateAsync(CreateRequest());

        var byWorkOrder = await svc.GetAllAsync(new QueryParams { Keyword = "WO-001" });
        var byDesc = await svc.GetAllAsync(new QueryParams { Keyword = "划伤" });
        var none = await svc.GetAllAsync(new QueryParams { Keyword = "不存在" });

        byWorkOrder.TotalCount.Should().Be(1);
        byDesc.TotalCount.Should().Be(1);
        none.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAllAsync_反馈日期区间过滤()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        await svc.CreateAsync(CreateRequest());

        var hit = await svc.GetAllAsync(new QueryParams
        {
            ReportDateFrom = DateTime.Today.AddDays(-1),
            ReportDateTo = DateTime.Today
        });
        var miss = await svc.GetAllAsync(new QueryParams
        {
            ReportDateFrom = DateTime.Today.AddDays(2),
            ReportDateTo = DateTime.Today.AddDays(3)
        });

        hit.TotalCount.Should().Be(1);
        miss.TotalCount.Should().Be(0);
    }

    // ========== CreateAsync ==========

    [Fact]
    public async Task CreateAsync_生产编号不存在_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var req = CreateRequest();
        req.BatchNo = "NOPE";

        var act = () => svc.CreateAsync(req);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*NOPE*");
    }

    [Fact]
    public async Task CreateAsync_反馈人为空白_抛业务异常()
    {
        // 反馈人校验由 DTO [Required] 下移到本方法（扫码链先在 ScanQualityService 覆写成登录人再调用），
        // 用例锁死守卫仍在，避免「去掉 [Required]」演变成「完全不校验」。
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var req = CreateRequest();
        req.Reporter = "   ";

        var act = () => svc.CreateAsync(req);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("反馈人不能为空");
    }

    [Fact]
    public async Task CreateAsync_冗余批次字段并自动计算产类与序号()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var pg = await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        var dto = await svc.CreateAsync(CreateRequest());

        dto.WorkOrderNo.Should().Be("WO-001");
        dto.PlantGrade.Should().Be("304");
        dto.ProductionBatchId.Should().Be(batch.Id);
        dto.ProcessGroupId.Should().Be(pg.Id);
        dto.SequenceNumber.Should().Be(2);              // 对齐工序组 Straighten=2
        dto.ProductStatus.Should().Be(ProductStatuses.InProgress);
        dto.DataSource.Should().Be("MANUAL");
    }

    [Fact]
    public async Task CreateAsync_留空不合格重量_按支数比理论计算()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        // 1000kg / 100 支 × 10 支 = 100
        var dto = await svc.CreateAsync(CreateRequest(defectWeight: null));

        dto.DefectWeight.Should().Be(100);
    }

    [Fact]
    public async Task CreateAsync_手填不合格重量_优先于理论值()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        var dto = await svc.CreateAsync(CreateRequest(defectWeight: 77));

        dto.DefectWeight.Should().Be(77);
    }

    [Fact]
    public async Task CreateAsync_缺来料数据_不合格重量为空()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var req = CreateRequest(defectWeight: null);
        req.IncomingQuantity = null;

        var dto = await svc.CreateAsync(req);

        dto.DefectWeight.Should().BeNull();
    }

    // ========== 来源类型分支 ==========

    [Fact]
    public async Task CreateAsync_来源为空_抛业务异常()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var req = CreateRequest();
        req.SourceType = string.Empty;

        var act = () => svc.CreateAsync(req);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*来源类型无效*");
    }

    [Fact]
    public async Task CreateAsync_来源非法_抛业务异常()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var req = CreateRequest();
        req.SourceType = "NotASource";

        var act = () => svc.CreateAsync(req);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*来源类型无效*");
    }

    [Fact]
    public async Task CreateAsync_过程检验来源_不需检验项目_保留工段()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var req = CreateRequest();
        req.SourceType = nameof(NonconformingFeedbackSourceType.ProcessInspection);

        var dto = await svc.CreateAsync(req);

        dto.SourceType.Should().Be(nameof(NonconformingFeedbackSourceType.ProcessInspection));
        dto.SectionName.Should().Be(SectionKeys.Straighten);
        dto.SequenceNumber.Should().Be(2);
        dto.InspectionItem.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_成品检验来源_清空工段与序号_保留检验项目()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var req = CreateRequest();
        req.SourceType = nameof(NonconformingFeedbackSourceType.FinalInspection);
        req.InspectionItem = InspectionItem.PMIInspection;
        // 前端即使带上工段也要被来源归一化清掉（成品检验无工段概念）

        var dto = await svc.CreateAsync(req);

        dto.SourceType.Should().Be(nameof(NonconformingFeedbackSourceType.FinalInspection));
        dto.SectionName.Should().BeNull();
        dto.SequenceNumber.Should().BeNull();
        dto.InspectionItem.Should().Be(InspectionItem.PMIInspection);
    }

    [Fact]
    public async Task CreateAsync_成品检验来源_缺检验项目_抛业务异常()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var req = CreateRequest();
        req.SourceType = nameof(NonconformingFeedbackSourceType.FinalInspection);
        req.InspectionItem = null;

        var act = () => svc.CreateAsync(req);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*检验项目*");
    }

    [Fact]
    public async Task CreateAsync_非成品检验来源_缺工段_抛业务异常()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var req = CreateRequest();
        req.SourceType = nameof(NonconformingFeedbackSourceType.ProcessInspection);
        req.SectionName = null;

        var act = () => svc.CreateAsync(req);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*工段*");
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_不存在_返回null()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var dto = await svc.GetByIdAsync(999);

        dto.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_带出附件与张数()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest());
        ctx.NonconformingFeedbackAttachments.Add(new NonconformingFeedbackAttachment
        {
            FeedbackId = created.Id,
            FileName = "a.jpg",
            StoredName = "s1.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 10,
            SortOrder = 0
        });
        await ctx.SaveChangesAsync();

        var dto = await svc.GetByIdAsync(created.Id);

        dto.Should().NotBeNull();
        dto!.Attachments.Should().HaveCount(1);
        dto.AttachmentCount.Should().Be(1);
    }

    // ========== UpdateAsync / DeleteAsync ==========

    [Fact]
    public async Task UpdateAsync_重新计算理论重量()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest(defectWeight: null)); // 100

        var updated = await svc.UpdateAsync(created.Id, new UpdateNonconformingFeedbackRequest
        {
            ReportDate = DateTime.Today,
            Reporter = "王五",
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "168*6",
            SourceType = nameof(NonconformingFeedbackSourceType.ProductionSection),
            SectionName = SectionKeys.Straighten,
            IncomingQuantity = 50,
            IncomingWeight = 1000m,
            DefectQuantity = 10,
            DefectWeight = null,    // 留空 → 1000/50×10 = 200
            ProblemDescription = "改描述"
        });

        updated.Reporter.Should().Be("王五");
        updated.DefectWeight.Should().Be(200);
        updated.ProblemDescription.Should().Be("改描述");
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateNonconformingFeedbackRequest { BatchNo = "BATCH001" });

        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task UpdateAsync_来源改为成品检验_清空工段并写入检验项目()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest());

        var updated = await svc.UpdateAsync(created.Id, new UpdateNonconformingFeedbackRequest
        {
            ReportDate = DateTime.Today,
            Reporter = "李四",
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "168*6",
            SourceType = nameof(NonconformingFeedbackSourceType.FinalInspection),
            SectionName = SectionKeys.Straighten,   // 带工段也要被归一化清空
            InspectionItem = InspectionItem.EddyCurrent,
            DefectQuantity = 10
        });

        updated.SourceType.Should().Be(nameof(NonconformingFeedbackSourceType.FinalInspection));
        updated.SectionName.Should().BeNull();
        updated.SequenceNumber.Should().BeNull();
        updated.InspectionItem.Should().Be(InspectionItem.EddyCurrent);
    }

    [Fact]
    public async Task DeleteAsync_连同附件磁盘文件一起删除()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest());
        ctx.NonconformingFeedbackAttachments.Add(new NonconformingFeedbackAttachment
        {
            FeedbackId = created.Id,
            FileName = "a.jpg",
            StoredName = "s1.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 10
        });
        await ctx.SaveChangesAsync();

        await svc.DeleteAsync(created.Id);

        _storage.Verify(s => s.DeleteAsync("s1.jpg", It.IsAny<CancellationToken>()), Times.Once);
        (await svc.GetByIdAsync(created.Id)).Should().BeNull();
    }

    // ========== 筛选上下文 / 批次带出 ==========

    [Fact]
    public async Task GetFilterContextsAsync_含各字段去重值()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        await svc.CreateAsync(CreateRequest());

        var ctxMap = await svc.GetFilterContextsAsync();

        ctxMap["Reporter"].Should().Contain("李四");
        ctxMap["BatchNo"].Should().Contain("BATCH001");
        ctxMap["ProcessName"].Should().Contain("60冷轧");
        ctxMap["ReportDate"].Should().Contain(DateTime.Today.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task LookupBatchAsync_不存在返回null_存在带出工序组与工段()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        (await svc.LookupBatchAsync("NOPE")).Should().BeNull();

        var hit = await svc.LookupBatchAsync("BATCH001");

        hit.Should().NotBeNull();
        hit!.WorkOrderNo.Should().Be("WO-001");
        hit.PlantGrade.Should().Be("304");
        hit.ProcessGroups.Should().HaveCount(1);
        hit.ProcessGroups[0].ProcessName.Should().Be("60冷轧");
        hit.ProcessGroups[0].Sections.Should().Contain(SectionKeys.Straighten);
    }

    [Fact]
    public async Task LookupBatchAsync_批次含附加成检_标记HasAdditionalFinalInspection()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        var without = await svc.LookupBatchAsync("BATCH001");
        without!.HasAdditionalFinalInspection.Should().BeFalse();

        // 补一道「附加成检」工序组 → 预检/终检推导依据翻转
        await SeedProcessGroupAsync(ctx, batch.Id,
            processName: ProcessKeys.AdditionalFinalInspection, spec: "168*6", seq: 3, straighten: null);

        var withAdd = await svc.LookupBatchAsync("BATCH001");

        withAdd!.HasAdditionalFinalInspection.Should().BeTrue();
        withAdd.ProcessGroups.Should().HaveCount(2);
    }

    // ========== 附件 ==========

    [Fact]
    public async Task AddAttachmentAsync_保存并记录大小()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest());
        using var ms = new MemoryStream(new byte[42]);

        var att = await svc.AddAttachmentAsync(created.Id, ms, "photo.jpg", "image/jpeg");

        att.FileName.Should().Be("photo.jpg");
        att.SizeBytes.Should().Be(42);
        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), "photo.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddAttachmentAsync_超过张数上限_抛业务异常()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest());
        for (var i = 0; i < svc.MaxAttachmentCount; i++)
        {
            ctx.NonconformingFeedbackAttachments.Add(new NonconformingFeedbackAttachment
            {
                FeedbackId = created.Id,
                FileName = $"{i}.jpg",
                StoredName = $"s{i}.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 1,
                SortOrder = i
            });
        }
        await ctx.SaveChangesAsync();
        using var ms = new MemoryStream(new byte[1]);

        var act = () => svc.AddAttachmentAsync(created.Id, ms, "x.jpg", "image/jpeg");

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*上限*");
    }

    [Fact]
    public async Task AddAttachmentAsync_落库失败_回收已写盘文件且不留跟踪实体()
    {
        var ctx = CreateFailingDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest());

        ctx.FailOnSave = true;
        using var ms = new MemoryStream(new byte[1]);

        var act = () => svc.AddAttachmentAsync(created.Id, ms, "orphan.jpg", "image/jpeg");

        await act.Should().ThrowAsync<InvalidOperationException>();
        // 文件已落盘但落库失败 → 必须回收磁盘文件，且不残留被跟踪实体（否则下次 SaveChanges 会重试插入）
        _storage.Verify(s => s.DeleteAsync("stored.jpg", It.IsAny<CancellationToken>()), Times.Once);
        ctx.ChangeTracker.Entries<NonconformingFeedbackAttachment>().Should().BeEmpty();
        (await ctx.NonconformingFeedbackAttachments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AddAttachmentAsync_反馈单不存在_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        using var ms = new MemoryStream(new byte[1]);

        var act = () => svc.AddAttachmentAsync(999, ms, "x.jpg", "image/jpeg");

        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task GetAttachmentContentAsync_返回文件内容_不存在返回null()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest());
        var att = await svc.AddAttachmentAsync(created.Id, new MemoryStream(new byte[3]), "a.jpg", "image/jpeg");

        var content = await svc.GetAttachmentContentAsync(created.Id, att.Id);

        content.Should().NotBeNull();
        content!.Content.Should().Equal(1, 2, 3);
        content.ContentType.Should().Be("image/jpeg");
        (await svc.GetAttachmentContentAsync(created.Id, 9999)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAttachmentAsync_删除记录与磁盘文件()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest());
        var att = await svc.AddAttachmentAsync(created.Id, new MemoryStream(new byte[3]), "a.jpg", "image/jpeg");

        await svc.DeleteAttachmentAsync(created.Id, att.Id);

        (await svc.GetAttachmentsAsync(created.Id)).Should().BeEmpty();
        _storage.Verify(s => s.DeleteAsync("stored.jpg", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // ========== 打印 ==========

    /// <summary>1×1 PNG（最小合法图片，用于验证照片嵌入渲染路径）</summary>
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    [Fact]
    public async Task GetPrintPdfAsync_无照片_生成PDF()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest());

        var pdf = await svc.GetPrintPdfAsync(created.Id);

        pdf.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task GetPrintPdfAsync_含照片_嵌入图片不抛异常()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest());
        await svc.AddAttachmentAsync(created.Id, new MemoryStream(new byte[1]), "a.jpg", "image/jpeg");

        _storage.Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Convert.FromBase64String(TinyPngBase64));

        var pdf = await svc.GetPrintPdfAsync(created.Id);

        pdf.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task GetPrintPdfAsync_反馈单不存在_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetPrintPdfAsync(999);

        await act.Should().ThrowAsync<BusinessException>();
    }
}
