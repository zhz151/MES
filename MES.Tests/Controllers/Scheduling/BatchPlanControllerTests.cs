using Microsoft.AspNetCore.Mvc;
using Moq;
using MES.Api.Controllers.Scheduling;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Tests.Tests;

namespace MES.Tests.Controllers.Scheduling;

public class BatchPlanControllerTests : ControllerTestBase
{
    private readonly Mock<IBatchPlanService> _serviceMock;
    private readonly Mock<IProductionRecordService> _prodRecordServiceMock;
    private readonly BatchPlanController _controller;

    public BatchPlanControllerTests()
    {
        _serviceMock = new Mock<IBatchPlanService>();
        _prodRecordServiceMock = new Mock<IProductionRecordService>();
        _controller = new BatchPlanController(_serviceMock.Object, _prodRecordServiceMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        var pagedResult = new PagedResult<BatchPlanDto>
        {
            Items = new List<BatchPlanDto> { new() { BatchNo = "B001" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20,
            Extras = new Dictionary<string, object> { ["batchCount"] = 1 }
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        var result = await _controller.GetPaged();

        var (_, response) = AssertOk<ApiResponse<PagedResult<BatchPlanDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetFilterContexts_ReturnsOk()
    {
        var ctx = new Dictionary<string, List<string>> { ["BatchNo"] = new() { "B001" } };
        _serviceMock.Setup(x => x.GetFilterContextsAsync()).ReturnsAsync(ctx);

        var result = await _controller.GetFilterContexts();

        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetPaged_PassesKeyword_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<BatchPlanDto> { Items = new List<BatchPlanDto>() });

        await _controller.GetPaged(keyword: "B001");

        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.Keyword == "B001")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_LimitsPageSize()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<BatchPlanDto> { Items = new List<BatchPlanDto>() });

        await _controller.GetPaged(pageSize: 9999);

        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesFilters_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<BatchPlanDto> { Items = new List<BatchPlanDto>() });

        var filtersJson = "[{\"Field\":\"BatchNo\",\"Value\":\"B001\"}]";
        await _controller.GetPaged(filters: filtersJson);

        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.Filters != null && q.Filters.Count > 0)), Times.Once);
    }
}
