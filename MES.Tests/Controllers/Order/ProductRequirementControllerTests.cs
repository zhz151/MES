using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.Order;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class ProductRequirementControllerTests : ControllerTestBase
{
    private readonly Mock<IProductRequirementService> _serviceMock;
    private readonly ProductRequirementController _controller;

    public ProductRequirementControllerTests()
    {
        _serviceMock = new Mock<IProductRequirementService>();
        _controller = new ProductRequirementController(_serviceMock.Object);
    }

    [Fact]
    public async Task Get_ReturnsOk()
    {
        // Arrange
        var dto = new ProductRequirementDto { Id = 1, RequirementType = RequirementType.Normal };
        _serviceMock.Setup(x => x.GetByOrderItemIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Get(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ProductRequirementDto>>(result);
        Assert.Equal(RequirementType.Normal, response.Data?.RequirementType);
    }

    [Fact]
    public async Task Get_ReturnsEmptyDto_WhenNotFound()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetByOrderItemIdAsync(999)).ReturnsAsync((ProductRequirementDto?)null);

        // Act
        var result = await _controller.Get(999);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ProductRequirementDto>>(result);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task CreateOrUpdate_ReturnsOk()
    {
        // Arrange
        var request = new CreateProductRequirementRequest { RequirementType = RequirementType.Normal };
        var dto = new ProductRequirementDto { Id = 1, RequirementType = RequirementType.Normal };
        _serviceMock.Setup(x => x.CreateOrUpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.CreateOrUpdate(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ProductRequirementDto>>(result);
        Assert.Equal(RequirementType.Normal, response.Data?.RequirementType);
    }

    [Fact]
    public async Task CreateOrUpdate_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.CreateOrUpdate(1, new CreateProductRequirementRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<ProductRequirementDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetByOrderId_ReturnsOk()
    {
        // Arrange
        var list = new List<ProductRequirementDto> { new() { Id = 1, RequirementType = RequirementType.Normal } };
        _serviceMock.Setup(x => x.GetByOrderIdAsync(1)).ReturnsAsync(list);

        // Act
        var result = await _controller.GetByOrderId(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<ProductRequirementDto>>>(result);
        Assert.Single(response.Data!);
    }
}
