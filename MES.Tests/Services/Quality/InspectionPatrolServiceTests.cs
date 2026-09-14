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
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Quality;
using MES.Services.Quality;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 巡检单服务测试：CRUD、巡检明细一对多、整改块条件化、批次带出、按类型附件上限与磁盘联动、打印。
/// </summary>
public class InspectionPatrolServiceTests : TestBase
{
    private readonly Mock<IAttachmentStorage> _storage = new();
    private readonly Mock<IProcessDefinitionService> _processDefinitionService = new();
    private readonly Mock<ISectionNameDisplayService> _sectionNameDisplayService = new();

    public InspectionPatrolServiceTests()
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

    private InspectionPatrolService CreateService(AppDbContext ctx)
        => new(ctx, NullLogger<InspectionPatrolService>.Instance,
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

    private static CreateInspectionPatrolRequest CreateRequest(
        bool needRectification = false, params string[] itemNames)
    {
        var names = itemNames.Length > 0 ? itemNames : new[] { "外径尺寸", "表面质量" };
        return new CreateInspectionPatrolRequest
        {
            PatrolDate = DateTime.Today,
            Inspector = "李四",
            DataSource = "MANUAL",
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "168*6",
            SectionName = SectionKeys.Straighten,
            ProductionUnit = "一车间",
            EquipmentName = "冷轧机1号",
            ProductionOperator = "操作工甲",
            Items = names.Select(n => new InspectionPatrolItemRequest
            {
                ItemName = n,
                Result = "合格",
                Remark = "无异常"
            }).ToList(),
            NeedRectification = needRectification,
            RectificationDescription = needRectification ? "调整辊缝" : "不应保留",
            VerificationResult = needRectification ? "复检合格" : "不应保留",
            IsClosed = needRectification
        };
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
    public async Task GetAllAsync_关键字匹配工单号与巡检项()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        await svc.CreateAsync(CreateRequest());

        var byWorkOrder = await svc.GetAllAsync(new QueryParams { Keyword = "WO-001" });
        var byItem = await svc.GetAllAsync(new QueryParams { Keyword = "外径尺寸" });
        var none = await svc.GetAllAsync(new QueryParams { Keyword = "不存在" });

        byWorkOrder.TotalCount.Should().Be(1);
        byItem.TotalCount.Should().Be(1);
        none.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAllAsync_巡检日期区间过滤()
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

    [Fact]
    public async Task GetAllAsync_列表带出巡检项数与照片数()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest());
        await svc.AddAttachmentAsync(created.Id, InspectionPatrolPhotoTypes.Patrol,
            new MemoryStream(new byte[3]), "a.jpg", "image/jpeg");

        var result = await svc.GetAllAsync(new QueryParams());

        result.Items.Should().HaveCount(1);
        result.Items[0].ItemCount.Should().Be(2);
        result.Items[0].AttachmentCount.Should().Be(1);
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
    public async Task CreateAsync_巡检人为空白_抛业务异常()
    {
        // 巡检人校验由 DTO [Required] 下移到本方法（扫码链先在 ScanQualityService 覆写成登录人再调用），
        // 用例锁死守卫仍在，避免「去掉 [Required]」演变成「完全不校验」。
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var req = CreateRequest();
        req.Inspector = "   ";

        var act = () => svc.CreateAsync(req);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("巡检人不能为空");
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
        dto.ItemCount.Should().Be(2);
    }

    [Fact]
    public async Task CreateAsync_无巡检明细_抛业务异常()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var req = CreateRequest();
        req.Items = new List<InspectionPatrolItemRequest>();

        var act = () => svc.CreateAsync(req);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*巡检明细*");
    }

    [Fact]
    public async Task CreateAsync_仅空巡检项_视为无明细抛业务异常()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var req = CreateRequest();
        req.Items = new List<InspectionPatrolItemRequest> { new() { ItemName = "  " } };

        var act = () => svc.CreateAsync(req);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*巡检明细*");
    }

    [Fact]
    public async Task CreateAsync_不涉及整改_清空整改字段()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        var dto = await svc.CreateAsync(CreateRequest(needRectification: false));

        dto.NeedRectification.Should().BeFalse();
        dto.RectificationDescription.Should().BeNull();
        dto.VerificationResult.Should().BeNull();
        dto.IsClosed.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_涉及整改_保留整改字段()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        var dto = await svc.CreateAsync(CreateRequest(needRectification: true));

        dto.NeedRectification.Should().BeTrue();
        dto.RectificationDescription.Should().Be("调整辊缝");
        dto.VerificationResult.Should().Be("复检合格");
        dto.IsClosed.Should().BeTrue();
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_不存在_返回null()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        (await svc.GetByIdAsync(999)).Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_带出明细顺序与附件()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest());

        var dto = await svc.GetByIdAsync(created.Id);

        dto.Should().NotBeNull();
        dto!.Items.Should().HaveCount(2);
        dto.Items[0].ItemName.Should().Be("外径尺寸");
        dto.Items[1].ItemName.Should().Be("表面质量");
        dto.ItemCount.Should().Be(2);
    }

    // ========== UpdateAsync / DeleteAsync / SetClosedAsync ==========

    [Fact]
    public async Task UpdateAsync_巡检明细整组替换()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest());   // 2 条

        var updated = await svc.UpdateAsync(created.Id, new UpdateInspectionPatrolRequest
        {
            PatrolDate = DateTime.Today,
            Inspector = "王五",
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "168*6",
            SectionName = SectionKeys.Straighten,
            ProductionUnit = "二车间",
            Items = new List<InspectionPatrolItemRequest>
            {
                new() { ItemName = "壁厚尺寸", Result = "合格" }
            },
            NeedRectification = false
        });

        updated.Inspector.Should().Be("王五");
        updated.ProductionUnit.Should().Be("二车间");
        updated.Items.Should().HaveCount(1);
        updated.Items[0].ItemName.Should().Be("壁厚尺寸");
        updated.ItemCount.Should().Be(1);
        (await ctx.InspectionPatrolItems.CountAsync(i => i.PatrolId == created.Id)).Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateInspectionPatrolRequest { BatchNo = "BATCH001" });

        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task DeleteAsync_连同附件磁盘文件一起删除()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest());
        await svc.AddAttachmentAsync(created.Id, InspectionPatrolPhotoTypes.Patrol,
            new MemoryStream(new byte[3]), "a.jpg", "image/jpeg");

        await svc.DeleteAsync(created.Id);

        _storage.Verify(s => s.DeleteAsync("stored.jpg", It.IsAny<CancellationToken>()), Times.Once);
        (await svc.GetByIdAsync(created.Id)).Should().BeNull();
        (await ctx.InspectionPatrolItems.CountAsync(i => i.PatrolId == created.Id)).Should().Be(0);
    }

    [Fact]
    public async Task SetClosedAsync_切换闭环标记()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest(needRectification: false));

        await svc.SetClosedAsync(created.Id, true);

        (await svc.GetByIdAsync(created.Id))!.IsClosed.Should().BeTrue();
    }

    // ========== 筛选上下文 / 批次带出 / 在产候选 ==========

    [Fact]
    public async Task GetFilterContextsAsync_含各字段去重值()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        await svc.CreateAsync(CreateRequest());

        var ctxMap = await svc.GetFilterContextsAsync();

        ctxMap["Inspector"].Should().Contain("李四");
        ctxMap["BatchNo"].Should().Contain("BATCH001");
        ctxMap["ProcessName"].Should().Contain("60冷轧");
        ctxMap["ProductionUnit"].Should().Contain("一车间");
        ctxMap["PatrolDate"].Should().Contain(DateTime.Today.ToString("yyyy-MM-dd"));
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
        hit!.ProductionBatchId.Should().Be(batch.Id);
        hit.WorkOrderNo.Should().Be("WO-001");
        hit.PlantGrade.Should().Be("304");
        hit.ProcessGroups.Should().HaveCount(1);
        hit.ProcessGroups[0].ProcessName.Should().Be("60冷轧");
        hit.ProcessGroups[0].Sections.Should().Contain(SectionKeys.Straighten);
    }

    [Fact]
    public async Task LookupBatchAsync_带出批次在产信息_厂内与委外两态()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        // 厂内在产：CurrentEquipmentName 有值、CurrentOutsource 为空
        batch.CurrentEquipmentName = "冷轧机1号";
        batch.CurrentOutsource = null;
        await ctx.SaveChangesAsync();

        var internalHit = await svc.LookupBatchAsync("BATCH001");
        internalHit!.CurrentEquipmentName.Should().Be("冷轧机1号");
        internalHit.CurrentOutsource.Should().BeNull();

        // 委外在产：CurrentOutsource 有值（前端据此置灰 设备号/操作人）
        batch.CurrentOutsource = "一车间";
        batch.CurrentEquipmentName = null;
        await ctx.SaveChangesAsync();

        var outsourceHit = await svc.LookupBatchAsync("BATCH001");
        outsourceHit!.CurrentOutsource.Should().Be("一车间");
        outsourceHit.CurrentEquipmentName.Should().BeNull();
    }

    [Fact]
    public async Task GetPositionOptionsAsync_候选来自启用档案()
    {
        var ctx = CreateDbContext();
        ctx.OutsourceVendorProfiles.Add(new OutsourceVendorProfile
        {
            VendorCode = "V1", VendorName = "一车间", SectionName = SectionKeys.Straighten, IsActive = true
        });
        ctx.OutsourceVendorProfiles.Add(new OutsourceVendorProfile
        {
            VendorCode = "V2", VendorName = "停用车间", SectionName = SectionKeys.Straighten, IsActive = false
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var options = await svc.GetPositionOptionsAsync();

        options.ProductionUnits.Should().Contain("一车间").And.NotContain("停用车间");
    }

    // ========== 附件 ==========

    [Fact]
    public async Task AddAttachmentAsync_按类型分列保存并记录大小()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest());
        using var ms = new MemoryStream(new byte[42]);

        var att = await svc.AddAttachmentAsync(created.Id, InspectionPatrolPhotoTypes.Rectification,
            ms, "photo.jpg", "image/jpeg");

        att.PhotoType.Should().Be(InspectionPatrolPhotoTypes.Rectification);
        att.FileName.Should().Be("photo.jpg");
        att.SizeBytes.Should().Be(42);
        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), "photo.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddAttachmentAsync_超过该类张数上限_抛业务异常()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest());
        for (var i = 0; i < svc.MaxAttachmentPerType; i++)
            await svc.AddAttachmentAsync(created.Id, InspectionPatrolPhotoTypes.Patrol,
                new MemoryStream(new byte[1]), $"{i}.jpg", "image/jpeg");

        var act = () => svc.AddAttachmentAsync(created.Id, InspectionPatrolPhotoTypes.Patrol,
            new MemoryStream(new byte[1]), "x.jpg", "image/jpeg");

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*上限*");
    }

    [Fact]
    public async Task AddAttachmentAsync_两种类型各自独立计数()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest());
        for (var i = 0; i < svc.MaxAttachmentPerType; i++)
            await svc.AddAttachmentAsync(created.Id, InspectionPatrolPhotoTypes.Patrol,
                new MemoryStream(new byte[1]), $"{i}.jpg", "image/jpeg");

        // 巡检照片已满，整改验证照片仍可上传
        var att = await svc.AddAttachmentAsync(created.Id, InspectionPatrolPhotoTypes.Rectification,
            new MemoryStream(new byte[1]), "r.jpg", "image/jpeg");

        att.PhotoType.Should().Be(InspectionPatrolPhotoTypes.Rectification);
    }

    [Fact]
    public async Task AddAttachmentAsync_类型无效_抛业务异常()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest());

        var act = () => svc.AddAttachmentAsync(created.Id, "Bogus",
            new MemoryStream(new byte[1]), "x.jpg", "image/jpeg");

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*类型无效*");
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

        var act = () => svc.AddAttachmentAsync(created.Id, InspectionPatrolPhotoTypes.Patrol,
            new MemoryStream(new byte[1]), "orphan.jpg", "image/jpeg");

        await act.Should().ThrowAsync<InvalidOperationException>();
        // 文件已落盘但落库失败 → 必须回收磁盘文件，且不残留被跟踪实体（否则下次 SaveChanges 会重试插入）
        _storage.Verify(s => s.DeleteAsync("stored.jpg", It.IsAny<CancellationToken>()), Times.Once);
        ctx.ChangeTracker.Entries<InspectionPatrolAttachment>().Should().BeEmpty();
        (await ctx.InspectionPatrolAttachments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AddAttachmentAsync_巡检单不存在_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.AddAttachmentAsync(999, InspectionPatrolPhotoTypes.Patrol,
            new MemoryStream(new byte[1]), "x.jpg", "image/jpeg");

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
        var att = await svc.AddAttachmentAsync(created.Id, InspectionPatrolPhotoTypes.Patrol,
            new MemoryStream(new byte[3]), "a.jpg", "image/jpeg");

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
        var att = await svc.AddAttachmentAsync(created.Id, InspectionPatrolPhotoTypes.Patrol,
            new MemoryStream(new byte[3]), "a.jpg", "image/jpeg");

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
        var created = await svc.CreateAsync(CreateRequest(needRectification: true));

        var pdf = await svc.GetPrintPdfAsync(created.Id);

        pdf.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task GetPrintPdfAsync_含两类照片_嵌入图片不抛异常()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest(needRectification: true));
        await svc.AddAttachmentAsync(created.Id, InspectionPatrolPhotoTypes.Patrol,
            new MemoryStream(new byte[1]), "a.jpg", "image/jpeg");
        await svc.AddAttachmentAsync(created.Id, InspectionPatrolPhotoTypes.Rectification,
            new MemoryStream(new byte[1]), "b.jpg", "image/jpeg");

        _storage.Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Convert.FromBase64String(TinyPngBase64));

        var pdf = await svc.GetPrintPdfAsync(created.Id);

        pdf.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task GetPrintPdfAsync_巡检单不存在_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetPrintPdfAsync(999);

        await act.Should().ThrowAsync<BusinessException>();
    }

    // ========== 扫码整改回填（窄口径） / 按「批次+工序组+工段」定位 ==========

    [Fact]
    public async Task RectifyAsync_只写整改字段_不清空第一次巡检明细()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(
            CreateRequest(needRectification: true, "外径尺寸", "表面质量", "壁厚"));

        var rectified = await svc.RectifyAsync(created.Id, new InspectionPatrolRectifyRequest
        {
            VerificationResult = "复检合格",
            RectificationOperator = "王五",
            IsClosed = true
        });

        // 关键不变量：UpdateAsync 是明细整组替换，RectifyAsync 必须完全不动第一次的巡检明细
        rectified.Items.Select(i => i.ItemName).Should().Equal("外径尺寸", "表面质量", "壁厚");
        rectified.Items.Should().OnlyContain(i => i.Result == "合格" && i.Remark == "无异常");
        rectified.ItemCount.Should().Be(3);
        rectified.Items.Select(i => i.Id).Should().OnlyContain(id => id > 0);

        rectified.VerificationResult.Should().Be("复检合格");
        rectified.RectificationOperator.Should().Be("王五");
        rectified.IsClosed.Should().BeTrue();
        rectified.NeedRectification.Should().BeTrue();

        // 落库复核（绕开服务缓存）
        var storedItems = await ctx.InspectionPatrolItems
            .Where(i => i.PatrolId == created.Id)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .ToListAsync();
        storedItems.Select(i => i.ItemName).Should().Equal("外径尺寸", "表面质量", "壁厚");
    }

    [Fact]
    public async Task RectifyAsync_未涉及整改的单_拒绝回填()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);
        var created = await svc.CreateAsync(CreateRequest(needRectification: false));

        var act = () => svc.RectifyAsync(created.Id, new InspectionPatrolRectifyRequest
        {
            VerificationResult = "无需整改",
            RectificationOperator = "王五",
            IsClosed = true
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*未涉及整改*");

        // 拒绝后不得留下任何痕迹
        var stored = await ctx.InspectionPatrols.AsNoTracking().FirstAsync(r => r.Id == created.Id);
        stored.IsClosed.Should().BeFalse();
        stored.VerificationResult.Should().BeNull();
        stored.RectificationOperator.Should().BeNull();
    }

    [Fact]
    public async Task RectifyAsync_巡检单不存在_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.RectifyAsync(999, new InspectionPatrolRectifyRequest
        {
            VerificationResult = "x", RectificationOperator = "王五", IsClosed = true
        });

        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task GetByKeyAsync_按批次工序组工段过滤_待整改优先()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var pg = await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        // 待整改单先建（Id 更小）：若不按「待整改优先」排序，Id 降序会把它排在后面 → 该断言才能证伪
        var pendingRequest = CreateRequest(needRectification: true);
        pendingRequest.IsClosed = false;      // 第一次巡检：涉及整改、尚未闭环
        var pending = await svc.CreateAsync(pendingRequest);
        var plain = await svc.CreateAsync(CreateRequest(needRectification: false));

        // 干扰项：同批次同工序组，但工段不同 → 不得命中
        var otherSection = CreateRequest(needRectification: true);
        otherSection.SectionName = SectionKeys.Solution;
        await svc.CreateAsync(otherSection);

        var list = await svc.GetByKeyAsync("BATCH001", pg.Id, SectionKeys.Straighten);

        list.Should().HaveCount(2);
        list.Should().OnlyContain(d => d.BatchNo == "BATCH001"
                                      && d.ProcessGroupId == pg.Id
                                      && d.SectionName == SectionKeys.Straighten);
        // 不涉及整改的单永远不占「待整改」位 → 涉及整改且未闭环者排前
        list[0].Id.Should().Be(pending.Id);
        list[1].Id.Should().Be(plain.Id);
    }

    [Fact]
    public async Task GetByKeyAsync_整改闭环后_不再优先_回落新到旧()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var pg = await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        // 待整改单先建（Id 更小）：优先与否直接由排序键决定，而非 Id 降序兜底
        var pendingRequest = CreateRequest(needRectification: true);
        pendingRequest.IsClosed = false;
        var pending = await svc.CreateAsync(pendingRequest);
        var plain = await svc.CreateAsync(CreateRequest(needRectification: false));

        var before = await svc.GetByKeyAsync("BATCH001", pg.Id, SectionKeys.Straighten);
        before[0].Id.Should().Be(pending.Id);

        await svc.RectifyAsync(pending.Id, new InspectionPatrolRectifyRequest
        {
            VerificationResult = "已整改", RectificationOperator = "王五", IsClosed = true
        });

        // 闭环后不再优先，回落按 巡检日期→Id 新到旧（plain 更晚创建）
        var after = await svc.GetByKeyAsync("BATCH001", pg.Id, SectionKeys.Straighten);
        after.Select(d => d.Id).Should().Equal(plain.Id, pending.Id);
        after.Single(d => d.Id == pending.Id).IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task GetByKeyAsync_批次号为空_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var list = await svc.GetByKeyAsync("", 1, SectionKeys.Straighten);

        list.Should().BeEmpty();
    }
}
