using FluentAssertions;
using MES.Core.DTOs;
using MES.Core.Models;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using MES.Blazor.Pages;
using MES.Blazor.Services;

namespace MES.Tests.Components;

public class EquipmentsTests : TestBase
{
    public EquipmentsTests()
    {
        RegisterServices(typeof(EquipmentService));
        ConfigureEmptyResponse("/api/equipment/list");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<CascadingAuthenticationState>(p => p.AddChildContent<Equipments>());
        cut.Markup.Should().Contain("设备台账");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<CascadingAuthenticationState>(p => p.AddChildContent<Equipments>());
        cut.Markup.Should().Contain("生命周期");
    }

    [Theory]
    [InlineData("Active", "在用")]
    [InlineData("Standby", "备用")]
    [InlineData("Scrapped", "报废")]
    public void LifecycleStatus_DisplaysCorrectText(string status, string expectedText)
    {
        ConfigureLifecycleResponse(status);
        var cut = Ctx.RenderComponent<CascadingAuthenticationState>(p => p.AddChildContent<Equipments>());
        cut.WaitForState(() => cut.Markup.Contains(expectedText));
        cut.Markup.Should().Contain(expectedText);
    }

    private void ConfigureLifecycleResponse(string lifecycleStatus)
    {
        ConfigureEmptyResponse("/api/equipment/list");
        var pagedResult = new PagedResult<EquipmentListDto>
        {
            Items = new List<EquipmentListDto>
            {
                new()
                {
                    Id = 1,
                    EquipmentCode = "EQ001",
                    EquipmentName = "测试设备",
                    LifecycleStatus = lifecycleStatus,
                    UsageType = "Primary",
                    RunningStatus = "Normal",
                    InspectionStatus = "Normal",
                    MaintStatus = "Normal",
                    Location = "车间A"
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/equipment/list", new ApiResponse<PagedResult<EquipmentListDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
