using Bunit;
using FluentAssertions;
using MES.Core.Models;
using MES.Blazor.Pages.Quality;
using MES.Blazor.Services;
using MES.Core.DTOs.Quality;

namespace MES.Tests.Components;

public class MaterialReceiveChecksTests : TestBase
{
    public MaterialReceiveChecksTests()
    {
        RegisterServices(typeof(MaterialReceiveCheckService), typeof(BatchService));
        ConfigureEmptyResponse("/api/material-receive-check/all");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<MaterialReceiveChecks>();
        cut.Markup.Should().Contain("成检到料");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<MaterialReceiveChecks>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Fact]
    public void Render_DisplaysData()
    {
        ConfigureListResponse();
        var cut = Ctx.RenderComponent<MaterialReceiveChecks>();
        cut.WaitForState(() => cut.Markup.Contains("BATCH-MC-001"));
        cut.Markup.Should().Contain("BATCH-MC-001");
    }

    private void ConfigureListResponse()
    {
        ConfigureEmptyResponse("/api/material-receive-check/all");
        var pagedResult = new PagedResult<MaterialReceiveCheckDto>
        {
            Items = new List<MaterialReceiveCheckDto>
            {
                new()
                {
                    Id = 1,
                    ProductionBatchId = 1,
                    BatchNo = "BATCH-MC-001",
                    ReceiveDate = DateTime.Today,
                    Checker = "张三"
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/material-receive-check/all", new ApiResponse<PagedResult<MaterialReceiveCheckDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
