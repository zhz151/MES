using Bunit;
using FluentAssertions;
using MES.Core.Models;
using MES.Blazor.Pages.Batches;
using MES.Blazor.Services;
using MES.Core.DTOs.Batch;

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
        var cut = RenderPage<OutsourceRecoveries>();
        cut.Markup.Should().Contain("委外回收");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = RenderPage<OutsourceRecoveries>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Fact]
    public void Render_DisplaysData()
    {
        ConfigureListResponse();
        var cut = RenderPage<OutsourceRecoveries>();
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
                    ProcessName = "60冷轧"
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
