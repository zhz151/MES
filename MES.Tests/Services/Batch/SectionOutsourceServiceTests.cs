using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services;
using MES.Services.Batch;
using MES.Tests.Tests;
using MES.Data.Entities.Batch;
using MES.Core.DTOs.Batch;
using MES.Core.Interfaces.Batch;
using MES.Core.Interfaces.Configuration;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Tests.Services;

/// <summary>
/// 工段委外服务测试：委外发出CRUD、委外回收、批量操作、状态更新
/// </summary>
public class SectionOutsourceServiceTests : TestBase
{
    private static SectionOutsourceService CreateService(AppDbContext ctx)
    {
        var loggerMock = new Mock<ILogger<SectionOutsourceService>>();
        var prodRecSvcMock = new Mock<IProductionRecordService>();
        var configMock = new Mock<IConfigParameterService>();
        configMock.Setup(x => x.GetConfigMapAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, decimal>());
        return new SectionOutsourceService(ctx, loggerMock.Object, prodRecSvcMock.Object, configMock.Object, new MemoryCache(new MemoryCacheOptions()), Mock.Of<ISectionNameDisplayService>(), CreateProcessDefinitionServiceMock());
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

    private async Task<SectionOutsource> SeedOutsourceAsync(AppDbContext ctx, int batchId,
        string vendor = "委外厂A", SectionOutsourceStatus status = SectionOutsourceStatus.PendingRecovery,
        bool isInternal = false)
    {
        var entity = new SectionOutsource
        {
            ProductionBatchId = batchId,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            SequenceNumber = 1,
            OutsourceVendor = vendor,
            SendOutDate = DateTime.Today,
            SendQuantity = 10,
            SendWeight = 1000m,
            Status = status,
            IsInternal = isInternal
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

        var result = await svc.GetByIdsAsync(id.ToString());

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(id);
    }

    [Fact]
    public async Task GetByIdsAsync_空数组_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdsAsync("");

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
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            ColdRollDraw = 1
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateSectionOutsourceRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
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
            BatchNo = "NONEXISTENT",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            OutsourceVendor = "委外厂A",
            SendOutDate = DateTime.Today
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
            new() { BatchNo = "BATCH001", ProcessName = "60冷轧", ManufacturingSpec = "219*8",
                SectionName = SectionKeys.ColdRollDraw, OutsourceVendor = "委外厂A", OutsourceSpec = "219*8", SendOutDate = DateTime.Today },
            new() { BatchNo = "BATCH002", ProcessName = "酸洗", ManufacturingSpec = "219*8",
                SectionName = SectionKeys.Pickle, OutsourceVendor = "委外厂B", OutsourceSpec = "219*8", SendOutDate = DateTime.Today }
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
            ProcessName = "60冷轧",
            ManufacturingSpec = "273*10",
            PlantGrade = "304",
            OutsourceSpec = "273*10",
            SectionName = SectionKeys.ColdRollDraw,
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
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
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

    // ========== 筛选测试（FilterDescriptor） ==========

    [Fact]
    public async Task GetPagedAsync_Filters_ProcessName_Contains_返回匹配()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-FLTR");
        await SeedOutsourceAsync(ctx, batch.Id, vendor: "委外厂A");
        ctx.SectionOutsources.Add(new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessName = "酸洗",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Pickle,
            SequenceNumber = 2,
            OutsourceVendor = "委外厂B",
            SendOutDate = DateTime.Today,
            SendQuantity = 20,
            SendWeight = 2000m,
            Status = SectionOutsourceStatus.PendingRecovery
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "ProcessName", Operator = "contains", Value = "酸洗" }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].ProcessName.Should().Be("酸洗");
    }

    [Fact]
    public async Task GetPagedAsync_Filters_OutsourceVendor_In_返回匹配()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-VENDOR");
        await SeedOutsourceAsync(ctx, batch.Id, vendor: "委外厂A");
        await SeedOutsourceAsync(ctx, batch.Id, vendor: "委外厂B");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "OutsourceVendor", Operator = "in", Values = new List<string> { "委外厂A" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].OutsourceVendor.Should().Be("委外厂A");
    }

    [Fact]
    public async Task GetPagedAsync_Filters_BatchNo_In_返回匹配()
    {
        var ctx = CreateDbContext();
        var batchA = await SeedBatchAsync(ctx, "BATCH-FLTR-A");
        var batchB = await SeedBatchAsync(ctx, "BATCH-FLTR-B");
        await SeedOutsourceAsync(ctx, batchA.Id, vendor: "委外厂A");
        await SeedOutsourceAsync(ctx, batchB.Id, vendor: "委外厂B");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "BatchNo", Operator = "in", Values = new List<string> { "BATCH-FLTR-A" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].BatchNo.Should().Be("BATCH-FLTR-A");
    }

    [Fact]
    public async Task GetPagedAsync_Filters_NoMatch_返回空列表()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-NOMATCH");
        await SeedOutsourceAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
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
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-CTX");
        await SeedOutsourceAsync(ctx, batch.Id, vendor: "委外厂A");
        ctx.SectionOutsources.Add(new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessName = "酸洗",
            ManufacturingSpec = "273*10",
            SectionName = SectionKeys.Pickle,
            SequenceNumber = 2,
            OutsourceVendor = "委外厂B",
            SendOutDate = DateTime.Today,
            SendQuantity = 20,
            SendWeight = 2000m,
            Status = SectionOutsourceStatus.PendingRecovery
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("ProcessName");
        contexts["ProcessName"].Should().Contain(new[] { "60冷轧", "酸洗" });
        contexts.Should().ContainKey("OutsourceVendor");
        contexts["OutsourceVendor"].Should().Contain(new[] { "委外厂A", "委外厂B" });
        contexts.Should().ContainKey("BatchNo");
        contexts["BatchNo"].Should().Contain("BATCH-CTX");
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["ProcessName"].Should().BeEmpty();
        contexts["OutsourceVendor"].Should().BeEmpty();
        contexts["BatchNo"].Should().BeEmpty();
    }

    [Fact]
    public async Task GetFilterContextsAsync_计算字段排除null()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-NULL");
        ctx.SectionOutsources.Add(new SectionOutsource
        {
            ProductionBatchId = batch.Id,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            SequenceNumber = 1,
            OutsourceVendor = "委外厂A",
            SendOutDate = DateTime.Today,
            SendQuantity = 10,
            SendWeight = 1000m,
            Status = SectionOutsourceStatus.PendingRecovery,
            ExpectedReturnDate = null
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["ExpectedReturnDate"].Should().BeEmpty();
    }

    // ========== 厂内（虚拟发外） ==========

    private async Task SeedColdRollDrawProcessGroupAsync(AppDbContext ctx, int batchId)
    {
        ctx.ProcessGroups.Add(new ProcessGroup
        {
            ProductionBatchId = batchId,
            SequenceNumber = 1,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            ColdRollDraw = 1
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAsync_厂内_非冷轧拔工段_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedColdRollDrawProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateSectionOutsourceRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Pickle,
            IsInternal = true,
            OutsourceVendor = "一车间",
            SendOutDate = DateTime.Today,
            SendWeight = 1000m
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*厂内*冷轧拔*");
    }

    [Fact]
    public async Task CreateAsync_厂内_冷轧拔_状态为略()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedColdRollDrawProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateSectionOutsourceRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            IsInternal = true,
            OutsourceVendor = "一车间",
            SendOutDate = DateTime.Today,
            SendQuantity = 10,
            SendWeight = 1000m
        });

        result.IsInternal.Should().BeTrue();
        result.Status.Should().Be(SectionOutsourceStatus.Virtual);
        var db = await ctx.SectionOutsources.SingleAsync();
        db.IsInternal.Should().BeTrue();
        db.Status.Should().Be(SectionOutsourceStatus.Virtual);
    }

    [Fact]
    public async Task BatchCreateAsync_厂内_非冷轧拔工段_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        await SeedBatchAsync(ctx, "BATCH001");
        var svc = CreateService(ctx);

        var act = () => svc.BatchCreateAsync(new List<CreateSectionOutsourceRequest>
        {
            new() { BatchNo = "BATCH001", ProcessName = "60冷轧", ManufacturingSpec = "219*8",
                SectionName = SectionKeys.Pickle, IsInternal = true, OutsourceVendor = "一车间",
                OutsourceSpec = "219*8", SendOutDate = DateTime.Today }
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*厂内*冷轧拔*");
    }

    [Fact]
    public async Task BatchCreateAsync_厂内_冷轧拔_状态为略()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH001");
        await SeedColdRollDrawProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        var result = await svc.BatchCreateAsync(new List<CreateSectionOutsourceRequest>
        {
            new() { BatchNo = "BATCH001", ProcessName = "60冷轧", ManufacturingSpec = "219*8",
                SectionName = SectionKeys.ColdRollDraw, IsInternal = true, OutsourceVendor = "一车间",
                OutsourceSpec = "219*8", SendOutDate = DateTime.Today }
        });

        result.Should().HaveCount(1);
        result[0].IsInternal.Should().BeTrue();
        result[0].Status.Should().Be(SectionOutsourceStatus.Virtual);
    }

    [Fact]
    public async Task CreateRecoveryAsync_厂内_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var outsource = await SeedOutsourceAsync(ctx, batch.Id, isInternal: true);
        var svc = CreateService(ctx);

        var act = () => svc.CreateRecoveryAsync(new CreateOutsourceRecoveryRequest
        {
            SectionOutsourceId = outsource.Id,
            RecoveryDate = DateTime.Today,
            RecoveryWeight = 800m
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*厂内*");
    }

    [Fact]
    public async Task BatchCreateRecoveriesAsync_厂内_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var outsource = await SeedOutsourceAsync(ctx, batch.Id, isInternal: true);
        var svc = CreateService(ctx);

        var act = () => svc.BatchCreateRecoveriesAsync(new List<CreateOutsourceRecoveryRequest>
        {
            new() { SectionOutsourceId = outsource.Id, RecoveryDate = DateTime.Today, RecoveryWeight = 800m }
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*厂内*");
    }

    [Fact]
    public async Task GetPagedAsync_厂内_IsInternal投影正确()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx, "BATCH-INTERNAL");
        await SeedOutsourceAsync(ctx, batch.Id, vendor: "一车间", status: SectionOutsourceStatus.Virtual, isInternal: true);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().HaveCount(1);
        result.Items[0].IsInternal.Should().BeTrue();
        result.Items[0].Status.Should().Be(SectionOutsourceStatus.Virtual);
    }

    [Fact]
    public async Task UpdateAsync_改厂内_非冷轧拔工段_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var outsource = await SeedOutsourceAsync(ctx, batch.Id);
        outsource.SectionName = SectionKeys.Pickle;
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(outsource.Id, new UpdateSectionOutsourceRequest { IsInternal = true });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*厂内*冷轧拔*");
    }

    [Fact]
    public async Task UpdateAsync_改厂内_冷轧拔_状态为略()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var outsource = await SeedOutsourceAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(outsource.Id, new UpdateSectionOutsourceRequest { IsInternal = true });

        result.IsInternal.Should().BeTrue();
        result.Status.Should().Be(SectionOutsourceStatus.Virtual);
        var db = await ctx.SectionOutsources.SingleAsync();
        db.IsInternal.Should().BeTrue();
        db.Status.Should().Be(SectionOutsourceStatus.Virtual);
    }

    [Fact]
    public async Task UpdateAsync_改厂内_已有回收记录_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var outsource = await SeedOutsourceAsync(ctx, batch.Id);
        ctx.OutsourceRecoveries.Add(new OutsourceRecovery
        {
            SectionOutsourceId = outsource.Id,
            RecoveryDate = DateTime.Today,
            RecoveryWeight = 800m
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(outsource.Id, new UpdateSectionOutsourceRequest { IsInternal = true });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*已有回收记录*厂内*");
    }

    [Fact]
    public async Task UpdateAsync_厂内改回真委外_状态为待回收()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var outsource = await SeedOutsourceAsync(ctx, batch.Id, status: SectionOutsourceStatus.Virtual, isInternal: true);
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(outsource.Id, new UpdateSectionOutsourceRequest { IsInternal = false });

        result.IsInternal.Should().BeFalse();
        result.Status.Should().Be(SectionOutsourceStatus.PendingRecovery);
        var db = await ctx.SectionOutsources.SingleAsync();
        db.IsInternal.Should().BeFalse();
        db.Status.Should().Be(SectionOutsourceStatus.PendingRecovery);
    }
}
