using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.Quality;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class ChemicalValidationRuleControllerTests : ControllerTestBase
{
    private readonly Mock<IChemicalValidationRuleService> _serviceMock;
    private readonly Mock<ILogger<ChemicalValidationRuleController>> _loggerMock;
    private readonly ChemicalValidationRuleController _controller;

    public ChemicalValidationRuleControllerTests()
    {
        _serviceMock = new Mock<IChemicalValidationRuleService>();
        _loggerMock = CreateLoggerMock<ChemicalValidationRuleController>();
        _controller = new ChemicalValidationRuleController(_serviceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<ChemicalValidationRuleDto>
        {
            Items = new List<ChemicalValidationRuleDto> { new() { Id = 1, PlantGrade = "304" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<ChemicalValidationRuleDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetAll_LimitsPageSize()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<ChemicalValidationRuleDto> { Items = new List<ChemicalValidationRuleDto>() });

        // Act
        var result = await _controller.GetAll(pageSize: 10000);

        // Assert
        AssertOk<ApiResponse<PagedResult<ChemicalValidationRuleDto>>>(result);
        _serviceMock.Verify(x => x.GetAllAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new ChemicalValidationRuleDto { Id = 1, PlantGrade = "304" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ChemicalValidationRuleDto>>(result);
        Assert.Equal("304", response.Data?.PlantGrade);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((ChemicalValidationRuleDto?)null);

        // Act
        var result = await _controller.GetById(999);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<ChemicalValidationRuleDto>>(notFoundResult.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetByPlantGrade_ReturnsOk()
    {
        // Arrange
        var dto = new ChemicalValidationRuleDto { Id = 1, PlantGrade = "304" };
        _serviceMock.Setup(x => x.GetByPlantGradeAsync("304")).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetByPlantGrade("304");

        // Assert
        var (_, response) = AssertOk<ApiResponse<ChemicalValidationRuleDto?>>(result);
        Assert.Equal("304", response.Data?.PlantGrade);
    }

    [Fact]
    public async Task BatchCreate_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreateChemicalValidationRuleRequest> { new() { PlantGrade = "304" } };
        var dtos = new List<ChemicalValidationRuleDto> { new() { Id = 1, PlantGrade = "304" } };
        _serviceMock.Setup(x => x.BatchCreateAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.BatchCreate(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<ChemicalValidationRuleDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task BatchCreate_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.BatchCreate(new List<CreateChemicalValidationRuleRequest>());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<ChemicalValidationRuleDto>>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateChemicalValidationRuleRequest { PlantGrade = "316" };
        var dto = new ChemicalValidationRuleDto { Id = 1, PlantGrade = "316" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ChemicalValidationRuleDto>>(result);
        Assert.Equal("316", response.Data?.PlantGrade);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Update(1, new UpdateChemicalValidationRuleRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<ChemicalValidationRuleDto>>(result);
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
    public async Task GetAll_PassesKeyword_ToService()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<ChemicalValidationRuleDto> { Items = new List<ChemicalValidationRuleDto>() });

        // Act
        await _controller.GetAll(keyword: "测试搜索");

        // Assert
        _serviceMock.Verify(x => x.GetAllAsync(It.Is<QueryParams>(q => q.Keyword == "测试搜索")), Times.Once);
    }

    [Fact]
    public async Task GetAll_PassesSortBy_ToService()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<ChemicalValidationRuleDto> { Items = new List<ChemicalValidationRuleDto>() });

        // Act
        await _controller.GetAll(sortBy: "PlantGrade", isDescending: false);

        // Assert
        _serviceMock.Verify(x => x.GetAllAsync(It.Is<QueryParams>(q => q.SortBy == "PlantGrade" && q.IsDescending == false)), Times.Once);
    }

    [Fact]
    public async Task GetAll_DefaultSortBy_IsPlantGrade()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<ChemicalValidationRuleDto> { Items = new List<ChemicalValidationRuleDto>() });

        // Act
        await _controller.GetAll();

        // Assert
        _serviceMock.Verify(x => x.GetAllAsync(It.Is<QueryParams>(q => q.SortBy == "plantgrade")), Times.Once);
    }

    [Fact]
    public async Task GetAll_PassesFiltersJson_ToService()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<ChemicalValidationRuleDto> { Items = new List<ChemicalValidationRuleDto>() });

        var filtersJson = "[{\"Field\":\"PlantGrade\",\"Operator\":\"contains\",\"Value\":\"304\"}]";

        // Act
        await _controller.GetAll(filters: filtersJson);

        // Assert
        _serviceMock.Verify(x => x.GetAllAsync(It.Is<QueryParams>(q =>
            q.Filters != null && q.Filters.Count == 1 && q.Filters[0].Field == "PlantGrade")), Times.Once);
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
