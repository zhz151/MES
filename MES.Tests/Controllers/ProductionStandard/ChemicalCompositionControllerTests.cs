using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.ProductionStandard;
using MES.Core.Models;
using MES.Core.DTOs.ProductionStandard;
using MES.Core.Interfaces.ProductionStandard;
using System.Security.Claims;

namespace MES.Tests.Controllers;

public class ChemicalCompositionControllerTests : ControllerTestBase
{
    private readonly Mock<IChemicalCompositionService> _serviceMock;
    private readonly Mock<ILogger<ChemicalCompositionController>> _loggerMock;
    private readonly ChemicalCompositionController _controller;

    public ChemicalCompositionControllerTests()
    {
        _serviceMock = new Mock<IChemicalCompositionService>();
        _loggerMock = CreateLoggerMock<ChemicalCompositionController>();
        _controller = new ChemicalCompositionController(_serviceMock.Object, _loggerMock.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "admin") }))
            }
        };
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<ChemicalCompositionDto>
        {
            Items = new List<ChemicalCompositionDto> { new() { Id = 1, PlantGrade = "304" } },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<ChemicalCompositionDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetAll_LimitsPageSize()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetAllAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<ChemicalCompositionDto> { Items = new List<ChemicalCompositionDto>() });

        // Act
        var result = await _controller.GetAll(pageSize: 10000);

        // Assert
        AssertOk<ApiResponse<PagedResult<ChemicalCompositionDto>>>(result);
        _serviceMock.Verify(x => x.GetAllAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateChemicalCompositionRequest { Carbon = "0.08" };
        var dto = new ChemicalCompositionDto { Id = 1, PlantGrade = "304", Carbon = "0.08" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ChemicalCompositionDto>>(result);
        Assert.Equal("0.08", response.Data?.Carbon);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Update(1, new UpdateChemicalCompositionRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<ChemicalCompositionDto>>(result);
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
    public async Task BatchCreate_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreateChemicalCompositionRequest>
        {
            new() { PlantGrade = "304", Carbon = "0.08" }
        };
        var dtos = new List<ChemicalCompositionDto>
        {
            new() { Id = 1, PlantGrade = "304", Carbon = "0.08" }
        };
        _serviceMock.Setup(x => x.BatchCreateAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.BatchCreate(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<ChemicalCompositionDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task BatchCreate_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.BatchCreate(new List<CreateChemicalCompositionRequest>());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<ChemicalCompositionDto>>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task DownloadTemplate_ReturnsFile()
    {
        // Arrange
        _serviceMock.Setup(x => x.GenerateTemplateAsync()).ReturnsAsync(new byte[] { 0x50, 0x4B });

        // Act
        var result = await _controller.DownloadTemplate();

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileResult.ContentType);
        Assert.Equal("牌号化学成分_模板.xlsx", fileResult.FileDownloadName);
    }

    [Fact]
    public async Task Preview_NullFile_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.Preview(null!);

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<ImportPreviewResult>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Preview_ReturnsOk()
    {
        // Arrange
        var previewResult = new ImportPreviewResult { TotalRows = 5, ValidCount = 3, ErrorCount = 2 };
        _serviceMock.Setup(x => x.PreviewImportAsync(It.IsAny<byte[]>(), It.IsAny<string>()))
            .ReturnsAsync(previewResult);

        using var ms = new MemoryStream(new byte[] { 0x01, 0x02 });
        var file = new FormFile(ms, 0, ms.Length, "file", "test.xlsx");

        // Act
        var result = await _controller.Preview(file);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ImportPreviewResult>>(result);
        Assert.Equal(5, response.Data!.TotalRows);
        Assert.Equal(3, response.Data.ValidCount);
    }

    [Fact]
    public async Task Import_NullFile_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.Import(null!);

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<ImportResult>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Import_ReturnsOk()
    {
        // Arrange
        var importResult = new ImportResult { TotalRows = 5, SuccessCount = 4, FailedCount = 1 };
        _serviceMock.Setup(x => x.ImportAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(importResult);

        using var ms = new MemoryStream(new byte[] { 0x01, 0x02 });
        var file = new FormFile(ms, 0, ms.Length, "file", "test.xlsx");

        // Act
        var result = await _controller.Import(file);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ImportResult>>(result);
        Assert.Equal(5, response.Data!.TotalRows);
        Assert.Equal(4, response.Data.SuccessCount);
    }

    [Fact]
    public async Task Import_WithRollback_ReturnsOk()
    {
        // Arrange
        var importResult = new ImportResult { TotalRows = 5, SuccessCount = 0, FailedCount = 5, HasRolledBack = true };
        _serviceMock.Setup(x => x.ImportAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(importResult);

        using var ms = new MemoryStream(new byte[] { 0x01, 0x02 });
        var file = new FormFile(ms, 0, ms.Length, "file", "test.xlsx");

        // Act
        var result = await _controller.Import(file);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ImportResult>>(result);
        Assert.True(response.Data!.HasRolledBack);
        Assert.Contains("已回滚", response.Message);
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
            .ReturnsAsync(new PagedResult<ChemicalCompositionDto> { Items = new List<ChemicalCompositionDto>() });

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
            .ReturnsAsync(new PagedResult<ChemicalCompositionDto> { Items = new List<ChemicalCompositionDto>() });

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
            .ReturnsAsync(new PagedResult<ChemicalCompositionDto> { Items = new List<ChemicalCompositionDto>() });

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
            .ReturnsAsync(new PagedResult<ChemicalCompositionDto> { Items = new List<ChemicalCompositionDto>() });

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
