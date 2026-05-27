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

public class MaintenanceOrderServiceTests : TestBase
{
    private MaintenanceOrderService CreateService(AppDbContext ctx)
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

        var result = await svc.GetPagedAsync(new MaintenanceOrderQueryParams
        { PageIndex = 1, PageSize = 20 });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索工单编号_返回匹配()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedMaintenanceOrderAsync(ctx, eq.Id, "BY-001");
        await SeedMaintenanceOrderAsync(ctx, eq.Id, "BY-002");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new MaintenanceOrderQueryParams
        { PageIndex = 1, PageSize = 20, Keyword = "BY-001" });

        result.Items.Should().HaveCount(1);
        result.Items[0].MaintOrderNo.Should().Be("BY-001");
    }

    [Fact]
    public async Task GetPagedAsync_关键词无匹配_返回空列表()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedMaintenanceOrderAsync(ctx, eq.Id, "BY-001");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new MaintenanceOrderQueryParams
        { PageIndex = 1, PageSize = 20, Keyword = "NONEXISTENT" });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_按工单编号排序_成功()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedMaintenanceOrderAsync(ctx, eq.Id, "B-001");
        await SeedMaintenanceOrderAsync(ctx, eq.Id, "A-001");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new MaintenanceOrderQueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "maintorderno", IsDescending = false });

        result.Items[0].MaintOrderNo.Should().Be("A-001");
        result.Items[1].MaintOrderNo.Should().Be("B-001");
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedMaintenanceOrderAsync(ctx, eq.Id, "BY-001");
        var id = await ctx.MaintenanceOrders.Select(m => m.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(id);

        result.Should().NotBeNull();
        result!.MaintOrderNo.Should().Be("BY-001");
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

        var result = await svc.CreateAsync(new CreateMaintenanceOrderRequest
        {
            EquipmentId = eq.Id,
            ActualDate = DateTime.Today,
            Executor = "张三",
            ExecutionSummary = "保养正常",
            Remark = "备注"
        });

        result.Should().NotBeNull();
        result.MaintOrderNo.Should().StartWith("BY-");
        result.Executor.Should().Be("张三");
        result.ExecutionSummary.Should().Be("保养正常");
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_更新成功()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedMaintenanceOrderAsync(ctx, eq.Id, "BY-001");
        var id = await ctx.MaintenanceOrders.Select(m => m.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateMaintenanceRequest
        {
            Executor = "李四",
            ExecutionSummary = "更新摘要"
        });

        result.Executor.Should().Be("李四");
        result.ExecutionSummary.Should().Be("更新摘要");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_删除成功()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedMaintenanceOrderAsync(ctx, eq.Id, "BY-001");
        var id = await ctx.MaintenanceOrders.Select(m => m.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var deleted = await ctx.MaintenanceOrders.FindAsync(id);
        deleted.Should().BeNull();
    }

    // ========== CreateBatchAsync ==========

    [Fact]
    public async Task CreateBatchAsync_批量创建成功()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        var svc = CreateService(ctx);

        var requests = new List<CreateMaintenanceOrderRequest>
        {
            new() { EquipmentId = eq.Id, Executor = "张三", ExecutionSummary = "保养1", ActualDate = DateTime.Today },
            new() { EquipmentId = eq.Id, Executor = "李四", ExecutionSummary = "保养2", ActualDate = DateTime.Today }
        };

        var results = await svc.CreateBatchAsync(requests);

        results.Should().HaveCount(2);
        results[0].Executor.Should().Be("张三");
        results[1].Executor.Should().Be("李四");
        results[0].MaintOrderNo.Should().StartWith("BY-");
        results[1].MaintOrderNo.Should().StartWith("BY-");
    }

    [Fact]
    public async Task CreateBatchAsync_空列表_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var results = await svc.CreateBatchAsync(new List<CreateMaintenanceOrderRequest>());

        results.Should().BeEmpty();
    }

    // ========== Helpers ==========

    private async Task SeedMaintenanceOrderAsync(AppDbContext ctx, int equipmentId, string orderNo)
    {
        ctx.MaintenanceOrders.Add(new MaintenanceOrder
        {
            MaintOrderNo = orderNo,
            EquipmentId = equipmentId,
            ActualDate = DateTime.Today,
            Executor = "张三",
            ExecutionSummary = "保养正常"
        });
        await ctx.SaveChangesAsync();
    }

    // ========== B10 专项测试 ==========

    [Fact]
    public async Task GetPagedAsync_按执行摘要排序_成功()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        ctx.MaintenanceOrders.AddRange(
            new MaintenanceOrder { MaintOrderNo = "BY-002", EquipmentId = eq.Id, ActualDate = DateTime.Today, Executor = "张三", ExecutionSummary = "B摘要" },
            new MaintenanceOrder { MaintOrderNo = "BY-001", EquipmentId = eq.Id, ActualDate = DateTime.Today, Executor = "张三", ExecutionSummary = "A摘要" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new MaintenanceOrderQueryParams
        { PageIndex = 0, PageSize = 20, SortBy = "executionsummary", IsDescending = false });

        result.Items[0].ExecutionSummary.Should().Be("A摘要");
        result.Items[1].ExecutionSummary.Should().Be("B摘要");
    }

    [Fact]
    public async Task GetPagedAsync_按备注排序_成功()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        ctx.MaintenanceOrders.AddRange(
            new MaintenanceOrder { MaintOrderNo = "BY-002", EquipmentId = eq.Id, ActualDate = DateTime.Today, Executor = "张三", ExecutionSummary = "正常", Remark = "B备注" },
            new MaintenanceOrder { MaintOrderNo = "BY-001", EquipmentId = eq.Id, ActualDate = DateTime.Today, Executor = "张三", ExecutionSummary = "正常", Remark = "A备注" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new MaintenanceOrderQueryParams
        { PageIndex = 0, PageSize = 20, SortBy = "remark", IsDescending = false });

        result.Items[0].Remark.Should().Be("A备注");
        result.Items[1].Remark.Should().Be("B备注");
    }

    // ========== 筛选测试（FilterDescriptor） ==========

    [Fact]
    public async Task GetPagedAsync_Filters_EquipmentNameContains_返回匹配()
    {
        var ctx = CreateDbContext();
        var eq1 = await SeedEquipmentAsync(ctx, name: "设备A", code: "EQ001");
        var eq2 = await SeedEquipmentAsync(ctx, name: "设备B", code: "EQ002");
        await SeedMaintenanceOrderAsync(ctx, eq1.Id, "BY-001");
        await SeedMaintenanceOrderAsync(ctx, eq2.Id, "BY-002");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new MaintenanceOrderQueryParams
        {
            PageIndex = 1, PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "EquipmentName", Operator = "contains", Value = "设备A" }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].EquipmentName.Should().Be("设备A");
    }

    [Fact]
    public async Task GetPagedAsync_Filters_EquipmentNameIn_返回匹配()
    {
        var ctx = CreateDbContext();
        var eq1 = await SeedEquipmentAsync(ctx, name: "设备A", code: "EQ001");
        var eq2 = await SeedEquipmentAsync(ctx, name: "设备B", code: "EQ002");
        ctx.MaintenanceOrders.AddRange(
            new MaintenanceOrder { MaintOrderNo = "BY-001", EquipmentId = eq1.Id, ActualDate = DateTime.Today, Executor = "张三", ExecutionSummary = "正常" },
            new MaintenanceOrder { MaintOrderNo = "BY-002", EquipmentId = eq2.Id, ActualDate = DateTime.Today, Executor = "李四", ExecutionSummary = "正常" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new MaintenanceOrderQueryParams
        {
            PageIndex = 1, PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "EquipmentName", Operator = "in", Values = new List<string> { "设备B" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].EquipmentName.Should().Be("设备B");
    }

    [Fact]
    public async Task GetPagedAsync_Filters_NoMatch_返回空列表()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx, name: "设备A", code: "EQ001");
        await SeedMaintenanceOrderAsync(ctx, eq.Id, "BY-001");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new MaintenanceOrderQueryParams
        {
            PageIndex = 1, PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "EquipmentName", Operator = "contains", Value = "NONEXISTENT" }
            }
        });

        result.Items.Should().BeEmpty();
    }

    // ========== GetFilterContextsAsync ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx, name: "设备A", code: "EQ001", location: "车间X");
        ctx.MaintenanceOrders.Add(new MaintenanceOrder
        {
            MaintOrderNo = "BY-001", EquipmentId = eq.Id, ActualDate = DateTime.Today,
            Executor = "张三", ExecutionSummary = "保养正常"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("MaintOrderNo");
        contexts["MaintOrderNo"].Should().Contain("BY-001");
        contexts.Should().ContainKey("EquipmentName");
        contexts["EquipmentName"].Should().Contain("设备A");
        contexts["EquipmentCode"].Should().Contain("EQ001");
        contexts["Executor"].Should().Contain("张三");
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["MaintOrderNo"].Should().BeEmpty();
        contexts["EquipmentName"].Should().BeEmpty();
        contexts["Executor"].Should().BeEmpty();
    }

    [Fact]
    public async Task GetFilterContextsAsync_Nullable字段排除null()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx, name: "设备A", code: "EQ001");
        ctx.MaintenanceOrders.Add(new MaintenanceOrder
        {
            MaintOrderNo = "BY-001", EquipmentId = eq.Id, ActualDate = DateTime.Today,
            Executor = null, ExecutionSummary = null, Remark = null
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["MaintOrderNo"].Should().HaveCount(1);
        contexts["Executor"].Should().BeEmpty();
        contexts["ExecutionSummary"].Should().BeEmpty();
        contexts["Remark"].Should().BeEmpty();
    }
}
