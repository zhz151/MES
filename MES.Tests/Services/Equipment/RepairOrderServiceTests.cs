using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Equipment;
using MES.Tests.Tests;
using MES.Data.Entities.Equipment;
using MES.Core.DTOs.Equipment;

namespace MES.Tests.Services;

public class RepairOrderServiceTests : TestBase
{
    private RepairOrderService CreateService(AppDbContext ctx)
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

        var result = await svc.GetPagedAsync(new RepairOrderQueryParams
        { PageIndex = 1, PageSize = 20 });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索工单编号_返回匹配()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedRepairOrderAsync(ctx, eq.Id, "WX-001");
        await SeedRepairOrderAsync(ctx, eq.Id, "WX-002");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new RepairOrderQueryParams
        { PageIndex = 1, PageSize = 20, Keyword = "WX-001" });

        result.Items.Should().HaveCount(1);
        result.Items[0].RepairOrderNo.Should().Be("WX-001");
    }

    [Fact]
    public async Task GetPagedAsync_关键词无匹配_返回空列表()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedRepairOrderAsync(ctx, eq.Id, "WX-001");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new RepairOrderQueryParams
        { PageIndex = 1, PageSize = 20, Keyword = "NONEXISTENT" });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_按工单编号排序_成功()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedRepairOrderAsync(ctx, eq.Id, "B-001");
        await SeedRepairOrderAsync(ctx, eq.Id, "A-001");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new RepairOrderQueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "repairorderno", IsDescending = false });

        result.Items[0].RepairOrderNo.Should().Be("A-001");
        result.Items[1].RepairOrderNo.Should().Be("B-001");
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedRepairOrderAsync(ctx, eq.Id, "WX-001");
        var id = await ctx.RepairOrders.Select(r => r.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(id);

        result.Should().NotBeNull();
        result!.RepairOrderNo.Should().Be("WX-001");
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

    // ========== CreateBatchAsync ==========

    [Fact]
    public async Task CreateBatchAsync_批量创建成功()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        var svc = CreateService(ctx);

        var requests = new List<CreateRepairOrderRequest>
        {
            new() { EquipmentId = eq.Id, FaultDescription = "故障1", ReportPerson = "张三" },
            new() { EquipmentId = eq.Id, FaultDescription = "故障2", ReportPerson = "李四" }
        };

        var results = await svc.CreateBatchAsync(requests);

        results.Should().HaveCount(2);
        results[0].FaultDescription.Should().Be("故障1");
        results[1].FaultDescription.Should().Be("故障2");
        results[0].RepairOrderNo.Should().StartWith("WX-");
        results[1].RepairOrderNo.Should().StartWith("WX-");
    }

    [Fact]
    public async Task CreateBatchAsync_空列表_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var results = await svc.CreateBatchAsync(new List<CreateRepairOrderRequest>());

        results.Should().BeEmpty();
    }

    // ========== CreateAsync ==========

    [Fact]
    public async Task CreateAsync_创建成功()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateRepairOrderRequest
        {
            EquipmentId = eq.Id,
            FaultDescription = "设备异响",
            FaultType = "机械故障",
            ReportPerson = "王五",
            ReportTime = DateTime.Now,
            RepairPerson = "赵六",
            RepairContent = "更换轴承",
            SparePartUsed = "轴承SKF-6205"
        });

        result.Should().NotBeNull();
        result.RepairOrderNo.Should().StartWith("WX-");
        result.FaultDescription.Should().Be("设备异响");
        result.ReportPerson.Should().Be("王五");
        result.RepairStatus.Should().Be("Pending");
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_更新成功()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedRepairOrderAsync(ctx, eq.Id, "WX-001");
        var id = await ctx.RepairOrders.Select(r => r.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateRepairOrderRequest
        {
            RepairContent = "更换完毕，测试正常",
            RepairStartTime = DateTime.Now,
            RepairEndTime = DateTime.Now.AddHours(2)
        });

        result.RepairContent.Should().Be("更换完毕，测试正常");
        result.RepairStatus.Should().Be("Completed");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_删除成功()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedRepairOrderAsync(ctx, eq.Id, "WX-001");
        var id = await ctx.RepairOrders.Select(r => r.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var deleted = await ctx.RepairOrders.FindAsync(id);
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
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateRepairOrderRequest());

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*不存在*");
    }

    // ========== Helpers ==========

    private async Task SeedRepairOrderAsync(AppDbContext ctx, int equipmentId, string orderNo)
    {
        ctx.RepairOrders.Add(new RepairOrder
        {
            RepairOrderNo = orderNo,
            EquipmentId = equipmentId,
            FaultDescription = "设备异响",
            ReportPerson = "王五",
            ReportTime = DateTime.Now,
            Priority = "Normal",
            RepairStatus = "Pending"
        });
        await ctx.SaveChangesAsync();
    }
}
