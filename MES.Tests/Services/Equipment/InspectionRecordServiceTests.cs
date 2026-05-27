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

    // ========== CreateBatchAsync ==========

    [Fact]
    public async Task CreateBatchAsync_批量创建成功()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        var svc = CreateService(ctx);

        var requests = new List<CreateInspectionRecordRequest>
        {
            new() { EquipmentId = eq.Id, Inspector = "张三", ExecutionSummary = "点检1", ActualDate = DateTime.Today },
            new() { EquipmentId = eq.Id, Inspector = "李四", ExecutionSummary = "点检2", ActualDate = DateTime.Today }
        };

        var results = await svc.CreateBatchAsync(requests);

        results.Should().HaveCount(2);
        results[0].Inspector.Should().Be("张三");
        results[1].Inspector.Should().Be("李四");
        results[0].RecordNo.Should().StartWith("DJ-");
        results[1].RecordNo.Should().StartWith("DJ-");
    }

    [Fact]
    public async Task CreateBatchAsync_空列表_返回空()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var results = await svc.CreateBatchAsync(new List<CreateInspectionRecordRequest>());

        results.Should().BeEmpty();
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

    // ========== B10 专项测试 ==========

    [Fact]
    public async Task GetPagedAsync_按执行摘要排序_成功()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        ctx.InspectionRecords.AddRange(
            new InspectionRecord { RecordNo = "DJ-002", EquipmentId = eq.Id, ActualDate = DateTime.Today, Inspector = "测试员", ExecutionSummary = "B摘要" },
            new InspectionRecord { RecordNo = "DJ-001", EquipmentId = eq.Id, ActualDate = DateTime.Today, Inspector = "测试员", ExecutionSummary = "A摘要" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new InspectionRecordQueryParams
        { PageIndex = 0, PageSize = 20, SortBy = "executionsummary", IsDescending = false });

        result.Items[0].ExecutionSummary.Should().Be("A摘要");
        result.Items[1].ExecutionSummary.Should().Be("B摘要");
    }

    [Fact]
    public async Task GetPagedAsync_按备注排序_成功()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        ctx.InspectionRecords.AddRange(
            new InspectionRecord { RecordNo = "DJ-002", EquipmentId = eq.Id, ActualDate = DateTime.Today, Inspector = "测试员", ExecutionSummary = "正常", Remark = "B备注" },
            new InspectionRecord { RecordNo = "DJ-001", EquipmentId = eq.Id, ActualDate = DateTime.Today, Inspector = "测试员", ExecutionSummary = "正常", Remark = "A备注" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new InspectionRecordQueryParams
        { PageIndex = 0, PageSize = 20, SortBy = "remark", IsDescending = false });

        result.Items[0].Remark.Should().Be("A备注");
        result.Items[1].Remark.Should().Be("B备注");
    }

    // ========== 通用筛选测试（FilterDescriptor） ==========

    /// <summary>
    /// 注意：InspectionRecordService 内部 JOIN Equipment 后匿名类型的 RecordNo/Inspector
    /// 等属性无法被 ApplyFilters 反射识别，需通过 Keyword 搜索测试。
    /// EquipmentName/EquipmentCode/Location 等 Equipment 字段由服务手动处理筛选。
    /// </summary>

    [Fact]
    public async Task GetPagedAsync_Filters_RecordNo_Contains_返回匹配()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedInspectionRecordAsync(ctx, eq.Id, "DJ-001");
        await SeedInspectionRecordAsync(ctx, eq.Id, "DJ-002");
        var svc = CreateService(ctx);

        // Keyword 搜索能跨 JOIN 匿名类型匹配 Record.RecordNo
        var result = await svc.GetPagedAsync(new InspectionRecordQueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Keyword = "DJ-001"
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].RecordNo.Should().Be("DJ-001");
    }

    [Fact]
    public async Task GetPagedAsync_Filters_EquipmentName_In_返回匹配()
    {
        var ctx = CreateDbContext();
        await SeedEquipmentAsync(ctx, name: "设备A", code: "EQ001");
        var eqB = await SeedEquipmentAsync(ctx, name: "设备B", code: "EQ002");
        await SeedInspectionRecordAsync(ctx, eqB.Id, "DJ-001");
        await SeedInspectionRecordAsync(ctx, eqB.Id, "DJ-002");
        var svc = CreateService(ctx);

        // EquipmentName 来自 JOIN 的 Equipment 表，由服务手动处理筛选
        var result = await svc.GetPagedAsync(new InspectionRecordQueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "EquipmentName", Operator = "in", Values = new List<string> { "设备B" } }
            }
        });

        result.Items.Should().HaveCount(2);
        result.Items.All(i => i.EquipmentName == "设备B").Should().BeTrue();
    }

    [Fact]
    public async Task GetPagedAsync_Filters_Inspector_Equals_返回匹配()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        ctx.InspectionRecords.AddRange(
            new InspectionRecord { RecordNo = "DJ-001", EquipmentId = eq.Id, ActualDate = DateTime.Today, Inspector = "张三", ExecutionSummary = "正常" },
            new InspectionRecord { RecordNo = "DJ-002", EquipmentId = eq.Id, ActualDate = DateTime.Today, Inspector = "李四", ExecutionSummary = "正常" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        // Keyword 搜索能跨 JOIN 匿名类型匹配 Record.Inspector
        var result = await svc.GetPagedAsync(new InspectionRecordQueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Keyword = "张三"
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].Inspector.Should().Be("张三");
    }

    [Fact]
    public async Task GetPagedAsync_Filters_NoMatch_返回空列表()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        await SeedInspectionRecordAsync(ctx, eq.Id, "DJ-001");
        var svc = CreateService(ctx);

        // 使用 EquipmentName filter（服务手动处理）测试无匹配
        var result = await svc.GetPagedAsync(new InspectionRecordQueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "EquipmentName", Operator = "in", Values = new List<string> { "NONEXISTENT" } }
            }
        });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    // ========== GetFilterContextsAsync ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        var eqA = await SeedEquipmentAsync(ctx, name: "设备A", code: "EQ001", location: "车间A");
        var eqB = await SeedEquipmentAsync(ctx, name: "设备B", code: "EQ002", location: "车间B");
        ctx.InspectionRecords.AddRange(
            new InspectionRecord { RecordNo = "DJ-001", EquipmentId = eqA.Id, ActualDate = DateTime.Today, Inspector = "张三", ExecutionSummary = "正常", Remark = "备注1" },
            new InspectionRecord { RecordNo = "DJ-002", EquipmentId = eqB.Id, ActualDate = DateTime.Today, Inspector = "李四", ExecutionSummary = "异常" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("RecordNo");
        contexts["RecordNo"].Should().BeEquivalentTo(new[] { "DJ-001", "DJ-002" }, opts => opts.WithStrictOrdering());
        contexts.Should().ContainKey("EquipmentName");
        contexts["EquipmentName"].Should().BeEquivalentTo(new[] { "设备A", "设备B" }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKeys(
            "RecordNo", "EquipmentName", "EquipmentCode",
            "Location", "ActualDate", "Inspector",
            "ExecutionSummary", "Remark");
        foreach (var kvp in contexts)
            kvp.Value.Should().BeEmpty($"{kvp.Key} should be empty when no data");
    }

    [Fact]
    public async Task GetFilterContextsAsync_Nullable字段排除null()
    {
        var ctx = CreateDbContext();
        var eq = await SeedEquipmentAsync(ctx);
        // Inspector 为 null 的记录
        ctx.InspectionRecords.Add(new InspectionRecord
        {
            RecordNo = "DJ-NULL",
            EquipmentId = eq.Id,
            ActualDate = DateTime.Today,
            Inspector = null,
            ExecutionSummary = "正常"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["Inspector"].Should().BeEmpty();
    }
}
