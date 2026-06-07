using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MES.Api.Controllers.Scheduling;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Tests.Tests;

namespace MES.Tests.Controllers.Scheduling;

public class ColdRollPlanControllerTests : ControllerTestBase
{
    private readonly Mock<IColdRollPlanService> _serviceMock;
    private readonly ColdRollPlanController _controller;

    public ColdRollPlanControllerTests()
    {
        _serviceMock = new Mock<IColdRollPlanService>();
        _controller = new ColdRollPlanController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetPlan_ReturnsOk()
    {
        var data = new List<ColdRollPlanRowDto>
        {
            new() { ProcessType = "60冷轧", RollingSpec = "219*8", WeightTotal = 1000m }
        };
        _serviceMock.Setup(x => x.GetPlanAsync(null)).ReturnsAsync(data);

        var result = await _controller.GetPlan(null);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("Success").GetBoolean());
        Assert.Single(doc.RootElement.GetProperty("Data").EnumerateArray());
    }

    [Fact]
    public async Task GetPlan_WithSectionFilter()
    {
        var data = new List<ColdRollPlanRowDto>
        {
            new() { ProcessType = "60冷轧", RollingSpec = "219*8", WeightTotal = 500m }
        };
        _serviceMock.Setup(x => x.GetPlanAsync("60冷轧")).ReturnsAsync(data);

        var result = await _controller.GetPlan("60冷轧");

        _serviceMock.Verify(x => x.GetPlanAsync("60冷轧"), Times.Once);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetPlan_Empty_ReturnsOk()
    {
        _serviceMock.Setup(x => x.GetPlanAsync(null)).ReturnsAsync(new List<ColdRollPlanRowDto>());

        var result = await _controller.GetPlan(null);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("Success").GetBoolean());
        Assert.Equal(0, doc.RootElement.GetProperty("Data").GetArrayLength());
    }
}
