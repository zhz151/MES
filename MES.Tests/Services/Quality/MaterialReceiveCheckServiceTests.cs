using FluentAssertions;
using Microsoft.Extensions.Logging;
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
using MES.Services.Quality;
using MES.Tests.Tests;
using Moq;


using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Batch;
using MES.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Tests.Services.Quality;

/// <summary>
/// 检验到料（成检到料）服务测试
/// </summary>
public class MaterialReceiveCheckServiceTests : TestBase
{
    private MaterialReceiveCheckService CreateService(AppDbContext ctx)
    {
        var qptMock = new Mock<IQualityProcessTrackingService>();
        var wesMock = new Mock<IWorkOrderExecutionService>();
        var prMock = new Mock<IProductionRecordService>();
        return new(ctx, qptMock.Object, wesMock.Object, prMock.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MaterialReceiveCheckService>.Instance, new MemoryCache(new MemoryCacheOptions()));
    }

    private async Task<ProductionBatch> SeedBatchAsync(AppDbContext ctx, string batchNo = "BATCH001")
    {
        var batch = new ProductionBatch
        {
            BatchNo = batchNo,
            WorkOrderNo = "WO001",
            SalesOrderNo = "SO001",
            ProductionMainNo = "D01",
            OrderItemIds = "[]",
            MaterialName = "不锈钢管",
            PlantGrade = "304",
            Specification = "219*8",
            Status = BatchStatus.InProgress,
            ProductionType = "InProcess",
            ManufacturingItem = "OrderFinished",
            CurrentValidQty = 100,
            CurrentValidWeight = 5000,
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

        // 新增：添加一个检验工序组（成检到料需要 ProcessGroup）
        var pg = new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            SequenceNumber = 1,
            ProcessName = "检验",
            ManufacturingSpec = batch.Specification,
            Inspection = 1
        };
        ctx.Set<ProcessGroup>().Add(pg);
        await ctx.SaveChangesAsync();

        return batch;
    }

    // ========== 检验到料 ==========

    [Fact]
    public async Task CreateMaterialReceiveCheckAsync_成功创建()
    {
        var ctx = CreateDbContext();
        await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateMaterialReceiveCheckAsync(new CreateMaterialReceiveCheckRequest
        {
            BatchNo = "BATCH001",
            ReceiveDate = DateTime.Today
        });

        result.Should().NotBeNull();
        result.BatchNo.Should().Be("BATCH001");
    }

    [Fact]
    public async Task UpdateMaterialReceiveCheckAsync_工序组变更后保存_重算推导值()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        // 创建到料：唯一检验工序组(Inspection=1) → 正式成检
        var created = await svc.CreateMaterialReceiveCheckAsync(new CreateMaterialReceiveCheckRequest
        {
            BatchNo = "BATCH001",
            ReceiveDate = DateTime.Today
        });
        created.InspectionType.Should().Be(MES.Core.Enums.InspectionType.FormalInspection);

        // 工艺卡变更：① 新增更深检验工序组(Inspection=2)，原工序组不再是最后检验节点
        ctx.Set<ProcessGroup>().Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            SequenceNumber = 2,
            ProcessName = "终检",
            ManufacturingSpec = batch.Specification,
            Inspection = 2
        });
        // ② 原工序组的工序名称变更
        var pg1 = await ctx.Set<ProcessGroup>().FirstAsync(pg => pg.BatchNo == batch.BatchNo && pg.SequenceNumber == 1);
        pg1.ProcessName = "外观检验";
        await ctx.SaveChangesAsync();

        // 编辑保存（人为字段未改）→ 推导值按当前工艺卡重新计算
        var result = await svc.UpdateMaterialReceiveCheckAsync(created.Id, new UpdateMaterialReceiveCheckRequest
        {
            ReceiveDate = DateTime.Today
        });

        result.InspectionType.Should().Be(MES.Core.Enums.InspectionType.PreInspection);
        result.ProcessName.Should().Be("外观检验"); // 工序冗余字段已从工序组刷新
        result.SequenceNumber.Should().Be(1); // 执行序=检验深度 Inspection=1，而非工序组执行顺序
    }

    [Fact]
    public async Task UpdateMaterialReceiveCheckAsync_重选工序组_联动更新推导值()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        // 创建到料：唯一检验工序组(Inspection=1) → 正式成检
        var created = await svc.CreateMaterialReceiveCheckAsync(new CreateMaterialReceiveCheckRequest
        {
            BatchNo = "BATCH001",
            ReceiveDate = DateTime.Today
        });
        created.InspectionType.Should().Be(MES.Core.Enums.InspectionType.FormalInspection);

        // 新增更深检验工序组(Inspection=2)「终检」（执行顺序 SequenceNumber=5 与检验深度 Inspection=2 刻意不同，
        // 用于验证「执行序」取的是检验深度 Inspection 而非工序组执行顺序），原工序组降级为非最后节点
        ctx.Set<ProcessGroup>().Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            SequenceNumber = 5,
            ProcessName = "终检",
            ManufacturingSpec = batch.Specification,
            Inspection = 2
        });
        // 新增非检验工序组（应被拒绝重选）
        ctx.Set<ProcessGroup>().Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            SequenceNumber = 3,
            ProcessName = "冷拔",
            ManufacturingSpec = batch.Specification,
            Inspection = null
        });
        await ctx.SaveChangesAsync();

        var finalPg = await ctx.Set<ProcessGroup>().FirstAsync(pg => pg.BatchNo == batch.BatchNo && pg.ProcessName == "终检");

        // 重选到「终检」→ 工序名称/执行序/成检类型联动更新
        var result = await svc.UpdateMaterialReceiveCheckAsync(created.Id, new UpdateMaterialReceiveCheckRequest
        {
            ReceiveDate = DateTime.Today,
            ProcessGroupId = finalPg.Id
        });

        result.ProcessGroupId.Should().Be(finalPg.Id);
        result.ProcessName.Should().Be("终检");
        result.SequenceNumber.Should().Be(finalPg.Inspection!.Value); // 执行序=检验深度 Inspection，而非工序组执行顺序
        result.InspectionType.Should().Be(MES.Core.Enums.InspectionType.FormalInspection); // 终检为最深检验节点

        // 重选到非检验工序组 → 拒绝
        var nonInspPg = await ctx.Set<ProcessGroup>().FirstAsync(pg => pg.BatchNo == batch.BatchNo && pg.ProcessName == "冷拔");
        var act = () => svc.UpdateMaterialReceiveCheckAsync(created.Id, new UpdateMaterialReceiveCheckRequest
        {
            ReceiveDate = DateTime.Today,
            ProcessGroupId = nonInspPg.Id
        });
        await act.Should().ThrowAsync<MES.Core.Exceptions.BusinessException>();
    }

    [Fact]
    public async Task GetAllMaterialReceiveChecksAsync_工艺卡新增更深检验节点_标记成检类型过期()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        // 创建到料：唯一检验工序组(Inspection=1) → 正式成检
        await svc.CreateMaterialReceiveCheckAsync(new CreateMaterialReceiveCheckRequest
        {
            BatchNo = "BATCH001",
            ReceiveDate = DateTime.Today
        });

        // 工艺卡新增更深检验节点(Inspection=2)，原到料的存储成检类型不再符合实时判定
        ctx.Set<ProcessGroup>().Add(new ProcessGroup
        {
            ProductionBatchId = batch.Id,
            BatchNo = batch.BatchNo,
            SequenceNumber = 2,
            ProcessName = "终检",
            ManufacturingSpec = batch.Specification,
            Inspection = 2
        });
        await ctx.SaveChangesAsync();

        var paged = await svc.GetAllMaterialReceiveChecksAsync(new QueryParams { PageIndex = 1, PageSize = 10 });
        var item = paged.Items.Should().ContainSingle().Subject;
        item.HealthIssue.Should().Be("成检类型过期");
    }

    [Fact]
    public async Task GetAllMaterialReceiveChecksAsync_关联工序组被降级非检验_标记工序组非检验()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        // 创建到料（正式成检）
        await svc.CreateMaterialReceiveCheckAsync(new CreateMaterialReceiveCheckRequest
        {
            BatchNo = "BATCH001",
            ReceiveDate = DateTime.Today
        });

        // 工艺卡变更：原检验工序组的 Inspection 被清空，不再是检验工序组
        var pg = await ctx.Set<ProcessGroup>().FirstAsync(p => p.BatchNo == batch.BatchNo);
        pg.Inspection = null;
        await ctx.SaveChangesAsync();

        var paged = await svc.GetAllMaterialReceiveChecksAsync(new QueryParams { PageIndex = 1, PageSize = 10 });
        var item = paged.Items.Should().ContainSingle().Subject;
        item.HealthIssue.Should().Be("工序组非检验");
    }

    [Fact]
    public async Task GetMaterialCheckHealthSummaryAsync_统计异常分类()
    {
        var ctx = CreateDbContext();
        var batch1 = await SeedBatchAsync(ctx, "BATCH001");
        var batch2 = await SeedBatchAsync(ctx, "BATCH002");
        var svc = CreateService(ctx);

        // 两个批次各创建到料（均正式成检）
        await svc.CreateMaterialReceiveCheckAsync(new CreateMaterialReceiveCheckRequest { BatchNo = "BATCH001", ReceiveDate = DateTime.Today });
        await svc.CreateMaterialReceiveCheckAsync(new CreateMaterialReceiveCheckRequest { BatchNo = "BATCH002", ReceiveDate = DateTime.Today });

        // 批次1 新增更深检验节点(Inspection=2) → 批次1 到料成检类型过期
        ctx.Set<ProcessGroup>().Add(new ProcessGroup
        {
            ProductionBatchId = batch1.Id,
            BatchNo = batch1.BatchNo,
            SequenceNumber = 5,
            ProcessName = "终检",
            ManufacturingSpec = batch1.Specification,
            Inspection = 2
        });
        // 批次2 检验工序组 Inspection 清空 → 批次2 到料工序组非检验
        var pgOfBatch2 = await ctx.Set<ProcessGroup>().FirstAsync(p => p.BatchNo == "BATCH002");
        pgOfBatch2.Inspection = null;
        await ctx.SaveChangesAsync();

        var summary = await svc.GetMaterialCheckHealthSummaryAsync(new QueryParams { PageIndex = 1, PageSize = 10 });
        summary.TotalCount.Should().Be(2);
        // 异常分类列出具体生产编号
        summary.InspectionTypeExpiredBatchNos.Should().Contain("BATCH001");
        summary.ProcessGroupNotInspectionBatchNos.Should().Contain("BATCH002");
        summary.InspectionTypeExpiredCount.Should().Be(1);
        summary.ProcessGroupNotInspectionCount.Should().Be(1);
        summary.IssueCount.Should().Be(2);
    }

    [Fact]
    public async Task GetMaterialReceiveCheckAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        await svc.CreateMaterialReceiveCheckAsync(new CreateMaterialReceiveCheckRequest
        {
            BatchNo = "BATCH001",
            ReceiveDate = DateTime.Today
        });

        var result = await svc.GetMaterialReceiveCheckAsync(batch.Id);

        result.Should().NotBeNull();
        result!.BatchNo.Should().Be("BATCH001");
    }

    [Fact]
    public async Task GetMaterialReceiveCheckAsync_不存在_返回Null()
    {
        var ctx = CreateDbContext();
        await SeedBatchAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetMaterialReceiveCheckAsync(999);

        result.Should().BeNull();
    }
}
