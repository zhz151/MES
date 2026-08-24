using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.Materials;
using MES.Core.Models;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.Interfaces.Materials;

namespace MES.Tests.Controllers;

public class SupplierControllerTests : ControllerTestBase
{
    private readonly Mock<ISupplierService> _serviceMock;
    private readonly Mock<ILogger<SupplierController>> _loggerMock;
    private readonly SupplierController _controller;

    public SupplierControllerTests()
    {
        _serviceMock = new Mock<ISupplierService>();
        _loggerMock = CreateLoggerMock<SupplierController>();
        _controller = new SupplierController(_serviceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<SupplierProfileDto>
        {
            Items = new List<SupplierProfileDto> { new() { Id = 1, SupplierName = "测试供应商" } },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged();

        // Assert
        var (okResult, response) = AssertOk<ApiResponse<PagedResult<SupplierProfileDto>>>(result);
        Assert.True(response.Success);
        Assert.Equal(1, response.Data?.TotalCount);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetPaged_LimitsPageSize()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<SupplierProfileDto> { Items = new List<SupplierProfileDto>() });

        // Act
        var result = await _controller.GetPaged(pageSize: 10000);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<SupplierProfileDto>>>(result);
        Assert.True(response.Success);

        // 验证 service 收到的是 5000 而不是 10000
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new SupplierProfileDto { Id = 1, SupplierName = "测试供应商" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<SupplierProfileDto>>(result);
        Assert.True(response.Success);
        Assert.Equal("测试供应商", response.Data?.SupplierName);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        // Arrange
        var request = new CreateSupplierRequest { SupplierName = "新供应商" };
        var dto = new SupplierProfileDto { Id = 1, SupplierName = "新供应商" };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<SupplierProfileDto>>(result);
        Assert.True(response.Success);
        Assert.Equal("新供应商", response.Data?.SupplierName);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);
        var request = new CreateSupplierRequest();

        // Act
        var result = await _controller.Create(request);

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<SupplierProfileDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateSupplierRequest { SupplierName = "更新名称" };
        var dto = new SupplierProfileDto { Id = 1, SupplierName = "更新名称" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<SupplierProfileDto>>(result);
        Assert.True(response.Success);
        Assert.Equal("更新名称", response.Data?.SupplierName);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);
        var request = new UpdateSupplierRequest();

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<SupplierProfileDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Delete_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task GetActive_ReturnsOk()
    {
        // Arrange
        var suppliers = new List<SupplierProfileDto>
        {
            new() { Id = 1, SupplierName = "活跃供应商" }
        };
        _serviceMock.Setup(x => x.GetActiveAsync()).ReturnsAsync(suppliers);

        // Act
        var result = await _controller.GetActive();

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<SupplierProfileDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task CreateBatch_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreateSupplierRequest> { new() { SupplierName = "批量供应商" } };
        var dtos = new List<SupplierProfileDto> { new() { Id = 1, SupplierName = "批量供应商" } };
        _serviceMock.Setup(x => x.CreateBatchAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.CreateBatch(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<SupplierProfileDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task CreateBatch_ReturnsBadRequest_WhenEmpty()
    {
        // Arrange
        var emptyRequests = new List<CreateSupplierRequest>();

        // Act
        var result = await _controller.CreateBatch(emptyRequests);

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<SupplierProfileDto>>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetFilterContexts_ReturnsOk()
    {
        // Arrange
        var filterContexts = new Dictionary<string, List<string>>
        {
            ["Field1"] = new() { "A", "B" }
        };
        _serviceMock.Setup(x => x.GetFilterContextsAsync()).ReturnsAsync(filterContexts);

        // Act
        var result = await _controller.GetFilterContexts();

        // Assert
        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetPaged_PassesKeyword_ToService()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<SupplierProfileDto> { Items = new List<SupplierProfileDto>() });

        // Act
        await _controller.GetPaged(keyword: "测试搜索");

        // Assert
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.Keyword == "测试搜索")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesSortBy_ToService()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<SupplierProfileDto> { Items = new List<SupplierProfileDto>() });

        // Act
        await _controller.GetPaged(sortBy: "SupplierName", isDescending: false);

        // Assert
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.SortBy == "SupplierName" && q.IsDescending == false)), Times.Once);
    }

    [Fact]
    public async Task GetPaged_DefaultSortBy_IsCreatedTime()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<SupplierProfileDto> { Items = new List<SupplierProfileDto>() });

        // Act
        await _controller.GetPaged();

        // Assert
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.SortBy == "CreatedTime")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesFiltersJson_ToService()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<SupplierProfileDto> { Items = new List<SupplierProfileDto>() });

        var filtersJson = "[{\"Field\":\"SupplierName\",\"Operator\":\"contains\",\"Value\":\"test\"}]";

        // Act
        await _controller.GetPaged(filters: filtersJson);

        // Assert
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q =>
            q.Filters != null && q.Filters.Count == 1 && q.Filters[0].Field == "SupplierName")), Times.Once);
    }

    [Fact]
    public async Task GetFilterContexts_Empty_ReturnsEmpty()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetFilterContextsAsync()).ReturnsAsync(new Dictionary<string, List<string>>());

        // Act
        var result = await _controller.GetFilterContexts();

        // Assert
        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
        Assert.Empty(response.Data!);
    }
}
