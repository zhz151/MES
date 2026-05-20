using Bunit;
using FluentAssertions;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Blazor.Pages;
using MES.Blazor.Services;

namespace MES.Tests.Components;

public class ProcessCardPrintTests : TestBase
{
    public ProcessCardPrintTests()
    {
        RegisterServices(typeof(BatchService));
        ConfigureEmptyResponse("/api/batch/list");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<ProcessCardPrint>();
        cut.Markup.Should().Contain("工艺流转卡打印");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<ProcessCardPrint>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Fact]
    public void Render_DisplaysData()
    {
        ConfigureListResponse();
        var cut = Ctx.RenderComponent<ProcessCardPrint>();
        cut.WaitForState(() => cut.Markup.Contains("BATCH001"));
        cut.Markup.Should().Contain("BATCH001");
    }

    private void ConfigureListResponse()
    {
        ConfigureEmptyResponse("/api/batch/list");
        var pagedResult = new PagedResult<ProductionBatchListDto>
        {
            Items = new List<ProductionBatchListDto>
            {
                new()
                {
                    Id = 1,
                    BatchNo = "BATCH001",
                    Status = "None",
                    WorkOrderNo = "WO001",
                    ManufacturingItem = "订单成品",
                    PlantGrade = "304",
                    Specification = "219*8",
                    TotalWeight = 2500m,
                    ProductionRatio = 1
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/batch/list", new ApiResponse<PagedResult<ProductionBatchListDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
