using FluentAssertions;
using MES.Core.Constants;
using MES.Core.DTOs.Configuration;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Scheduling;
using MES.Services.Configuration;
using MES.Tests.Tests;

namespace MES.Tests.Services.Configuration;

/// <summary>
/// 段落日产配置服务同步机制测试：段落由 3 类配置自动生成
/// （冷轧拔=机台组显示名 / 普通工段=StandardWorkDays 启用工段扣冷轧拔检验入库 / 检验=固定荒管检在制检），
/// GetSettingsAsync 内部先同步期望段落集（缺失补齐、多余删除、显示名/顺序联动）再返回，段落随配置增减、仅参数可编辑。
/// </summary>
public class SectionParagraphConfigServiceTests : TestBase
{
    private SectionParagraphConfigService CreateService(AppDbContext ctx, params SectionInfoDto[] sections)
    {
        var stdMock = CreateStandardWorkDayServiceMock(sections);
        return new SectionParagraphConfigService(ctx, stdMock);
    }

    private void SeedMachineGroup(AppDbContext ctx, string groupKey, string displayName, int displayOrder)
    {
        ctx.ColdRollMachineGroupConfigs.Add(new ColdRollMachineGroupConfig
        {
            GroupKey = groupKey,
            DisplayName = displayName,
            DisplayOrder = displayOrder,
        });
    }

    [Fact]
    public async Task GetSettingsAsync_空表自动补齐三类段落()
    {
        using var ctx = CreateDbContext();
        SeedMachineGroup(ctx, ColdRollMachineGroupKeys.Roll5060, ColdRollMachineGroupKeys.Roll5060Display, 1);
        SeedMachineGroup(ctx, ColdRollMachineGroupKeys.Draw, ColdRollMachineGroupKeys.DrawDisplay, 2);
        // 启用工段含冷轧拔/检验/入库，应被扣除，只留酸洗、固溶两个普通工段
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx,
            new SectionInfoDto { SectionKey = SectionKeys.Pickle, SectionName = "酸洗", DisplayOrder = 1, IsEnabled = true },
            new SectionInfoDto { SectionKey = SectionKeys.Solution, SectionName = "固溶", DisplayOrder = 2, IsEnabled = true },
            new SectionInfoDto { SectionKey = SectionKeys.ColdRollDraw, SectionName = "冷轧拔", DisplayOrder = 3, IsEnabled = true },
            new SectionInfoDto { SectionKey = SectionKeys.Inspection, SectionName = "检验", DisplayOrder = 4, IsEnabled = true },
            new SectionInfoDto { SectionKey = SectionKeys.Warehouse, SectionName = "入库", DisplayOrder = 5, IsEnabled = true });

        var result = await svc.GetSettingsAsync();

        result.Should().HaveCount(6);   // 2 冷轧拔 + 2 普通工段 + 2 检验
        result.Select(x => x.ParagraphName).Should().Equal(
            ColdRollMachineGroupKeys.Roll5060Display,
            ColdRollMachineGroupKeys.DrawDisplay,
            "酸洗",
            "固溶",
            BatchPlanSectionTabs.RoughTubeInspection,
            BatchPlanSectionTabs.InProcessInspection);

        var cold = result[0];
        cold.CategoryType.Should().Be(ParagraphCategoryTypes.Cold);
        cold.ParagraphKey.Should().Be(ColdRollMachineGroupKeys.Roll5060);

        var section = result[2];
        section.CategoryType.Should().Be(ParagraphCategoryTypes.Section);
        section.ParagraphKey.Should().Be(SectionKeys.Pickle);

        var fixedRow = result[4];
        fixedRow.CategoryType.Should().Be(ParagraphCategoryTypes.Fixed);
        fixedRow.ParagraphKey.Should().Be(BatchPlanSectionTabs.RoughTubeInspection);

        result.Should().BeInAscendingOrder(x => x.DisplayOrder);
    }

    [Fact]
    public async Task GetSettingsAsync_多余段落与存量旧段落被清理()
    {
        using var ctx = CreateDbContext();
        SeedMachineGroup(ctx, ColdRollMachineGroupKeys.Roll5060, ColdRollMachineGroupKeys.Roll5060Display, 1);
        await ctx.SaveChangesAsync();

        // 预置：存量旧段落(CategoryType=null)、多余段落(Gone)、合法机台组段落(5060 仍在)
        ctx.SectionParagraphConfigs.AddRange(
            new SectionParagraphConfig { ParagraphName = "旧段落", ParagraphKey = null, CategoryType = null, DisplayOrder = 1 },
            new SectionParagraphConfig { ParagraphName = "不存在", ParagraphKey = "Gone", CategoryType = ParagraphCategoryTypes.Section, DisplayOrder = 2 },
            new SectionParagraphConfig { ParagraphName = ColdRollMachineGroupKeys.Roll5060Display, ParagraphKey = ColdRollMachineGroupKeys.Roll5060, CategoryType = ParagraphCategoryTypes.Cold, DisplayOrder = 99 });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetSettingsAsync();

        result.Should().HaveCount(3);   // 冷轧5060 + 荒管检 + 在制检
        result.Should().NotContain(x => x.CategoryType == null);
        result.Should().NotContain(x => x.ParagraphName == "不存在");
        result.Single(x => x.ParagraphKey == ColdRollMachineGroupKeys.Roll5060).DisplayOrder.Should().Be(1);
    }

    [Fact]
    public async Task GetSettingsAsync_机台组显示名变更联动更新段落名与顺序()
    {
        using var ctx = CreateDbContext();
        SeedMachineGroup(ctx, ColdRollMachineGroupKeys.Roll5060, "冷轧5060新名", 3);
        await ctx.SaveChangesAsync();

        ctx.SectionParagraphConfigs.Add(new SectionParagraphConfig
        {
            ParagraphName = "冷轧5060旧名",
            ParagraphKey = ColdRollMachineGroupKeys.Roll5060,
            CategoryType = ParagraphCategoryTypes.Cold,
            DisplayOrder = 1,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetSettingsAsync();

        var cold = result.Single(x => x.ParagraphKey == ColdRollMachineGroupKeys.Roll5060);
        cold.ParagraphName.Should().Be("冷轧5060新名");
        cold.DisplayOrder.Should().Be(1);
    }

    [Fact]
    public async Task SaveSettingAsync_仅更新参数不动段落名()
    {
        using var ctx = CreateDbContext();
        SeedMachineGroup(ctx, ColdRollMachineGroupKeys.Roll5060, ColdRollMachineGroupKeys.Roll5060Display, 1);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var settings = await svc.GetSettingsAsync();   // 触发同步生成
        var cold = settings.Single(x => x.ParagraphKey == ColdRollMachineGroupKeys.Roll5060);

        var saved = await svc.SaveSettingAsync(new SectionParagraphConfigDto
        {
            Id = cold.Id,
            ParagraphName = "手改名",
            ParagraphKey = "手改Key",
            CategoryType = "Hack",
            DailyFlowTarget = 13m,
            LowerLimitDays = 3m,
            UpperLimitDays = 6m,
            Remark = "备注",
        });

        saved.Should().BeTrue();

        var entity = await ctx.SectionParagraphConfigs.FindAsync(cold.Id);
        entity!.ParagraphName.Should().Be(ColdRollMachineGroupKeys.Roll5060Display);  // 段落名不被手改
        entity.ParagraphKey.Should().Be(ColdRollMachineGroupKeys.Roll5060);
        entity.CategoryType.Should().Be(ParagraphCategoryTypes.Cold);
        entity.DailyFlowTarget.Should().Be(13m);
        entity.LowerLimitDays.Should().Be(3m);
        entity.UpperLimitDays.Should().Be(6m);
        entity.Remark.Should().Be("备注");
    }
}
