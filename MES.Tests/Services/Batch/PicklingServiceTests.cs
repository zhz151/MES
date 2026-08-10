using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
using MES.Core.Models;
using MES.Services;
using MES.Services.Batch;
using MES.Tests.Tests;


using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Batch;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace MES.Tests.Services;

/// <summary>
/// 去油/酸洗服务测试：入缸记录 CRUD、完工记录 CRUD、筛选上下文
/// </summary>
public class PicklingServiceTests : TestBase
{
    private PicklingService CreateService(AppDbContext ctx)
    {
        var prMock = new Mock<Core.Interfaces.Batch.IProductionRecordService>();
        return new(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<PicklingService>.Instance, new MemoryCache(new MemoryCacheOptions()), prMock.Object, Mock.Of<Core.Interfaces.Configuration.ISectionNameDisplayService>(), CreateProcessDefinitionServiceMock());
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
            TagNo = "TAG001",
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

    private async Task<ProcessGroup> SeedProcessGroupAsync(AppDbContext ctx, int batchId,
        string processName = "冷拔", string manufacturingSpec = "219*8")
    {
        var pg = new ProcessGroup
        {
            ProductionBatchId = batchId,
            ProcessName = processName,
            ManufacturingSpec = manufacturingSpec,
            SequenceNumber = 1,
            Pickle = 1  // 酸洗工段序号
        };
        ctx.ProcessGroups.Add(pg);
        await ctx.SaveChangesAsync();
        return pg;
    }

    private async Task<PicklingInRecord> SeedInRecordAsync(AppDbContext ctx,
        string batchNo = "BATCH001", string sectionName = SectionKeys.Pickle)
    {
        var batch = await ctx.ProductionBatches.FirstOrDefaultAsync(b => b.BatchNo == batchNo);
        if (batch == null) batch = await SeedBatchAsync(ctx, batchNo);

        var pg = await SeedProcessGroupAsync(ctx, batch.Id);

        var record = new PicklingInRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = pg.ProcessName,
            ManufacturingSpec = pg.ManufacturingSpec,
            SectionName = sectionName,
            SequenceNumber = 1,
            InDate = DateTime.Today,
            Status = PicklingStatus.Soaking,
            Quantity = 10,
            Weight = 1000m
        };
        ctx.PicklingInRecords.Add(record);
        await ctx.SaveChangesAsync();
        return record;
    }

    // ========== 入缸记录 — GetPagedAsync ==========

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
        await SeedInRecordAsync(ctx, "BATCH001");
        await SeedInRecordAsync(ctx, "BATCH002");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "BATCH001" });

        result.Items.Should().HaveCount(1);
        result.Items[0].BatchNo.Should().Be("BATCH001");
    }

    // ========== 入缸记录 — CreateAsync ==========

    [Fact]
    public async Task CreateAsync_成功创建()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        var pg = await SeedProcessGroupAsync(ctx, batch.Id);

        // 先创建冷轧拔生产记录（酸洗的前置条件：冷拔工序必须先有冷轧拔工段）
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProcessGroupId = pg.Id,
            ProcessName = "冷拔",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.ColdRollDraw,
            SequenceNumber = 1,
            ExecDate = DateTime.Today,
            Quantity = 20,
            Weight = 2000m
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreatePicklingInRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "冷拔",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Pickle,
            InDate = DateTime.Today,
            Quantity = 20,
            Weight = 2000m
        });

        result.Should().NotBeNull();
        result.BatchNo.Should().Be("BATCH001");
        result.Quantity.Should().Be(20);

        var saved = await ctx.PicklingInRecords.FirstAsync();
        saved.Status.Should().Be(PicklingStatus.Soaking);
    }

    [Fact]
    public async Task CreateAsync_批次不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreatePicklingInRecordRequest
        {
            BatchNo = "NONEXISTENT",
            ProcessName = "冷拔",
            ManufacturingSpec = "219*8",
            SectionName = SectionKeys.Pickle,
            InDate = DateTime.Today
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task CreateAsync_工段不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var batch = await SeedBatchAsync(ctx);
        await SeedProcessGroupAsync(ctx, batch.Id);
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreatePicklingInRecordRequest
        {
            BatchNo = "BATCH001",
            ProcessName = "冷拔",
            ManufacturingSpec = "219*8",
            SectionName = "不存在的工段",
            InDate = DateTime.Today
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== 入缸记录 — UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_成功更新()
    {
        var ctx = CreateDbContext();
        var record = await SeedInRecordAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(record.Id, new UpdatePicklingInRecordRequest
        {
            Quantity = 30,
            Weight = 3000m,
            Remark = "更新备注"
        });

        result.Quantity.Should().Be(30);
        result.Weight.Should().Be(3000m);
        result.Remark.Should().Be("更新备注");
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdatePicklingInRecordRequest { InDate = DateTime.Today });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task UpdateAsync_入缸变更字段_级联同步到出缸快照()
    {
        var ctx = CreateDbContext();
        var record = await SeedInRecordAsync(ctx);
        // 出缸记录冗余字段为空（设备/支数/重量待入缸变更同步）
        ctx.PicklingOutRecords.Add(new PicklingOutRecord
        {
            PicklingInRecordId = record.Id,
            CompleteDate = DateTime.Today,
            ProductionBatchId = record.ProductionBatchId,
            SectionName = record.SectionName
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        // 入缸变更：设备/支数/重量
        await svc.UpdateAsync(record.Id, new UpdatePicklingInRecordRequest
        {
            EquipmentName = "酸洗槽2",
            Quantity = 30,
            Weight = 3000m
        });

        var outRec = await ctx.PicklingOutRecords.AsNoTracking().FirstAsync();
        outRec.EquipmentName.Should().Be("酸洗槽2"); // 入缸变更字段跟随
        outRec.Quantity.Should().Be(30);
        outRec.Weight.Should().Be(3000m);
    }

    [Fact]
    public async Task UpdateAsync_入缸未变更字段_出缸保持原值()
    {
        var ctx = CreateDbContext();
        var record = await SeedInRecordAsync(ctx);
        // 出缸记录已有部分快照值
        ctx.PicklingOutRecords.Add(new PicklingOutRecord
        {
            PicklingInRecordId = record.Id,
            CompleteDate = DateTime.Today,
            ProductionBatchId = record.ProductionBatchId,
            SectionName = record.SectionName,
            Quantity = 88,
            EquipmentName = "老设备"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        // 仅变更重量，未改设备/支数
        await svc.UpdateAsync(record.Id, new UpdatePicklingInRecordRequest
        {
            Weight = 5000m
        });

        var outRec = await ctx.PicklingOutRecords.AsNoTracking().FirstAsync();
        outRec.Weight.Should().Be(5000m);            // 入缸变更字段跟随
        outRec.Quantity.Should().Be(88);             // 未变更字段保持出缸原值
        outRec.EquipmentName.Should().Be("老设备");   // 未变更字段保持出缸原值
    }

    // ========== 入缸记录 — DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var record = await SeedInRecordAsync(ctx);
        var svc = CreateService(ctx);

        await svc.DeleteAsync(record.Id);

        var deleted = await ctx.PicklingInRecords.FindAsync(record.Id);
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

    [Fact]
    public async Task DeleteAsync_已有完工_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var record = await SeedInRecordAsync(ctx);
        ctx.PicklingOutRecords.Add(new PicklingOutRecord
        {
            PicklingInRecordId = record.Id,
            CompleteDate = DateTime.Today,
            ProductionBatchId = record.ProductionBatchId,
            SectionName = record.SectionName
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteAsync(record.Id);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*完工*");
    }

    // ========== 完工记录 ==========

    [Fact]
    public async Task GetOutRecordByInIdAsync_存在_返回记录()
    {
        var ctx = CreateDbContext();
        var record = await SeedInRecordAsync(ctx);
        ctx.PicklingOutRecords.Add(new PicklingOutRecord
        {
            PicklingInRecordId = record.Id,
            CompleteDate = DateTime.Today,
            ProductionBatchId = record.ProductionBatchId,
            SectionName = record.SectionName
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetOutRecordByInIdAsync(record.Id);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetOutRecordByInIdAsync_不存在_返回Null()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetOutRecordByInIdAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOutRecordsPagedAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetOutRecordsPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateOutRecordAsync_成功创建()
    {
        var ctx = CreateDbContext();
        var record = await SeedInRecordAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateOutRecordAsync(new CreatePicklingOutRecordRequest
        {
            PicklingInRecordId = record.Id,
            CompleteDate = DateTime.Today
        });

        result.Should().NotBeNull();
        result.PicklingInRecordId.Should().Be(record.Id);

        // 入缸状态应更新为 Completed
        var inRecord = await ctx.PicklingInRecords.FindAsync(record.Id);
        inRecord!.Status.Should().Be(PicklingStatus.Completed);
    }

    [Fact]
    public async Task CreateOutRecordAsync_已完工_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var record = await SeedInRecordAsync(ctx);
        record.Status = PicklingStatus.Completed;
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var act = () => svc.CreateOutRecordAsync(new CreatePicklingOutRecordRequest
        {
            PicklingInRecordId = record.Id,
            CompleteDate = DateTime.Today
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*已完工*");
    }

    [Fact]
    public async Task UpdateOutRecordAsync_成功更新()
    {
        var ctx = CreateDbContext();
        var record = await SeedInRecordAsync(ctx);
        var outRecord = new PicklingOutRecord
        {
            PicklingInRecordId = record.Id,
            CompleteDate = DateTime.Today,
            ProductionBatchId = record.ProductionBatchId,
            SectionName = record.SectionName
        };
        ctx.PicklingOutRecords.Add(outRecord);
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateOutRecordAsync(outRecord.Id, new UpdatePicklingOutRecordRequest
        {
            Remark = "更新完工备注"
        });

        result.Remark.Should().Be("更新完工备注");
    }

    [Fact]
    public async Task DeleteOutRecordAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var record = await SeedInRecordAsync(ctx);
        var outRecord = new PicklingOutRecord
        {
            PicklingInRecordId = record.Id,
            CompleteDate = DateTime.Today,
            ProductionBatchId = record.ProductionBatchId,
            SectionName = record.SectionName
        };
        ctx.PicklingOutRecords.Add(outRecord);
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        await svc.DeleteOutRecordAsync(outRecord.Id);

        var deleted = await ctx.PicklingOutRecords.FindAsync(outRecord.Id);
        deleted.Should().BeNull();

        // 入缸状态应恢复为 Soaking
        var inRecord = await ctx.PicklingInRecords.FindAsync(record.Id);
        inRecord!.Status.Should().Be(PicklingStatus.Soaking);
    }

    // ========== 回填入缸冗余字段 ==========

    [Fact]
    public async Task BackfillOutRecordInDataAsync_补齐空冗余字段_批次号取自批次()
    {
        var ctx = CreateDbContext();
        var inRec = await SeedInRecordAsync(ctx, "BATCH001");
        // 补齐入缸记录其余可回填字段
        inRec.TagNo = "TAG-9";
        inRec.PlantGrade = "304";
        inRec.Operator = "王五";
        inRec.Shift = nameof(ShiftType.DayShift);
        inRec.EquipmentName = "酸洗槽1";
        inRec.ProductStatus = "InProcess";
        await ctx.SaveChangesAsync();

        // 出缸记录冗余字段全空（SectionName 为必填列，保持入缸值）
        ctx.PicklingOutRecords.Add(new PicklingOutRecord
        {
            PicklingInRecordId = inRec.Id,
            CompleteDate = DateTime.Today,
            DataSource = "MANUAL",
            ProductionBatchId = inRec.ProductionBatchId,
            SectionName = inRec.SectionName
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var updated = await svc.BackfillOutRecordInDataAsync();

        updated.Should().Be(1);
        var outRec = await ctx.PicklingOutRecords.AsNoTracking().FirstAsync();
        outRec.BatchNo.Should().Be("BATCH001");            // 批次号取自批次导航
        outRec.ProcessName.Should().Be("冷拔");
        outRec.ManufacturingSpec.Should().Be("219*8");
        outRec.SectionName.Should().Be(SectionKeys.Pickle);
        outRec.TagNo.Should().Be("TAG-9");
        outRec.PlantGrade.Should().Be("304");
        outRec.EquipmentName.Should().Be("酸洗槽1");
        outRec.Operator.Should().Be("王五");
        outRec.Shift.Should().Be(nameof(ShiftType.DayShift));
        outRec.Quantity.Should().Be(10);
        outRec.Weight.Should().Be(1000m);
        outRec.ProductStatus.Should().Be("InProcess");
    }

    [Fact]
    public async Task BackfillOutRecordInDataAsync_已有值不覆盖()
    {
        var ctx = CreateDbContext();
        var inRec = await SeedInRecordAsync(ctx, "BATCH002");
        // 一条已有部分值（应保持不动），一条全空（应补齐，SectionName 为必填列保持入缸值）
        ctx.PicklingOutRecords.Add(new PicklingOutRecord
        {
            PicklingInRecordId = inRec.Id,
            CompleteDate = DateTime.Today,
            DataSource = "MANUAL",
            ProductionBatchId = inRec.ProductionBatchId,
            SectionName = inRec.SectionName,
            BatchNo = "已填批次号",
            Quantity = 99
        });
        ctx.PicklingOutRecords.Add(new PicklingOutRecord
        {
            PicklingInRecordId = inRec.Id,
            CompleteDate = DateTime.Today,
            DataSource = "MANUAL",
            ProductionBatchId = inRec.ProductionBatchId,
            SectionName = inRec.SectionName
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var updated = await svc.BackfillOutRecordInDataAsync();

        updated.Should().Be(2); // 两条记录的其余空字段均被补齐
        var outRecs = await ctx.PicklingOutRecords.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
        // 第一条：已填字段不被覆盖，空字段被补齐
        outRecs[0].BatchNo.Should().Be("已填批次号");
        outRecs[0].Quantity.Should().Be(99);
        outRecs[0].ProcessName.Should().Be("冷拔");
        outRecs[0].Weight.Should().Be(1000m);
        // 第二条：全部空字段被补齐
        outRecs[1].BatchNo.Should().Be("BATCH002");
        outRecs[1].Quantity.Should().Be(10);
        outRecs[1].ProcessName.Should().Be("冷拔");
    }

    // ========== GetByBatchAsync ==========

    [Fact]
    public async Task GetByBatchAsync_返回对应记录()
    {
        var ctx = CreateDbContext();
        await SeedInRecordAsync(ctx, "BATCH001");
        await SeedInRecordAsync(ctx, "BATCH002");
        var svc = CreateService(ctx);

        var result = await svc.GetByBatchAsync("BATCH001");

        result.Should().HaveCount(1);
        result[0].BatchNo.Should().Be("BATCH001");
    }

    // ========== 筛选上下文 ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        await SeedInRecordAsync(ctx, "BATCH001");
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("ProcessName");
        contexts.Should().ContainKey("SectionName");
    }

    [Fact]
    public async Task GetOutRecordFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        var record = await SeedInRecordAsync(ctx);
        ctx.PicklingOutRecords.Add(new PicklingOutRecord
        {
            PicklingInRecordId = record.Id,
            CompleteDate = DateTime.Today,
            ProductionBatchId = record.ProductionBatchId,
            SectionName = record.SectionName,
            EquipmentName = "设备A"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetOutRecordFilterContextsAsync();

        contexts.Should().ContainKey("EquipmentName");
        contexts["EquipmentName"].Should().Contain("设备A");
    }
}
