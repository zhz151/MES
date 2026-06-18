using Microsoft.AspNetCore.Mvc;
using Moq;
using MES.Api.Controllers.Configuration;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class EmployeeControllerTests : ControllerTestBase
{
    private readonly Mock<IEmployeeService> _serviceMock;
    private readonly EmployeeController _controller;

    public EmployeeControllerTests()
    {
        _serviceMock = new Mock<IEmployeeService>();
        _controller = new EmployeeController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<EmployeeDto>
        {
            Items = new List<EmployeeDto> { new() { Id = 1, Code = "EMP001", Name = "张三" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<EmployeeDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetPaged_LimitsPageSize()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<EmployeeDto> { Items = new List<EmployeeDto>() });

        // Act
        var result = await _controller.GetPaged(pageSize: 10000);

        // Assert
        AssertOk<ApiResponse<PagedResult<EmployeeDto>>>(result);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetByCode_ReturnsOk()
    {
        // Arrange
        var dto = new EmployeeDto { Code = "EMP001", Name = "张三" };
        _serviceMock.Setup(x => x.GetByCodeAsync("EMP001")).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetByCode("EMP001");

        // Assert
        var (_, response) = AssertOk<ApiResponse<EmployeeDto>>(result);
        Assert.Equal("EMP001", response.Data?.Code);
    }

    [Fact]
    public async Task GetByCode_ReturnsBadRequest_WhenEmpty()
    {
        // Act
        var result = await _controller.GetByCode("");

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<EmployeeDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetByCode_ReturnsNotFound()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetByCodeAsync("NONEXISTENT")).ReturnsAsync((EmployeeDto?)null);

        // Act
        var result = await _controller.GetByCode("NONEXISTENT");

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<EmployeeDto>>(notFoundResult.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Save_ReturnsOk()
    {
        // Arrange
        var dto = new EmployeeDto { Code = "EMP001", Name = "张三" };
        _serviceMock.Setup(x => x.SaveAsync(dto)).ReturnsAsync(true);

        // Act
        var result = await _controller.Save(dto);

        // Assert
        var (_, response) = AssertOk<ApiResponse<bool>>(result);
        Assert.True(response.Data);
    }

    [Fact]
    public async Task Delete_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteAsync(1)).ReturnsAsync(true);

        // Act
        var result = await _controller.Delete(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<bool>>(result);
        Assert.True(response.Data);
    }
}
