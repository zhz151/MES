using Bunit;
using FluentAssertions;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Blazor.Pages;
using MES.Blazor.Services;

namespace MES.Tests.Components;

public class OutsourceRecoveriesTests : TestBase
{
    public OutsourceRecoveriesTests()
    {
        RegisterServices(typeof(SectionOutsourceService));
        ConfigureEmptyResponse("/api/section-outsource/recoveries/list");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<OutsourceRecoveries>();
        cut.Markup.Should().Contain("委外回收");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<OutsourceRecoveries>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Fact]
    public void Render_DisplaysData()
    {
        ConfigureListResponse();
        var cut = Ctx.RenderComponent<OutsourceRecoveries>();
        cut.WaitForState(() => cut.Markup.Contains("BATCH-OR-001"));
        cut.Markup.Should().Contain("BATCH-OR-001");
    }

    private void ConfigureListResponse()
    {
        ConfigureEmptyResponse("/api/section-outsource/recoveries/list");
        var pagedResult = new PagedResult<OutsourceRecoveryDto>
        {
            Items = new List<OutsourceRecoveryDto>
            {
                new()
                {
                    Id = 1,
                    SectionOutsourceId = 1,
                    BatchNo = "BATCH-OR-001",
                    RecoveryDate = DateTime.Today,
                    RecoveryQuantity = 80,
                    RecoveryWeight = 3000m,
                    OutsourceVendor = "测试供应商",
                    ProcessName = "冷轧"
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/section-outsource/recoveries/list", new ApiResponse<PagedResult<OutsourceRecoveryDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
