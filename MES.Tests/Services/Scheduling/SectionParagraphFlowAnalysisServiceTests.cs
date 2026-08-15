using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Scheduling;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.Scheduling;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Scheduling;
using MES.Tests.Tests;
using Moq;

namespace MES.Tests.Services.Scheduling;

/// <summary>
/// 段落流转分析服务测试：按组合归类表「归属段落」上卷聚合待在产重量，结合段落日产配置判定偏少/正常/过多。
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

    [Fact]
    public async Task GetAnalysisAsync_按归属段落聚合待在产重量并判定状态()
    {
        using var ctx = CreateDbContext();
        ctx.CombinationGroups.Add(new CombinationGroup
        {
            ProcessGroupName = ProcessKeys.RoughTubeProcessing,
            SectionName = SectionKeys.Pickle,
            ProductStatus = ProductStatuses.InProgress,
            ParagraphName = "酸洗",
        });
        ctx.CombinationGroups.Add(new CombinationGroup
        {
            ProcessGroupName = ProcessKeys.RoughTubeProcessing,
            SectionName = SectionKeys.Pickle,
            ProductStatus = ProductStatuses.Finished,
            ParagraphName = "酸洗",
        });
        // 未归属段落行应被跳过（ParagraphName 为空）
        ctx.CombinationGroups.Add(new CombinationGroup
        {
            ProcessGroupName = ProcessKeys.RoughTubeProcessing,
            SectionName = SectionKeys.Pickle,
            ProductStatus = ProductStatuses.RoughTube,
            ParagraphName = null,
        });
        await ctx.SaveChangesAsync();

        var statusRows = new List<SectionProductionStatusDto>
        {
            new() { ProcessGroupName = ProcessKeys.RoughTubeProcessing, SectionName = SectionKeys.Pickle, ProductStatus = ProductStatuses.InProgress, Total = 2500m },
            new() { ProcessGroupName = ProcessKeys.RoughTubeProcessing, SectionName = SectionKeys.Pickle, ProductStatus = ProductStatuses.Finished, Total = 3000m },
            // 不匹配任何组合行（工序组不同），不应计入
            new() { ProcessGroupName = ProcessKeys.ColdRoll60, SectionName = SectionKeys.Pickle, ProductStatus = ProductStatuses.InProgress, Total = 9000m },
        };

        var paragraphs = new List<SectionParagraphConfigDto>
        {
            new() { Id = 1, DisplayOrder = 1, ParagraphName = "酸洗", DailyFlowTarget = 40m, LowerLimitDays = 0.5m, UpperLimitDays = 2m },
        };

        var svc = CreateService(ctx, statusRows, paragraphs);
        var result = await svc.GetAnalysisAsync();

        result.Should().HaveCount(1);
        var row = result.Single(x => x.ParagraphName == "酸洗");
        row.PendingTotal.Should().Be(6);          // Round((2500+3000)/1000, 0) = 6
        row.VariationTotal.Should().Be(6);
        row.DailyFlowTarget.Should().Be(40m);
        row.SustainableDays.Should().Be(0.2m);    // Round(6/40, 1) = 0.2
        row.StatusJudgment.Should().Be("偏少");    // 0.2 < 0.5
        row.KeyBatchCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAnalysisAsync_通配组合行匹配多工序组并上卷到段落()
    {
        using var ctx = CreateDbContext();
        ctx.CombinationGroups.Add(new CombinationGroup
        {
            ProcessGroupName = CombinationWildcards.All,
            SectionName = SectionKeys.Cut,
            ProductStatus = ProductStatuses.RoughTube,
            ParagraphName = "切割",
        });
        ctx.CombinationGroups.Add(new CombinationGroup
        {
            ProcessGroupName = CombinationWildcards.All,
            SectionName = SectionKeys.Pickle,
            ProductStatus = ProductStatuses.RoughTube,
            ParagraphName = "酸洗",
        });
        await ctx.SaveChangesAsync();

        var statusRows = new List<SectionProductionStatusDto>
        {
            new() { ProcessGroupName = ProcessKeys.RoughTubeProcessing, SectionName = SectionKeys.Cut, ProductStatus = ProductStatuses.RoughTube, Total = 1200m },
            new() { ProcessGroupName = ProcessKeys.ColdRoll60, SectionName = SectionKeys.Cut, ProductStatus = ProductStatuses.RoughTube, Total = 800m },
            new() { ProcessGroupName = ProcessKeys.RoughTubeProcessing, SectionName = SectionKeys.Pickle, ProductStatus = ProductStatuses.RoughTube, Total = 4000m },
        };

        var paragraphs = new List<SectionParagraphConfigDto>
        {
            new() { Id = 1, DisplayOrder = 1, ParagraphName = "切割", DailyFlowTarget = 50m, LowerLimitDays = 0.5m, UpperLimitDays = 2m },
            new() { Id = 2, DisplayOrder = 2, ParagraphName = "酸洗", DailyFlowTarget = 40m, LowerLimitDays = 0.5m, UpperLimitDays = 2m },
        };

        var svc = CreateService(ctx, statusRows, paragraphs);
        var result = await svc.GetAnalysisAsync();

        result.Should().HaveCount(2);
        result.Single(x => x.ParagraphName == "切割").PendingTotal.Should().Be(2); // Round(2000/1000, 0)
        result.Single(x => x.ParagraphName == "酸洗").PendingTotal.Should().Be(4); // Round(4000/1000, 0)
    }

    [Fact]
    public async Task GetAnalysisAsync_可持续天数在上限时判定过多()
    {
        using var ctx = CreateDbContext();
        ctx.CombinationGroups.Add(new CombinationGroup
        {
            ProcessGroupName = CombinationWildcards.All,
            SectionName = SectionKeys.Pickle,
            ProductStatus = ProductStatuses.AllStatus,
            ParagraphName = "酸洗",
        });
        await ctx.SaveChangesAsync();

        var statusRows = new List<SectionProductionStatusDto>
        {
            new() { ProcessGroupName = ProcessKeys.RoughTubeProcessing, SectionName = SectionKeys.Pickle, ProductStatus = ProductStatuses.InProgress, Total = 100000m },
        };

        var paragraphs = new List<SectionParagraphConfigDto>
        {
            new() { Id = 1, DisplayOrder = 1, ParagraphName = "酸洗", DailyFlowTarget = 40m, LowerLimitDays = 0.5m, UpperLimitDays = 2m },
        };

        var svc = CreateService(ctx, statusRows, paragraphs);
        var result = await svc.GetAnalysisAsync();

        var row = result.Single(x => x.ParagraphName == "酸洗");
        row.PendingTotal.Should().Be(100);       // Round(100000/1000, 0) = 100
        row.SustainableDays.Should().Be(2.5m);   // Round(100/40, 1) = 2.5
        row.StatusJudgment.Should().Be("过多");    // 2.5 > 2
    }
}
