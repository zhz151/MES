using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class ProductionStandardControllerTests : ControllerTestBase
{
    private readonly Mock<IProductionStandardService> _serviceMock;
    private readonly ProductionStandardController _controller;

    public ProductionStandardControllerTests()
    {
        _serviceMock = new Mock<IProductionStandardService>();
        _controller = new ProductionStandardController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<ProductionStandardDto>
        {
            Items = new List<ProductionStandardDto> { new() { Id = 1, StandardName = "测试标准" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<bool?>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged(pageIndex: 1, pageSize: 20);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<ProductionStandardDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new ProductionStandardDto { Id = 1, StandardName = "测试标准" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ProductionStandardDto>>(result);
        Assert.Equal("测试标准", response.Data?.StandardName);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        // Arrange
        var request = new CreateProductionStandardRequest { StandardName = "新标准" };
        var dto = new ProductionStandardDto { Id = 1, StandardName = "新标准" };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ProductionStandardDto>>(result);
        Assert.Equal("新标准", response.Data?.StandardName);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Create(new CreateProductionStandardRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<ProductionStandardDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateProductionStandardRequest { StandardName = "更新名称" };
        var dto = new ProductionStandardDto { Id = 1, StandardName = "更新名称" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ProductionStandardDto>>(result);
        Assert.Equal("更新名称", response.Data?.StandardName);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Update(1, new UpdateProductionStandardRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<ProductionStandardDto>>(result);
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
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        // Arrange
        var standards = new List<ProductionStandardDto>
        {
            new() { Id = 1, StandardName = "标准1" },
            new() { Id = 2, StandardName = "标准2" }
        };
        _serviceMock.Setup(x => x.GetAllAsync(true)).ReturnsAsync(standards);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<ProductionStandardDto>>>(result);
        Assert.Equal(2, response.Data?.Count);
    }

    [Fact]
    public async Task PrintStandard_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintStandardAsync(1)).ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintStandard(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintStandardBatch_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintStandardBatch(new OrderPrintBatchRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintStandardBatch_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintStandardBatchAsync(It.IsAny<int[]>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintStandardBatch(new OrderPrintBatchRequest { Ids = new[] { 1, 2 } });

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintStandardAll_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintStandardAll(new OrderPrintAllRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintStandardAll_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintStandardAllAsync(It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintStandardAll(new OrderPrintAllRequest { Keyword = "标准" });

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task GetFilterContexts_ReturnsOk()
    {
        // Arrange
        var ctx = new Dictionary<string, List<string>> { ["StandardName"] = new() { "测试标准" } };
        _serviceMock.Setup(x => x.GetFilterContextsAsync()).ReturnsAsync(ctx);

        // Act
        var result = await _controller.GetFilterContexts();

        // Assert
        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!);
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

    [Fact]
    public async Task GetPaged_LimitsPageSize()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<bool?>()))
            .ReturnsAsync(new PagedResult<ProductionStandardDto> { Items = new List<ProductionStandardDto>() });
        var result = await _controller.GetPaged(pageSize: 9999);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.PageSize == 5000), It.IsAny<bool?>()), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesKeyword_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<bool?>()))
            .ReturnsAsync(new PagedResult<ProductionStandardDto> { Items = new List<ProductionStandardDto>() });
        await _controller.GetPaged(keyword: "测试");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.Keyword == "测试"), It.IsAny<bool?>()), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesSortBy_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<bool?>()))
            .ReturnsAsync(new PagedResult<ProductionStandardDto> { Items = new List<ProductionStandardDto>() });
        await _controller.GetPaged(sortBy: "StandardName");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.SortBy == "StandardName"), It.IsAny<bool?>()), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesFilters_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<bool?>()))
            .ReturnsAsync(new PagedResult<ProductionStandardDto> { Items = new List<ProductionStandardDto>() });
        var filtersJson = "[{\"Field\":\"StandardName\",\"Operator\":\"equals\",\"Value\":\"测试标准\"}]";
        await _controller.GetPaged(filters: filtersJson);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.Filters != null && q.Filters.Count > 0), It.IsAny<bool?>()), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesIsActive_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<bool?>()))
            .ReturnsAsync(new PagedResult<ProductionStandardDto> { Items = new List<ProductionStandardDto>() });
        await _controller.GetPaged(isActive: true);
        _serviceMock.Verify(x => x.GetPagedAsync(It.IsAny<QueryParams>(), true), Times.Once);
    }
}
