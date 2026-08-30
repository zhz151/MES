using FluentAssertions;
using MES.Core.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Scheduling;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.Scheduling;
using MES.Data;
using MES.Data.Entities.Scheduling;
using MES.Services.Scheduling;
using MES.Tests.Tests;
using Moq;

namespace MES.Tests.Services.Scheduling;

/// <summary>
/// 段落流转分析服务测试：段落由 3 类配置驱动自动生成，聚合按段落类别直接匹配待在产三维行
/// （冷轧拔=机台组内工序集且工段=冷轧拔 / 普通工段=工段命中 / 检验=检验工段按荒管在制产类划分）。
/// </summary>
public class SectionParagraphFlowAnalysisServiceTests : TestBase
{
    private SectionParagraphFlowAnalysisService CreateService(
        AppDbContext ctx,
        List<SectionProductionStatusDto>? statusRows = null,
        List<SectionParagraphConfigDto>? paragraphs = null)
    {
        var statusMock = new Mock<ISectionProductionStatusService>();
        statusMock.Setup(x => x.GetStatusAsync())
            .ReturnsAsync(statusRows ?? new List<SectionProductionStatusDto>());

        var paragraphMock = new Mock<ISectionParagraphConfigService>();
        paragraphMock.Setup(x => x.GetSettingsAsync())
            .ReturnsAsync(paragraphs ?? new List<SectionParagraphConfigDto>());

        var processDefMock = CreateProcessDefinitionServiceMock();

        return new SectionParagraphFlowAnalysisService(ctx, statusMock.Object, paragraphMock.Object, processDefMock);
    }

    private void SeedMachineGroup(AppDbContext ctx, string groupKey, string displayName, string processKeys, int displayOrder)
    {
        ctx.ColdRollMachineGroupConfigs.Add(new ColdRollMachineGroupConfig
        {
            GroupKey = groupKey,
            DisplayName = displayName,
            ProcessKeys = processKeys,
            DisplayOrder = displayOrder,
        });
    }

    [Fact]
    public async Task GetAnalysisAsync_冷轧拔按工段限定匹配_机台组内非冷轧拔批次归普通工段()
    {
        using var ctx = CreateDbContext();
        SeedMachineGroup(ctx, "5060", "冷轧5060", $"{ProcessKeys.ColdRoll50},{ProcessKeys.ColdRoll60}", 1);
        SeedMachineGroup(ctx, "Draw", "冷拔", ProcessKeys.ColdDraw, 2);
        await ctx.SaveChangesAsync();

        var statusRows = new List<SectionProductionStatusDto>
        {
            // 工序在 5060 组内 且 工段=冷轧拔 → 命中冷轧拔「5060」段落（与批次计划冷轧 Tab 同口径）
            new() { ProcessGroupName = ProcessKeys.ColdRoll60, SectionName = SectionKeys.ColdRollDraw, ProductStatus = ProductStatuses.InProgress, Total = 2500m },
            // 工序在 5060 组内但工段=酸洗：冷轧拔段落按工段限定不吸收 → 归普通工段「酸洗」，不丢失
            new() { ProcessGroupName = ProcessKeys.ColdRoll60, SectionName = SectionKeys.Pickle, ProductStatus = ProductStatuses.InProgress, Total = 3000m },
            // 工序不属于任何冷轧拔机台组 → 命中普通工段「酸洗」
            new() { ProcessGroupName = ProcessKeys.RoughTubeProcessing, SectionName = SectionKeys.Pickle, ProductStatus = ProductStatuses.InProgress, Total = 4000m },
        };

        var paragraphs = new List<SectionParagraphConfigDto>
        {
            new() { Id = 1, CategoryType = ParagraphCategoryTypes.Cold, ParagraphKey = "5060", ParagraphName = "冷轧5060", DailyFlowTarget = 13m, LowerLimitDays = 3m, UpperLimitDays = 6m },
            new() { Id = 2, CategoryType = ParagraphCategoryTypes.Section, ParagraphKey = SectionKeys.Pickle, ParagraphName = "酸洗", DailyFlowTarget = 40m, LowerLimitDays = 0.5m, UpperLimitDays = 2m },
        };

        var svc = CreateService(ctx, statusRows, paragraphs);
        var result = await svc.GetAnalysisAsync();

        result.Should().HaveCount(2);
        var cold = result.Single(x => x.ParagraphName == "冷轧5060");
        cold.PendingTotal.Should().Be(2.5m);   // 2500/1000 = 2.5（仅冷轧拔工段行，精确吨值）
        var section = result.Single(x => x.ParagraphName == "酸洗");
        section.PendingTotal.Should().Be(7m);  // (3000+4000)/1000 = 7（机台组内非冷轧拔行归普通工段）
    }

    [Fact]
    public async Task GetAnalysisAsync_检验固定段落按荒管在制产类划分()
    {
        using var ctx = CreateDbContext();
        await ctx.SaveChangesAsync();

        var statusRows = new List<SectionProductionStatusDto>
        {
            new() { ProcessGroupName = ProcessKeys.RoughTubeProcessing, SectionName = SectionKeys.Inspection, ProductStatus = ProductStatuses.RoughTube, Total = 5000m },
            new() { ProcessGroupName = ProcessKeys.RoughTubeProcessing, SectionName = SectionKeys.Inspection, ProductStatus = ProductStatuses.InProgress, Total = 6000m },
            // 成品产类不匹配任何固定检验段落
            new() { ProcessGroupName = ProcessKeys.RoughTubeProcessing, SectionName = SectionKeys.Inspection, ProductStatus = ProductStatuses.Finished, Total = 7000m },
        };

        var paragraphs = new List<SectionParagraphConfigDto>
        {
            new() { Id = 1, CategoryType = ParagraphCategoryTypes.Fixed, ParagraphKey = BatchPlanSectionTabs.RoughTubeInspection, ParagraphName = BatchPlanSectionTabs.RoughTubeInspection, DailyFlowTarget = 12m, LowerLimitDays = 0.5m, UpperLimitDays = 2m },
            new() { Id = 2, CategoryType = ParagraphCategoryTypes.Fixed, ParagraphKey = BatchPlanSectionTabs.InProcessInspection, ParagraphName = BatchPlanSectionTabs.InProcessInspection, DailyFlowTarget = 12m, LowerLimitDays = 0.5m, UpperLimitDays = 2m },
        };

        var svc = CreateService(ctx, statusRows, paragraphs);
        var result = await svc.GetAnalysisAsync();

        result.Should().HaveCount(2);
        result.Single(x => x.ParagraphName == BatchPlanSectionTabs.RoughTubeInspection).PendingTotal.Should().Be(5m);
        result.Single(x => x.ParagraphName == BatchPlanSectionTabs.InProcessInspection).PendingTotal.Should().Be(6m);
    }

    [Fact]
    public async Task GetAnalysisAsync_可持续天数在上限时判定过多()
    {
        using var ctx = CreateDbContext();
        await ctx.SaveChangesAsync();

        var statusRows = new List<SectionProductionStatusDto>
        {
            new() { ProcessGroupName = ProcessKeys.RoughTubeProcessing, SectionName = SectionKeys.Pickle, ProductStatus = ProductStatuses.InProgress, Total = 100000m },
        };

        var paragraphs = new List<SectionParagraphConfigDto>
        {
            new() { Id = 1, CategoryType = ParagraphCategoryTypes.Section, ParagraphKey = SectionKeys.Pickle, ParagraphName = "酸洗", DailyFlowTarget = 40m, LowerLimitDays = 0.5m, UpperLimitDays = 2m },
        };

        var svc = CreateService(ctx, statusRows, paragraphs);
        var result = await svc.GetAnalysisAsync();

        var row = result.Single(x => x.ParagraphName == "酸洗");
        row.PendingTotal.Should().Be(100);       // 100000/1000 = 100
        row.SustainableDays.Should().Be(2.5m);   // Round(100/40, 1) = 2.5
        row.StatusJudgment.Should().Be("过多");    // 2.5 > 2
    }
}
