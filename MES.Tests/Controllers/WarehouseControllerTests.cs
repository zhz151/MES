using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class WarehouseControllerTests : ControllerTestBase
{
    private readonly Mock<IWarehouseService> _serviceMock;
    private readonly WarehouseController _controller;

    public WarehouseControllerTests()
    {
        _serviceMock = new Mock<IWarehouseService>();
        _controller = new WarehouseController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<WarehouseDto>
        {
            Items = new List<WarehouseDto> { new() { Id = 1, Name = "成品库" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>(), It.IsAny<bool?>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged(new QueryParams());

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<WarehouseDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        // Arrange
        var list = new List<WarehouseDto> { new() { Id = 1, Name = "成品库" } };
        _serviceMock.Setup(x => x.GetAllAsync(true)).ReturnsAsync(list);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<WarehouseDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new WarehouseDto { Id = 1, Name = "成品库" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<WarehouseDto>>(result);
        Assert.Equal("成品库", response.Data?.Name);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        // Arrange
        var request = new CreateWarehouseRequest { Name = "新仓库" };
        var dto = new WarehouseDto { Id = 1, Name = "新仓库" };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<WarehouseDto>>(result);
        Assert.Equal("新仓库", response.Data?.Name);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Create(new CreateWarehouseRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<WarehouseDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateWarehouseRequest { Name = "更新名称" };
        var dto = new WarehouseDto { Id = 1, Name = "更新名称" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<WarehouseDto>>(result);
        Assert.Equal("更新名称", response.Data?.Name);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Update(1, new UpdateWarehouseRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<WarehouseDto>>(result);
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
}
