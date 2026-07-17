using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.Materials;
using MES.Core.Models;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.Enums;
using MES.Core.Interfaces.Materials;

namespace MES.Tests.Controllers;

public class MaterialControllerTests : ControllerTestBase
{
    private readonly Mock<IMaterialService> _serviceMock;
    private readonly Mock<ILogger<MaterialController>> _loggerMock;
    private readonly MaterialController _controller;

    public MaterialControllerTests()
    {
        _serviceMock = new Mock<IMaterialService>();
        _loggerMock = CreateLoggerMock<MaterialController>();
        _controller = new MaterialController(_serviceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<MaterialDto>
        {
            Items = new List<MaterialDto> { new() { Id = 1, MaterialCode = "测试物料" } },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<MaterialDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetPaged_LimitsPageSize()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<MaterialDto> { Items = new List<MaterialDto>() });

        // Act
        var result = await _controller.GetPaged(pageSize: 10000);

        // Assert
        AssertOk<ApiResponse<PagedResult<MaterialDto>>>(result);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new MaterialDto { Id = 1, MaterialCode = "测试物料" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<MaterialDto>>(result);
        Assert.Equal("测试物料", response.Data?.MaterialCode);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        // Arrange
        var request = new CreateMaterialRequest { PlantGrade = "304", MaterialCategory = MaterialType.RoughTube, Specification = "219*8" };
        var dto = new MaterialDto { Id = 1, MaterialCode = "M001" };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<MaterialDto>>(result);
        Assert.Equal("M001", response.Data?.MaterialCode);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Create(new CreateMaterialRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<MaterialDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateMaterialRequest { PlantGrade = "316L" };
        var dto = new MaterialDto { Id = 1, MaterialCode = "M002" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<MaterialDto>>(result);
        Assert.Equal("M002", response.Data?.MaterialCode);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Update(1, new UpdateMaterialRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<MaterialDto>>(result);
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
    public async Task PrintMaterial_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintMaterialAsync(1, null)).ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintMaterialSingle(new OrderPrintSingleRequest { Id = 1 });

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task GetCategories_ReturnsOk()
    {
        // Arrange
        var categories = new List<string> { "管材", "棒材" };
        _serviceMock.Setup(x => x.GetCategoriesAsync()).ReturnsAsync(categories);

        // Act
        var result = await _controller.GetCategories();

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<string>>>(result);
        Assert.Equal(2, response.Data?.Count);
    }

    [Fact]
    public async Task GetActive_ReturnsOk()
    {
        // Arrange
        var materials = new List<MaterialDto> { new() { Id = 1, MaterialCode = "活跃物料" } };
        _serviceMock.Setup(x => x.GetActiveAsync()).ReturnsAsync(materials);

        // Act
        var result = await _controller.GetActive();

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<MaterialDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task Match_ReturnsOk_WhenFound()
    {
        // Arrange
        var dto = new MaterialDto { Id = 1, MaterialCode = "匹配物料" };
        _serviceMock.Setup(x => x.MatchAsync("管材", "304", "219*8")).ReturnsAsync(dto);

        // Act
        var result = await _controller.Match("管材", "304", "219*8");

        // Assert
        var (_, response) = AssertOk<ApiResponse<MaterialDto?>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task Match_ReturnsOk_WhenNotFound()
    {
        // Arrange
        _serviceMock.Setup(x => x.MatchAsync("管材", "999", "X")).ReturnsAsync((MaterialDto?)null);

        // Act
        var result = await _controller.Match("管材", "999", "X");

        // Assert
        var (_, response) = AssertOk<ApiResponse<MaterialDto?>>(result);
        Assert.True(response.Success);
        Assert.Null(response.Data);
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
            .ReturnsAsync(new PagedResult<MaterialDto> { Items = new List<MaterialDto>() });

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
            .ReturnsAsync(new PagedResult<MaterialDto> { Items = new List<MaterialDto>() });

        // Act
        await _controller.GetPaged(sortBy: "MaterialCode", isDescending: false);

        // Assert
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.SortBy == "MaterialCode" && q.IsDescending == false)), Times.Once);
    }

    [Fact]
    public async Task GetPaged_DefaultSortBy_IsCreatedTime()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<MaterialDto> { Items = new List<MaterialDto>() });

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
            .ReturnsAsync(new PagedResult<MaterialDto> { Items = new List<MaterialDto>() });

        var filtersJson = "[{\"Field\":\"MaterialCode\",\"Operator\":\"contains\",\"Value\":\"test\"}]";

        // Act
        await _controller.GetPaged(filters: filtersJson);

        // Assert
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q =>
            q.Filters != null && q.Filters.Count == 1 && q.Filters[0].Field == "MaterialCode")), Times.Once);
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
