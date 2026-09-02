using Bunit;
using FluentAssertions;
using MES.Core.Models;
using MES.Blazor.Pages.Batches;
using MES.Blazor.Services;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Scheduling;

namespace MES.Tests.Components;

public class ProductionRecordsTests : TestBase
{
    public ProductionRecordsTests()
    {
        RegisterServices(typeof(ProductionRecordService), typeof(BatchPlanService), typeof(EmployeeService));
        ConfigureEmptyResponse("/api/production-record/all/records");
        // 操作人下拉候选（EmployeeService）——返回空列表，避免渲染时 NRE
        ConfigureEmptyResponse("/api/employee/list");
        // 月度/近日生产量汇总（BatchPlanSvc）——返回空列表，避免渲染时 NRE
        ConfigureResponse("/api/batch-plan/summary", new ApiResponse<List<BatchPlanSummaryRowDto>>
        {
            Success = true, Code = 200, Data = new List<BatchPlanSummaryRowDto>()
        });
        ConfigureResponse("/api/batch-plan/monthly-summary", new ApiResponse<List<BatchPlanMonthlySummaryRowDto>>
        {
            Success = true, Code = 200, Data = new List<BatchPlanMonthlySummaryRowDto>()
        });
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = RenderPage<ProductionRecords>();
        cut.Markup.Should().Contain("生产记录");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = RenderPage<ProductionRecords>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Theory]
    [InlineData("成品")]
    [InlineData("荒管")]
    [InlineData("在制")]
    public void StatusColumn_DisplaysCorrectText(string productStatus)
    {
        ConfigureListResponse(productStatus);
        var cut = RenderPage<ProductionRecords>();
        cut.WaitForState(() => cut.Markup.Contains(productStatus));
        cut.Markup.Should().Contain(productStatus);
    }

    private void ConfigureListResponse(string productStatus)
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
                    ProductStatus = productStatus
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
