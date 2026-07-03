using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.ProductionStandard;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class GradeMappingControllerTests : ControllerTestBase
{
    private readonly Mock<IGradeMappingService> _serviceMock;
    private readonly GradeMappingController _controller;

    public GradeMappingControllerTests()
    {
        _serviceMock = new Mock<IGradeMappingService>();
        _controller = new GradeMappingController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<StandardGradeMappingDto>
        {
            Items = new List<StandardGradeMappingDto> { new() { Id = 1, PlantGrade = "304" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged(pageIndex: 1, pageSize: 20);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<StandardGradeMappingDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new StandardGradeMappingDto { Id = 1, PlantGrade = "304" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<StandardGradeMappingDto>>(result);
        Assert.Equal("304", response.Data?.PlantGrade);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        // Arrange
        var request = new CreateGradeMappingRequest { PlantGrade = "316" };
        var dto = new StandardGradeMappingDto { Id = 1, PlantGrade = "316" };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<StandardGradeMappingDto>>(result);
        Assert.Equal("316", response.Data?.PlantGrade);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Create(new CreateGradeMappingRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<StandardGradeMappingDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateGradeMappingRequest { PlantGrade = "316L" };
        var dto = new StandardGradeMappingDto { Id = 1, PlantGrade = "316L" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<StandardGradeMappingDto>>(result);
        Assert.Equal("316L", response.Data?.PlantGrade);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Update(1, new UpdateGradeMappingRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<StandardGradeMappingDto>>(result);
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
        var response = Assert.IsType<ApiResponse<bool>>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        // Arrange
        var mappings = new List<StandardGradeMappingDto>
        {
            new() { Id = 1, PlantGrade = "304" },
            new() { Id = 2, PlantGrade = "316" }
        };
        _serviceMock.Setup(x => x.GetAllAsync()).ReturnsAsync(mappings);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<StandardGradeMappingDto>>>(result);
        Assert.Equal(2, response.Data?.Count);
    }

    [Fact]
    public async Task PrintGradeMapping_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintGradeMappingAsync(1)).ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintGradeMapping(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintGradeMappingBatch_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintGradeMappingBatch(new OrderPrintBatchRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintGradeMappingBatch_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintGradeMappingBatchAsync(It.IsAny<int[]>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintGradeMappingBatch(new OrderPrintBatchRequest { Ids = new[] { 1, 2 } });

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintGradeMappingAll_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintGradeMappingAll(new OrderPrintAllRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintGradeMappingAll_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintGradeMappingAllAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintGradeMappingAll(new OrderPrintAllRequest { Keyword = "304" });

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task GetFilterContexts_ReturnsOk()
    {
        var filterContexts = new Dictionary<string, List<string>>
        {
            ["Field1"] = new() { "A", "B" }
        };
        _serviceMock.Setup(x => x.GetFilterContextsAsync()).ReturnsAsync(filterContexts);
        var result = await _controller.GetFilterContexts();
        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetPaged_PassesKeyword_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<StandardGradeMappingDto> { Items = new List<StandardGradeMappingDto>() });
        await _controller.GetPaged(keyword: "测试");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.Keyword == "测试")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesSortBy_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<StandardGradeMappingDto> { Items = new List<StandardGradeMappingDto>() });
        await _controller.GetPaged(sortBy: "Code", isDescending: false);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.SortBy == "Code" && q.IsDescending == false)), Times.Once);
    }

    [Fact]
    public async Task GetPaged_DefaultSortBy_IsCreatedTime()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<StandardGradeMappingDto> { Items = new List<StandardGradeMappingDto>() });
        await _controller.GetPaged(pageIndex: 1, pageSize: 20);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.SortBy == "CreatedTime")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesFiltersJson_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<StandardGradeMappingDto> { Items = new List<StandardGradeMappingDto>() });
        var filtersJson = "[{\"Field\":\"Code\",\"Operator\":\"contains\",\"Value\":\"T\"}]";
        await _controller.GetPaged(filters: filtersJson);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q =>
            q.Filters != null && q.Filters.Count == 1 && q.Filters[0].Field == "Code")), Times.Once);
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
}
