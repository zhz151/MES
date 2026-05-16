using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services;
using MES.Tests.Tests;

namespace MES.Tests.Services;

public class InspectionRecordServiceTests : TestBase
{
    private InspectionRecordService CreateService(AppDbContext ctx)
        => new(ctx);

    private async Task<Equipment> SeedEquipmentAsync(AppDbContext ctx,
        string name = "测试设备", string code = "EQ001", string location = "车间A")
    {
        var eq = new Equipment
        {
            EquipmentName = name,
            EquipmentCode = code,
            Location = location,
            LifecycleStatus = "Active",
            UsageType = "Primary"
        };
        ctx.Equipment.Add(eq);
        await ctx.SaveChangesAsync();
        return eq;
    }

    // ========== GetPagedAsync ==========

    [Fact]
    public async Task GetPagedAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new InspectionRecordQueryParams
        { PageIndex = 1, PageSize = 20 });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索记录编号_返回匹配()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedInspectionRecordAsync(ctx, eq.Id, "DJ-001");
        await SeedInspectionRecordAsync(ctx, eq.Id, "DJ-002");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new InspectionRecordQueryParams
        { PageIndex = 1, PageSize = 20, Keyword = "DJ-001" });

        result.Items.Should().HaveCount(1);
        result.Items[0].RecordNo.Should().Be("DJ-001");
    }

    [Fact]
    public async Task GetPagedAsync_关键词无匹配_返回空列表()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedInspectionRecordAsync(ctx, eq.Id, "DJ-001");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new InspectionRecordQueryParams
        { PageIndex = 1, PageSize = 20, Keyword = "NONEXISTENT" });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_按记录编号排序_成功()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedInspectionRecordAsync(ctx, eq.Id, "B-001");
        await SeedInspectionRecordAsync(ctx, eq.Id, "A-001");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new InspectionRecordQueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "recordno", IsDescending = false });

        result.Items[0].RecordNo.Should().Be("A-001");
        result.Items[1].RecordNo.Should().Be("B-001");
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedInspectionRecordAsync(ctx, eq.Id, "DJ-001");
        var id = await ctx.InspectionRecords.Select(r => r.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(id);

        result.Should().NotBeNull();
        result!.RecordNo.Should().Be("DJ-001");
        result.EquipmentName.Should().Be(eq.EquipmentName);
    }

    [Fact]
    public async Task GetByIdAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetByIdAsync(999);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== CreateAsync ==========

    [Fact]
    public async Task CreateAsync_创建成功()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateInspectionRecordRequest
        {
            EquipmentId = eq.Id,
            ActualDate = DateTime.Today,
            Inspector = "测试员",
            ExecutionSummary = "点检正常",
            Remark = "备注"
        });

        result.Should().NotBeNull();
        result.RecordNo.Should().StartWith("DJ-");
        result.Inspector.Should().Be("测试员");
        result.ExecutionSummary.Should().Be("点检正常");
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_更新成功()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedInspectionRecordAsync(ctx, eq.Id, "DJ-001");
        var id = await ctx.InspectionRecords.Select(r => r.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateInspectionRequest
        {
            Inspector = "新点检员",
            ExecutionSummary = "更新摘要"
        });

        result.Inspector.Should().Be("新点检员");
        result.ExecutionSummary.Should().Be("更新摘要");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_删除成功()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedInspectionRecordAsync(ctx, eq.Id, "DJ-001");
        var id = await ctx.InspectionRecords.Select(r => r.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var deleted = await ctx.InspectionRecords.FindAsync(id);
        deleted.Should().BeNull();
    }

    // ========== Helpers ==========

    private async Task SeedInspectionRecordAsync(AppDbContext ctx, int equipmentId, string recordNo)
    {
        ctx.InspectionRecords.Add(new InspectionRecord
        {
            RecordNo = recordNo,
            EquipmentId = equipmentId,
            ActualDate = DateTime.Today,
            Inspector = "测试员",
            ExecutionSummary = "点检正常"
        });
        await ctx.SaveChangesAsync();
    }
}
