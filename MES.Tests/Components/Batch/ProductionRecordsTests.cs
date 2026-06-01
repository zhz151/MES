using Bunit;
using FluentAssertions;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Blazor.Pages.Batches;
using MES.Blazor.Services;

namespace MES.Tests.Components;

public class ProductionRecordsTests : TestBase
{
    public ProductionRecordsTests()
    {
        RegisterServices(typeof(ProductionRecordService));
        ConfigureEmptyResponse("/api/production-record/all/records");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<ProductionRecords>();
        cut.Markup.Should().Contain("生产记录");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<ProductionRecords>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Theory]
    [InlineData(true, "成品")]
    [InlineData(false, "在制品")]
    public void StatusColumn_DisplaysCorrectText(bool isFinished, string expectedText)
    {
        ConfigureListResponse(isFinished);
        var cut = Ctx.RenderComponent<ProductionRecords>();
        cut.WaitForState(() => cut.Markup.Contains(expectedText));
        cut.Markup.Should().Contain(expectedText);
    }

    private void ConfigureListResponse(bool isFinished)
    {
        ConfigureEmptyResponse("/api/production-record/all/records");
        var pagedResult = new PagedResult<ProductionRecordDto>
        {
            Items = new List<ProductionRecordDto>
            {
                new()
                {
                    Id = 1,
                    ProductionBatchId = 1,
                    ProcessGroupId = 1,
                    ProcessName = "60冷轧",
                    SectionName = "冷轧车间",
                    SequenceNumber = 1,
                    ExecDate = DateTime.Today,
                    IsFinished = isFinished
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/production-record/all/records", new ApiResponse<PagedResult<ProductionRecordDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
