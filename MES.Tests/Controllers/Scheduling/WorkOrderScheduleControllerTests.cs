using Microsoft.AspNetCore.Mvc;
using Moq;
using MES.Api.Controllers.Scheduling;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Tests.Tests;

namespace MES.Tests.Controllers.Scheduling;

public class WorkOrderScheduleControllerTests : ControllerTestBase
{
    private readonly Mock<IWorkOrderScheduleService> _serviceMock;
    private readonly WorkOrderScheduleController _controller;

    public WorkOrderScheduleControllerTests()
    {
        _serviceMock = new Mock<IWorkOrderScheduleService>();
        _controller = new WorkOrderScheduleController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        var pagedResult = new PagedResult<WorkOrderScheduleDto>
        {
            Items = new List<WorkOrderScheduleDto> { new() { Id = 1, WorkOrderNo = "WO001" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        var result = await _controller.GetPaged();

        var (_, response) = AssertOk<ApiResponse<PagedResult<WorkOrderScheduleDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetFilterContexts_ReturnsOk()
    {
        var ctx = new Dictionary<string, List<string>> { ["Field1"] = new() { "A" } };
        _serviceMock.Setup(x => x.GetFilterContextsAsync()).ReturnsAsync(ctx);

        var result = await _controller.GetFilterContexts();

        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetFilterContexts_Empty_ReturnsEmpty()
    {
        _serviceMock.Setup(x => x.GetFilterContextsAsync()).ReturnsAsync(new Dictionary<string, List<string>>());

        var result = await _controller.GetFilterContexts();

        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
        Assert.Empty(response.Data!);
    }

    [Fact]
    public async Task GetPaged_PassesKeyword_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<WorkOrderScheduleDto> { Items = new List<WorkOrderScheduleDto>() });

        await _controller.GetPaged(keyword: "测试");

        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.Keyword == "测试")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_LimitsPageSize()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<WorkOrderScheduleDto> { Items = new List<WorkOrderScheduleDto>() });

        await _controller.GetPaged(pageSize: 9999);

        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesFilters_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<WorkOrderScheduleDto> { Items = new List<WorkOrderScheduleDto>() });

        var filtersJson = "[{\"Field\":\"ScheduleStage\",\"Value\":\"2\"}]";
        await _controller.GetPaged(filters: filtersJson);

        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.Filters != null && q.Filters.Count > 0)), Times.Once);
    }
}
