using Bunit;
using FluentAssertions;
using MES.Core.Models;
using MES.Blazor.Pages.Batches;
using MES.Blazor.Services;
using MES.Core.DTOs.Batch;
using MES.Core.Enums;

namespace MES.Tests.Components;

public class ProcessCardPrintTests : TestBase
{
    public ProcessCardPrintTests()
    {
        RegisterServices(typeof(BatchService), typeof(StandardWorkDayService));
        ConfigureEmptyResponse("/api/batch/list");
        // 工段列从参数表加载；未配置时 fallback 为预置 26 工段，此处配置空响应触发降级路径
        ConfigureEmptyResponse("/api/standard-work-day/enabled-sections");
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
                    Status = BatchStatus.None,
                    WorkOrderNo = "WO001",
                    ManufacturingItem = MaterialType.OrderFinished,
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
