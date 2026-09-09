using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Core.Constants;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Shared;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Batch;
using MES.Core.Interfaces.Configuration;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Batch;
using MES.Services.Batch;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 委外单位档案服务测试：WV 编码自增、(单位名×工段) 行键唯一、CRUD、批次建档、筛选上下文、委外候选源
/// </summary>
public class OutsourceVendorProfileServiceTests : TestBase
{
    private static int _seed;

    private static OutsourceVendorProfileService CreateService(AppDbContext ctx)
        => new(ctx, new MemoryCache(new MemoryCacheOptions()));

    private static async Task<OutsourceVendorProfile> SeedAsync(AppDbContext ctx,
        string name = "新志皓", string section = "ColdRollDraw", bool active = true, bool isWorkshop = false)
    {
        var e = new OutsourceVendorProfile
        {
            VendorCode = $"WV{9000 + _seed++}",
            VendorName = name,
            SectionName = section,
            IsWorkshop = isWorkshop,
            ContactPerson = "张三",
            ContactPhone = "13800138000",
            IsActive = active
        };
        ctx.OutsourceVendorProfiles.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    // ========== CreateAsync 编码自增 ==========

    [Fact]
    public async Task CreateAsync_首个档案_编码WV0001()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var dto = await svc.CreateAsync(new CreateOutsourceVendorRequest
        {
            VendorName = "新志皓",
            SectionName = "ColdRollDraw",
            IsWorkshop = false,
            IsActive = true
        });

        dto.VendorCode.Should().Be("WV0001");
        dto.VendorName.Should().Be("新志皓");
        dto.SectionName.Should().Be("ColdRollDraw");
        dto.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateAsync_连续建档_编码依次自增()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var a = await svc.CreateAsync(new CreateOutsourceVendorRequest { VendorName = "甲", SectionName = "ColdRollDraw", IsActive = true });
        var b = await svc.CreateAsync(new CreateOutsourceVendorRequest { VendorName = "乙", SectionName = "ColdRollDraw", IsActive = true });

        a.VendorCode.Should().Be("WV0001");
        b.VendorCode.Should().Be("WV0002");
    }

    // ========== 行键唯一：(单位名 × 工段) ==========

    [Fact]
    public async Task CreateAsync_同单位同工段_抛业务异常()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, name: "新志皓", section: "ColdRollDraw");
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateOutsourceVendorRequest { VendorName = "新志皓", SectionName = "ColdRollDraw", IsActive = true });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("委外单位「新志皓」在工段「ColdRollDraw」已存在档案，请勿重复建档");
    }

    [Fact]
    public async Task CreateAsync_同单位不同工段_允许建档()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, name: "新志皓", section: "ColdRollDraw");
        var svc = CreateService(ctx);

        var dto = await svc.CreateAsync(new CreateOutsourceVendorRequest { VendorName = "新志皓", SectionName = "OuterPolish", IsActive = true });

        dto.SectionName.Should().Be("OuterPolish");
    }

    [Fact]
    public async Task CreateAsync_单位名为空_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateOutsourceVendorRequest { VendorName = "  ", SectionName = "ColdRollDraw", IsActive = true });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("委外单位名不能为空");
    }

    // ========== CreateBatchAsync ==========

    [Fact]
    public async Task CreateBatchAsync_多行_编码连续自增()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var dtos = await svc.CreateBatchAsync(new List<CreateOutsourceVendorRequest>
        {
            new() { VendorName = "甲", SectionName = "ColdRollDraw", IsActive = true },
            new() { VendorName = "乙", SectionName = "ColdRollDraw", IsActive = true },
            new() { VendorName = "丙", SectionName = "OuterPolish", IsWorkshop = true, IsActive = true },
        });

        dtos.Should().HaveCount(3);
        dtos[0].VendorCode.Should().Be("WV0001");
        dtos[1].VendorCode.Should().Be("WV0002");
        dtos[2].VendorCode.Should().Be("WV0003");
        dtos[2].IsWorkshop.Should().BeTrue();
    }

    [Fact]
    public async Task CreateBatchAsync_批内重复行_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.CreateBatchAsync(new List<CreateOutsourceVendorRequest>
        {
            new() { VendorName = "甲", SectionName = "ColdRollDraw", IsActive = true },
            new() { VendorName = "甲", SectionName = "ColdRollDraw", IsActive = true },
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*第 2 行*重复*");
    }

    [Fact]
    public async Task CreateBatchAsync_与库内既有重复_抛业务异常()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, name: "甲", section: "ColdRollDraw");
        var svc = CreateService(ctx);

        var act = () => svc.CreateBatchAsync(new List<CreateOutsourceVendorRequest>
        {
            new() { VendorName = "乙", SectionName = "ColdRollDraw", IsActive = true },
            new() { VendorName = "甲", SectionName = "ColdRollDraw", IsActive = true },
        });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("委外单位「甲」在工段「ColdRollDraw」已存在档案，请勿重复建档");
    }

    // ========== UpdateAsync / DeleteAsync ==========

    [Fact]
    public async Task UpdateAsync_改名撞车_抛业务异常()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, name: "甲", section: "ColdRollDraw");
        var existing = await SeedAsync(ctx, name: "乙", section: "ColdRollDraw");
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(existing.Id, new UpdateOutsourceVendorRequest { VendorName = "甲" });

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("委外单位「甲」在工段「ColdRollDraw」已存在档案，请勿重复建档");
    }

    [Fact]
    public async Task UpdateAsync_正常修改_生效()
    {
        var ctx = CreateDbContext();
        var e = await SeedAsync(ctx, name: "甲", section: "ColdRollDraw");
        var svc = CreateService(ctx);

        var dto = await svc.UpdateAsync(e.Id, new UpdateOutsourceVendorRequest
        {
            ContactPerson = "李四",
            IsActive = false,
            Remark = "停用备注"
        });

        dto.ContactPerson.Should().Be("李四");
        dto.IsActive.Should().BeFalse();
        dto.Remark.Should().Be("停用备注");
        dto.VendorName.Should().Be("甲");
    }

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        var e = await SeedAsync(ctx);
        var svc = CreateService(ctx);

        await svc.DeleteAsync(e.Id);

        ctx.OutsourceVendorProfiles.Count().Should().Be(0);
    }

    // ========== GetPagedAsync / GetFilterContextsAsync ==========

    [Fact]
    public async Task GetPagedAsync_关键字搜索_命中单位名与联系人()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, name: "新志皓", section: "ColdRollDraw");
        var svc = CreateService(ctx);

        var byName = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "新志皓" });
        byName.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetFilterContextsAsync_返回各列去重上下文()
    {
        var ctx = CreateDbContext();
        await SeedAsync(ctx, name: "新志皓", section: "ColdRollDraw");
        await SeedAsync(ctx, name: "新志皓", section: "OuterPolish");
        var svc = CreateService(ctx);

        var dict = await svc.GetFilterContextsAsync();

        dict["VendorName"].Should().ContainSingle().Which.Should().Be("新志皓");
        dict["SectionName"].Should().HaveCount(2);
        dict.Should().ContainKey("IsWorkshop");
        dict.Should().ContainKey("IsActive");
        dict["VendorCode"].Should().NotBeEmpty();
    }

    // ========== ② 往来信息统计（SectionOutsource + OutsourceRecovery 种子） ==========

    private static int _soSeed;

    private static async Task<SectionOutsource> SeedOutsourceAsync(AppDbContext ctx, string vendor, string section,
        decimal? sendWeight = 1000m, decimal? totalAmount = 1400m, bool isInternal = false,
        SectionOutsourceStatus status = SectionOutsourceStatus.PendingRecovery, DateTime? sendOutDate = null)
    {
        var e = new SectionOutsource
        {
            ProductionBatchId = 9000 + _soSeed,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            SectionName = section,
            SequenceNumber = 1,
            OutsourceVendor = vendor,
            SendOutDate = sendOutDate ?? DateTime.Today,
            SendQuantity = 10,
            SendWeight = sendWeight,
            TotalAmount = totalAmount,
            Status = status,
            IsInternal = isInternal
        };
        _soSeed++;
        ctx.SectionOutsources.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    private static async Task<OutsourceRecovery> SeedRecoveryAsync(AppDbContext ctx, int soId, DateTime recoveryDate,
        decimal? recoveryWeight = null, decimal? unprocessedWeight = null)
    {
        var e = new OutsourceRecovery
        {
            SectionOutsourceId = soId,
            RecoveryDate = recoveryDate,
            RecoveryQuantity = 10,
            RecoveryWeight = recoveryWeight,
            UnprocessedWeight = unprocessedWeight
        };
        ctx.OutsourceRecoveries.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    private static async Task<OutsourceVendorProfileDto> GetRowAsync(OutsourceVendorProfileService svc, int profileId)
    {
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 100 });
        return result.Items.Single(x => x.Id == profileId);
    }

    [Fact]
    public async Task GetPagedAsync_统计_按年份切分发出回收与在外净欠负数截0()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var year = DateTime.Today.Year;
        var lastYear = new DateTime(year - 1, 6, 1);
        var thisYear = new DateTime(year, 6, 1);

        var profile = await SeedAsync(ctx, name: "甲", section: SectionKeys.ColdRollDraw);

        // 去年发出、去年已回收齐 → 计累计不计本年（回收去年也不计本年）
        var soLast = await SeedOutsourceAsync(ctx, "甲", SectionKeys.ColdRollDraw,
            sendWeight: 1000m, totalAmount: 1000m, sendOutDate: lastYear, status: SectionOutsourceStatus.Recovered);
        await SeedRecoveryAsync(ctx, soLast.Id, lastYear, recoveryWeight: 1000m);

        // 今年发出未回收齐：回收 800 + 退回 200 → 在外净欠 1000
        var soThis = await SeedOutsourceAsync(ctx, "甲", SectionKeys.ColdRollDraw,
            sendWeight: 2000m, totalAmount: 2000m, sendOutDate: thisYear, status: SectionOutsourceStatus.PendingRecovery);
        await SeedRecoveryAsync(ctx, soThis.Id, thisYear, recoveryWeight: 800m);
        await SeedRecoveryAsync(ctx, soThis.Id, thisYear, unprocessedWeight: 200m);

        // 今年发出、回收+退回已超发出 → 在外净欠截 0
        var soOver = await SeedOutsourceAsync(ctx, "甲", SectionKeys.ColdRollDraw,
            sendWeight: 500m, totalAmount: 500m, sendOutDate: thisYear, status: SectionOutsourceStatus.PendingRecovery);
        await SeedRecoveryAsync(ctx, soOver.Id, thisYear, recoveryWeight: 600m, unprocessedWeight: 100m);

        var row = await GetRowAsync(svc, profile.Id);

        // 累计委外：3 单 / 3500kg / 3500 元
        row.TotalOrderCount.Should().Be(3);
        row.TotalWeight.Should().Be(3500m);
        row.TotalAmount.Should().Be(3500m);
        // 本年委外：今年发出的 2 单（soLast 去年发出不计本年）
        row.YearOrderCount.Should().Be(2);
        row.YearWeight.Should().Be(2500m);
        row.YearAmount.Should().Be(2500m);
        // 本年回收：soLast 去年回收不计 + soThis 800 + soOver 600
        row.YearRecoveredWeight.Should().Be(1400m);
        // 本年回收金额：soThis 2000×800/2000=800 + soOver 500×(600截500)/500=500（soLast 去年回收不计金额）
        row.YearRecoveredAmount.Should().Be(1300m);
        // 本年退回：soThis 200 + soOver 100（退回只冲减欠重、不产生回收金额）
        row.YearReturnWeight.Should().Be(300m);
        // 在委外净欠：soThis(2000-800-200=1000) + soOver(500-700 截 0)
        row.PendingWeight.Should().Be(1000m);
        // 委外未回收金额：soThis 2000×1000/2000=1000（soOver 净欠 0）
        row.PendingAmount.Should().Be(1000m);
    }

    [Fact]
    public async Task GetPagedAsync_统计金额_退回不产生金额_未回收整单按份额()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var profile = await SeedAsync(ctx, name: "甲", section: SectionKeys.ColdRollDraw);

        // 一单发出后基本全退回（正常仅 300、退回 700）：回收金额只按正常 300 分摊、退回部分不计金额
        var soA = await SeedOutsourceAsync(ctx, "甲", SectionKeys.ColdRollDraw,
            sendWeight: 1000m, totalAmount: 1400m);
        await SeedRecoveryAsync(ctx, soA.Id, DateTime.Today, recoveryWeight: 300m);
        await SeedRecoveryAsync(ctx, soA.Id, DateTime.Today, unprocessedWeight: 700m);

        // 一单完全未回收：未回收金额 = 整单金额
        await SeedOutsourceAsync(ctx, "甲", SectionKeys.ColdRollDraw,
            sendWeight: 2000m, totalAmount: 3000m);

        var row = await GetRowAsync(svc, profile.Id);

        row.YearRecoveredWeight.Should().Be(300m);
        row.YearRecoveredAmount.Should().Be(420m);      // 1400 × 300/1000
        row.YearReturnWeight.Should().Be(700m);
        row.PendingWeight.Should().Be(2000m);           // soA 已回齐不计，soB 全欠
        row.PendingAmount.Should().Be(3000m);           // 3000 × 2000/2000
    }

    [Fact]
    public async Task GetPagedAsync_统计_厂内IsInternal行不计()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        // 本厂车间档案行（IsWorkshop）：对应发出单 IsInternal=true、无价、Status=Virtual → 不应产生任何统计
        var workshop = await SeedAsync(ctx, name: "乙", section: SectionKeys.ColdRollDraw, isWorkshop: true);
        await SeedOutsourceAsync(ctx, "乙", SectionKeys.ColdRollDraw,
            sendWeight: null, totalAmount: null, isInternal: true, status: SectionOutsourceStatus.Virtual);

        var row = await GetRowAsync(svc, workshop.Id);

        row.TotalOrderCount.Should().Be(0);
        row.TotalWeight.Should().Be(0m);
        row.TotalAmount.Should().Be(0m);
        row.YearOrderCount.Should().Be(0);
        row.YearWeight.Should().Be(0m);
        row.YearRecoveredWeight.Should().Be(0m);
        row.YearRecoveredAmount.Should().Be(0m);
        row.PendingWeight.Should().Be(0m);
        row.PendingAmount.Should().Be(0m);
        row.YearReturnWeight.Should().Be(0m);
    }

    [Fact]
    public async Task GetPagedAsync_统计_同名跨工段分流不混算()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var cd = await SeedAsync(ctx, name: "甲", section: SectionKeys.ColdRollDraw);
        var op = await SeedAsync(ctx, name: "甲", section: SectionKeys.OuterPolish);

        await SeedOutsourceAsync(ctx, "甲", SectionKeys.ColdRollDraw, sendWeight: 1000m, totalAmount: 1000m);
        await SeedOutsourceAsync(ctx, "甲", SectionKeys.ColdRollDraw, sendWeight: 2000m, totalAmount: 2000m);
        await SeedOutsourceAsync(ctx, "甲", SectionKeys.OuterPolish, sendWeight: 3000m, totalAmount: 3000m);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 100 });
        var cdRow = result.Items.Single(x => x.Id == cd.Id);
        var opRow = result.Items.Single(x => x.Id == op.Id);

        cdRow.TotalOrderCount.Should().Be(2);
        cdRow.TotalWeight.Should().Be(3000m);
        cdRow.TotalAmount.Should().Be(3000m);
        opRow.TotalOrderCount.Should().Be(1);
        opRow.TotalWeight.Should().Be(3000m);
        opRow.TotalAmount.Should().Be(3000m);
    }

    [Fact]
    public async Task GetPagedAsync_统计_档外文本不计()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var profile = await SeedAsync(ctx, name: "甲", section: SectionKeys.ColdRollDraw);
        // 历史档外单位（本页无对应档案行）：新单已强制命中档案，存量漂移尾巴不属本页任何行不计
        await SeedOutsourceAsync(ctx, "档外单位X", SectionKeys.ColdRollDraw, sendWeight: 9999m, totalAmount: 9999m);

        var row = await GetRowAsync(svc, profile.Id);

        row.TotalOrderCount.Should().Be(0);
        row.TotalWeight.Should().Be(0m);
        row.TotalAmount.Should().Be(0m);
    }

    [Fact]
    public async Task PrintOutsourceVendorListAsync_字典行含统计文本_返回PDF()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var items = new List<Dictionary<string, object>>
        {
            new() { ["VendorName"] = "甲", ["SectionName"] = "冷轧拔", ["TotalOrdering"] = "3单/3.5吨", ["Pending"] = "1.0吨" }
        };
        var columns = new List<PrintColumnDef>
        {
            new() { Key = "VendorName", Label = "委外单位名" },
            new() { Key = "SectionName", Label = "委外工段" },
            new() { Key = "TotalOrdering", Label = "累计委外" },
            new() { Key = "Pending", Label = "委外未回收" }
        };

        var pdf = await svc.PrintOutsourceVendorListAsync("委外单位列表", items, columns);

        pdf.Should().NotBeNull();
        pdf.Length.Should().BeGreaterThan(0);
    }
}
