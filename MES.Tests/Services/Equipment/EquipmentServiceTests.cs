using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Equipment;
using EquipmentEntity = MES.Data.Entities.Equipment.Equipment;
using MES.Tests.Tests;
using MES.Core.DTOs.Equipment;

namespace MES.Tests.Services;

public class EquipmentServiceTests : TestBase
{
    private static EquipmentService CreateService(AppDbContext ctx) => new(ctx);

    // ========== GetPagedAsync ==========

    [Fact]
    public async Task GetPagedAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new EquipmentQueryParams());

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索设备编码_返回匹配()
    {
        var ctx = CreateDbContext();
        ctx.Equipment.AddRange(
            new EquipmentEntity { EquipmentCode = "EQ-001", EquipmentName = "车床", Location = "A区" },
            new EquipmentEntity { EquipmentCode = "EQ-002", EquipmentName = "铣床", Location = "B区" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new EquipmentQueryParams
        {
            Keyword = "EQ-001"
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].EquipmentCode.Should().Be("EQ-001");
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索设备名称_返回匹配()
    {
        var ctx = CreateDbContext();
        ctx.Equipment.AddRange(
            new EquipmentEntity { EquipmentCode = "EQ-001", EquipmentName = "数控车床", Location = "A区" },
            new EquipmentEntity { EquipmentCode = "EQ-002", EquipmentName = "普通铣床", Location = "B区" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new EquipmentQueryParams
        {
            Keyword = "车床"
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].EquipmentCode.Should().Be("EQ-001");
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索备注_返回匹配()
    {
        var ctx = CreateDbContext();
        ctx.Equipment.AddRange(
            new EquipmentEntity { EquipmentCode = "EQ-001", EquipmentName = "车床", Location = "A区", Remark = "需定期保养" },
            new EquipmentEntity { EquipmentCode = "EQ-002", EquipmentName = "铣床", Location = "B区" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new EquipmentQueryParams
        {
            Keyword = "定期保养"
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].EquipmentCode.Should().Be("EQ-001");
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索维保人_返回匹配()
    {
        var ctx = CreateDbContext();
        ctx.Equipment.AddRange(
            new EquipmentEntity { EquipmentCode = "EQ-001", EquipmentName = "车床", Location = "A区", InspectionPerson = "张三" },
            new EquipmentEntity { EquipmentCode = "EQ-002", EquipmentName = "铣床", Location = "B区", MaintPerson = "李四" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new EquipmentQueryParams
        {
            Keyword = "张三"
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].EquipmentCode.Should().Be("EQ-001");
    }

    [Fact]
    public async Task GetPagedAsync_关键词无匹配_返回空列表()
    {
        var ctx = CreateDbContext();
        ctx.Equipment.AddRange(
            new EquipmentEntity { EquipmentCode = "EQ-001", EquipmentName = "车床", Location = "A区" },
            new EquipmentEntity { EquipmentCode = "EQ-002", EquipmentName = "铣床", Location = "B区" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new EquipmentQueryParams
        {
            Keyword = "不存在"
        });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_按设备编码排序_成功()
    {
        var ctx = CreateDbContext();
        ctx.Equipment.AddRange(
            new EquipmentEntity { EquipmentCode = "EQ-B", EquipmentName = "车床", Location = "A区" },
            new EquipmentEntity { EquipmentCode = "EQ-A", EquipmentName = "铣床", Location = "B区" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new EquipmentQueryParams
        {
            SortBy = "equipmentcode",
            IsDescending = false
        });

        result.Items.Should().HaveCount(2);
        result.Items[0].EquipmentCode.Should().Be("EQ-A");
        result.Items[1].EquipmentCode.Should().Be("EQ-B");
    }

    [Fact]
    public async Task GetPagedAsync_按设备名称降序排序_成功()
    {
        var ctx = CreateDbContext();
        ctx.Equipment.AddRange(
            new EquipmentEntity { EquipmentCode = "EQ-001", EquipmentName = "车床", Location = "A区" },
            new EquipmentEntity { EquipmentCode = "EQ-002", EquipmentName = "铣床", Location = "B区" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new EquipmentQueryParams
        {
            SortBy = "equipmentname",
            IsDescending = true
        });

        result.Items.Should().HaveCount(2);
        result.Items[0].EquipmentName.Should().Be("铣床");
        result.Items[1].EquipmentName.Should().Be("车床");
    }

    [Fact]
    public async Task GetPagedAsync_按生命周期筛选_返回匹配()
    {
        var ctx = CreateDbContext();
        ctx.Equipment.AddRange(
            new EquipmentEntity { EquipmentCode = "EQ-001", EquipmentName = "车床", Location = "A区", LifecycleStatus = "Active" },
            new EquipmentEntity { EquipmentCode = "EQ-002", EquipmentName = "铣床", Location = "B区", LifecycleStatus = "Standby" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new EquipmentQueryParams
        {
            LifecycleStatus = LifecycleStatus.Active
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].EquipmentCode.Should().Be("EQ-001");
    }

    [Fact]
    public async Task GetPagedAsync_按使用类型筛选_返回匹配()
    {
        var ctx = CreateDbContext();
        ctx.Equipment.AddRange(
            new EquipmentEntity { EquipmentCode = "EQ-001", EquipmentName = "车床", Location = "A区", UsageType = "Primary" },
            new EquipmentEntity { EquipmentCode = "EQ-002", EquipmentName = "铣床", Location = "B区", UsageType = "Secondary" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new EquipmentQueryParams
        {
            UsageType = UsageType.Primary
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].EquipmentCode.Should().Be("EQ-001");
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_存在_返回详情()
    {
        var ctx = CreateDbContext();
        var entity = new EquipmentEntity
        {
            EquipmentCode = "EQ-001",
            EquipmentName = "数控车床",
            Location = "A区",
            ModelNumber = "CK6140",
            LifecycleStatus = "Active",
            UsageType = "Primary"
        };
        ctx.Equipment.Add(entity);
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(entity.Id);

        result.Should().NotBeNull();
        result.Id.Should().Be(entity.Id);
        result.EquipmentCode.Should().Be("EQ-001");
        result.EquipmentName.Should().Be("数控车床");
        result.ModelNumber.Should().Be("CK6140");
    }

    [Fact]
    public async Task GetByIdAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetByIdAsync(999);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("设备不存在");
    }

    // ========== CreateAsync ==========

    [Fact]
    public async Task CreateAsync_创建成功()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var request = new CreateEquipmentRequest
        {
            EquipmentCode = "EQ-001",
            EquipmentName = "数控车床",
            Location = "A区",
            ModelNumber = "CK6140",
            Manufacturer = "沈阳机床",
            LifecycleStatus = LifecycleStatus.Active,
            UsageType = UsageType.Primary
        };

        var result = await svc.CreateAsync(request);

        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.EquipmentCode.Should().Be("EQ-001");
        result.EquipmentName.Should().Be("数控车床");
        result.ModelNumber.Should().Be("CK6140");
        result.Manufacturer.Should().Be("沈阳机床");
        result.Location.Should().Be("A区");

        var saved = await ctx.Equipment.FirstOrDefaultAsync(e => e.Id == result.Id);
        saved.Should().NotBeNull();
        saved!.EquipmentCode.Should().Be("EQ-001");
    }

    [Fact]
    public async Task CreateAsync_编号重复_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        ctx.Equipment.Add(new EquipmentEntity
        {
            EquipmentCode = "EQ-001",
            EquipmentName = "现有设备",
            Location = "A区"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var request = new CreateEquipmentRequest
        {
            EquipmentCode = "EQ-001",
            EquipmentName = "新设备",
            Location = "B区"
        };

        var act = () => svc.CreateAsync(request);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*设备编号 EQ-001 已存在*");
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_更新成功()
    {
        var ctx = CreateDbContext();
        var entity = new EquipmentEntity
        {
            EquipmentCode = "EQ-001",
            EquipmentName = "旧名称",
            Location = "A区",
            ModelNumber = "旧型号",
            LifecycleStatus = "Active",
            UsageType = "Primary"
        };
        ctx.Equipment.Add(entity);
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var request = new UpdateEquipmentRequest
        {
            EquipmentCode = "EQ-001",
            EquipmentName = "新名称",
            Location = "B区",
            ModelNumber = "新型号",
            LifecycleStatus = LifecycleStatus.Standby,
            UsageType = UsageType.Secondary
        };

        var result = await svc.UpdateAsync(entity.Id, request);

        result.EquipmentName.Should().Be("新名称");
        result.Location.Should().Be("B区");
        result.ModelNumber.Should().Be("新型号");
        result.LifecycleStatus.Should().Be(LifecycleStatus.Standby);
        result.UsageType.Should().Be(UsageType.Secondary);

        var updated = await ctx.Equipment.FirstAsync(e => e.Id == entity.Id);
        updated.EquipmentName.Should().Be("新名称");
        updated.Location.Should().Be("B区");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_删除成功()
    {
        var ctx = CreateDbContext();
        var entity = new EquipmentEntity
        {
            EquipmentCode = "EQ-001",
            EquipmentName = "待删除设备",
            Location = "A区"
        };
        ctx.Equipment.Add(entity);
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(entity.Id);

        var deleted = await ctx.Equipment.FirstOrDefaultAsync(e => e.Id == entity.Id);
        deleted.Should().BeNull();
    }

    // ========== B10/B11 专项测试 ==========

    [Fact]
    public async Task GetPagedAsync_按生命周期排序_成功()
    {
        var ctx = CreateDbContext();
        ctx.Equipment.AddRange(
            new EquipmentEntity { EquipmentCode = "EQ-B", EquipmentName = "设备B", Location = "A区", LifecycleStatus = "Standby", UsageType = "Primary" },
            new EquipmentEntity { EquipmentCode = "EQ-A", EquipmentName = "设备A", Location = "A区", LifecycleStatus = "Active", UsageType = "Primary" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var resultAsc = await svc.GetPagedAsync(new EquipmentQueryParams
        { PageIndex = 0, PageSize = 20, SortBy = "lifecyclestatus", IsDescending = false });

        resultAsc.Items[0].LifecycleStatus.Should().Be(LifecycleStatus.Active);
        resultAsc.Items[1].LifecycleStatus.Should().Be(LifecycleStatus.Standby);
    }

    [Fact]
    public async Task GetPagedAsync_按使用类型排序_成功()
    {
        var ctx = CreateDbContext();
        ctx.Equipment.AddRange(
            new EquipmentEntity { EquipmentCode = "EQ-B", EquipmentName = "设备B", Location = "A区", LifecycleStatus = "Active", UsageType = "Secondary" },
            new EquipmentEntity { EquipmentCode = "EQ-A", EquipmentName = "设备A", Location = "A区", LifecycleStatus = "Active", UsageType = "Primary" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var resultAsc = await svc.GetPagedAsync(new EquipmentQueryParams
        { PageIndex = 0, PageSize = 20, SortBy = "usagetype", IsDescending = false });

        resultAsc.Items[0].UsageType.Should().Be(UsageType.Primary);
        resultAsc.Items[1].UsageType.Should().Be(UsageType.Secondary);
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索生命周期_返回匹配()
    {
        var ctx = CreateDbContext();
        ctx.Equipment.AddRange(
            new EquipmentEntity { EquipmentCode = "EQ-001", EquipmentName = "车床", Location = "A区", LifecycleStatus = "Active", UsageType = "Primary" },
            new EquipmentEntity { EquipmentCode = "EQ-002", EquipmentName = "铣床", Location = "B区", LifecycleStatus = "Standby", UsageType = "Secondary" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new EquipmentQueryParams
        { PageIndex = 0, PageSize = 20, Keyword = "Active" });

        result.Items.Should().HaveCount(1);
        result.Items[0].LifecycleStatus.Should().Be(LifecycleStatus.Active);
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索使用类型_返回匹配()
    {
        var ctx = CreateDbContext();
        ctx.Equipment.AddRange(
            new EquipmentEntity { EquipmentCode = "EQ-001", EquipmentName = "车床", Location = "A区", LifecycleStatus = "Active", UsageType = "Primary" },
            new EquipmentEntity { EquipmentCode = "EQ-002", EquipmentName = "铣床", Location = "B区", LifecycleStatus = "Standby", UsageType = "Secondary" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new EquipmentQueryParams
        { PageIndex = 0, PageSize = 20, Keyword = "Secondary" });

        result.Items.Should().HaveCount(1);
        result.Items[0].UsageType.Should().Be(UsageType.Secondary);
    }

    // ========== 筛选上下文 ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        ctx.Equipment.AddRange(
            new EquipmentEntity { EquipmentCode = "EQ-001", EquipmentName = "车床", ModelNumber = "M1", Location = "A区", RelatedSection = "加工", LifecycleStatus = "Active", UsageType = "Primary" },
            new EquipmentEntity { EquipmentCode = "EQ-002", EquipmentName = "铣床", ModelNumber = null, Location = "B区", RelatedSection = null, LifecycleStatus = "Standby", UsageType = "Secondary" }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        result.Should().ContainKeys("EquipmentCode", "EquipmentName", "ModelNumber", "Location", "RelatedSection");
        result["EquipmentCode"].Should().BeEquivalentTo(new[] { "EQ-001", "EQ-002" }, options => options.WithStrictOrdering());
        result["EquipmentName"].Should().BeEquivalentTo(new[] { "车床", "铣床" }, options => options.WithStrictOrdering());
        result["ModelNumber"].Should().HaveCount(1).And.Contain("M1");
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        result.Should().ContainKeys("EquipmentCode", "EquipmentName", "ModelNumber", "Location", "RelatedSection");
        foreach (var kvp in result)
            kvp.Value.Should().BeEmpty($"字段 {kvp.Key} 应返回空列表");
    }
}
