using Bunit;
using FluentAssertions;
using MES.Core.Models;
using MES.Blazor.Pages.Batches;
using MES.Blazor.Services;
using MES.Core.DTOs.Batch;
using MES.Core.Enums;

namespace MES.Tests.Components;

public class SectionOutsourcesTests : TestBase
{
    public SectionOutsourcesTests()
    {
        RegisterServices(typeof(SectionOutsourceService), typeof(ProductionRecordService));
        ConfigureEmptyResponse("/api/section-outsource/list");
        ConfigureEmptyResponse("/api/section-outsource/recoveries/filter-contexts");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<SectionOutsources>();
        cut.Markup.Should().Contain("工段委外");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<SectionOutsources>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Theory]
    [InlineData(SectionOutsourceStatus.PendingRecovery, "待回收")]
    [InlineData(SectionOutsourceStatus.Recovered, "已回收")]
    [InlineData(SectionOutsourceStatus.InProgress, "在轧")]
    public void StatusColumn_DisplaysCorrectText(SectionOutsourceStatus status, string expectedText)
    {
        ConfigureListResponse(status);
        var cut = Ctx.RenderComponent<SectionOutsources>();
        cut.WaitForState(() => cut.Markup.Contains(expectedText));
        cut.Markup.Should().Contain(expectedText);
    }

    private void ConfigureListResponse(SectionOutsourceStatus status)
    {
        ConfigureEmptyResponse("/api/section-outsource/list");
        ConfigureEmptyResponse("/api/section-outsource/recoveries/filter-contexts");
        var pagedResult = new PagedResult<SectionOutsourceDto>
        {
            Items = new List<SectionOutsourceDto>
            {
                new()
                {
                    Id = 1,
                    ProductionBatchId = 1,
                    ProcessGroupId = 1,
                    BatchNo = "BATCH001",
                    ProcessName = "60冷轧",
                    SectionName = "冷轧拔",
                    SequenceNumber = 1,
                    OutsourceVendor = "测试供应商",
                    SendOutDate = DateTime.Today,
                    Status = status
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/section-outsource/list", new ApiResponse<PagedResult<SectionOutsourceDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
