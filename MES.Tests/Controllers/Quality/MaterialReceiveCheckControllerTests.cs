using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.Quality;
using MES.Core.Models;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Shared;
using MES.Core.Interfaces.Quality;

namespace MES.Tests.Controllers;

public class MaterialReceiveCheckControllerTests : ControllerTestBase
{
    private readonly Mock<IMaterialReceiveCheckService> _serviceMock;
    private readonly Mock<ILogger<MaterialReceiveCheckController>> _loggerMock;
    private readonly MaterialReceiveCheckController _controller;

    public MaterialReceiveCheckControllerTests()
    {
        _serviceMock = new Mock<IMaterialReceiveCheckService>();
        _loggerMock = CreateLoggerMock<MaterialReceiveCheckController>();
        _controller = new MaterialReceiveCheckController(_serviceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetMaterialReceiveCheck_ReturnsOk()
    {
        // Arrange
        var dto = new MaterialReceiveCheckDto { Id = 1, BatchNo = "BATCH001" };
        _serviceMock.Setup(x => x.GetMaterialReceiveCheckAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetMaterialReceiveCheck(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<MaterialReceiveCheckDto>>(result);
        Assert.Equal("BATCH001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task GetMaterialReceiveCheck_ReturnsDefault_WhenNotFound()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetMaterialReceiveCheckAsync(999)).ReturnsAsync((MaterialReceiveCheckDto?)null);

        // Act
        var result = await _controller.GetMaterialReceiveCheck(999);

        // Assert
        var (_, response) = AssertOk<ApiResponse<MaterialReceiveCheckDto>>(result);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task CreateMaterialReceiveCheck_ReturnsOk()
    {
        // Arrange
        var request = new CreateMaterialReceiveCheckRequest { BatchNo = "BATCH001" };
        var dto = new MaterialReceiveCheckDto { Id = 1, BatchNo = "BATCH001" };
        _serviceMock.Setup(x => x.CreateMaterialReceiveCheckAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.CreateMaterialReceiveCheck(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<MaterialReceiveCheckDto>>(result);
        Assert.Equal("BATCH001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task GetAllMaterialReceiveChecks_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<MaterialReceiveCheckDto>
        {
            Items = new List<MaterialReceiveCheckDto> { new() { Id = 1, BatchNo = "BATCH001" } },
            TotalCount = 1
        };
        _serviceMock.Setup(x => x.GetAllMaterialReceiveChecksAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAllMaterialReceiveChecks();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<MaterialReceiveCheckDto>>>(result);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task UpdateMaterialReceiveCheck_ReturnsOk()
    {
        // Arrange
        var request = new UpdateMaterialReceiveCheckRequest { ReceiveDate = DateTime.Now };
        var dto = new MaterialReceiveCheckDto { Id = 1, BatchNo = "BATCH002" };
        _serviceMock.Setup(x => x.UpdateMaterialReceiveCheckAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.UpdateMaterialReceiveCheck(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<MaterialReceiveCheckDto>>(result);
        Assert.Equal("BATCH002", response.Data?.BatchNo);
    }

    [Fact]
    public async Task DeleteMaterialReceiveCheck_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteMaterialReceiveCheckAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteMaterialReceiveCheck(1);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task BatchCreateMaterialReceiveChecks_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        _controller.ModelState.AddModelError("BatchNo", "Required");

        // Act
        var result = await _controller.BatchCreateMaterialReceiveChecks(new List<CreateMaterialReceiveCheckRequest>());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<MaterialReceiveCheckDto>>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task BatchCreateMaterialReceiveChecks_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreateMaterialReceiveCheckRequest> { new() { BatchNo = "BATCH001" } };
        var dtos = new List<MaterialReceiveCheckDto> { new() { Id = 1, BatchNo = "BATCH001" } };
        _serviceMock.Setup(x => x.BatchCreateMaterialReceiveChecksAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.BatchCreateMaterialReceiveChecks(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<MaterialReceiveCheckDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task PrintMaterialCheckBatch_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        _controller.ModelState.AddModelError("Ids", "Required");

        // Act
        var result = await _controller.PrintMaterialCheckBatch(new MaterialCheckPrintBatchRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintMaterialCheckBatch_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintMaterialCheckBatchAsync(It.IsAny<int[]>(), It.IsAny<List<PrintColumnDef>>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        // Act
        var result = await _controller.PrintMaterialCheckBatch(new MaterialCheckPrintBatchRequest { Ids = new[] { 1 } });

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task PrintMaterialCheckAll_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        _controller.ModelState.AddModelError("Columns", "Required");

        // Act
        var result = await _controller.PrintMaterialCheckAll(new MaterialCheckPrintAllRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintMaterialCheckAll_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintMaterialCheckAllAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<List<PrintColumnDef>>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        // Act
        var result = await _controller.PrintMaterialCheckAll(new MaterialCheckPrintAllRequest { Keyword = "BATCH" });

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetFilterContexts_ReturnsOk()
    {
        // Arrange
        var ctx = new Dictionary<string, List<string>> { { "BatchNo", new List<string> { "BATCH001" } } };
        _serviceMock.Setup(x => x.GetFilterContextsAsync()).ReturnsAsync(ctx);

        // Act
        var result = await _controller.GetFilterContexts();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<Dictionary<string, List<string>>>>(okResult.Value);
        Assert.NotNull(response.Data);
        Assert.Contains("BatchNo", response.Data.Keys);
    }

    [Fact]
    public async Task GetFilterContexts_Empty_ReturnsEmpty()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetFilterContextsAsync()).ReturnsAsync(new Dictionary<string, List<string>>());

        // Act
        var result = await _controller.GetFilterContexts();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<Dictionary<string, List<string>>>>(okResult.Value);
        Assert.Empty(response.Data!);
    }

    // ========== GetAllMaterialReceiveChecks parameter forwarding ==========

    [Fact]
    public async Task GetAllMaterialReceiveChecks_LimitsPageSize()
    {
        _serviceMock.Setup(x => x.GetAllMaterialReceiveChecksAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<MaterialReceiveCheckDto> { Items = new List<MaterialReceiveCheckDto>() });
        await _controller.GetAllMaterialReceiveChecks(pageSize: 10000);
        _serviceMock.Verify(x => x.GetAllMaterialReceiveChecksAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetAllMaterialReceiveChecks_PassesKeyword_ToService()
    {
        _serviceMock.Setup(x => x.GetAllMaterialReceiveChecksAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<MaterialReceiveCheckDto> { Items = new List<MaterialReceiveCheckDto>() });
        await _controller.GetAllMaterialReceiveChecks(keyword: "测试");
        _serviceMock.Verify(x => x.GetAllMaterialReceiveChecksAsync(It.Is<QueryParams>(q => q.Keyword == "测试")), Times.Once);
    }

    [Fact]
    public async Task GetAllMaterialReceiveChecks_PassesSortBy_ToService()
    {
        _serviceMock.Setup(x => x.GetAllMaterialReceiveChecksAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<MaterialReceiveCheckDto> { Items = new List<MaterialReceiveCheckDto>() });
        await _controller.GetAllMaterialReceiveChecks(sortBy: "BatchNo");
        _serviceMock.Verify(x => x.GetAllMaterialReceiveChecksAsync(It.Is<QueryParams>(q => q.SortBy == "BatchNo")), Times.Once);
    }

    [Fact]
    public async Task GetAllMaterialReceiveChecks_UsesDefaultSortBy_WhenNotProvided()
    {
        _serviceMock.Setup(x => x.GetAllMaterialReceiveChecksAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<MaterialReceiveCheckDto> { Items = new List<MaterialReceiveCheckDto>() });
        await _controller.GetAllMaterialReceiveChecks();
        _serviceMock.Verify(x => x.GetAllMaterialReceiveChecksAsync(It.Is<QueryParams>(q => q.SortBy == "createdtime")), Times.Once);
    }

    [Fact]
    public async Task GetAllMaterialReceiveChecks_PassesFilters_ToService()
    {
        _serviceMock.Setup(x => x.GetAllMaterialReceiveChecksAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<MaterialReceiveCheckDto> { Items = new List<MaterialReceiveCheckDto>() });
        var filtersJson = "[{\"Field\":\"BatchNo\",\"Operator\":\"equals\",\"Value\":\"BATCH001\"}]";
        await _controller.GetAllMaterialReceiveChecks(filters: filtersJson);
        _serviceMock.Verify(x => x.GetAllMaterialReceiveChecksAsync(It.Is<QueryParams>(q => q.Filters != null && q.Filters.Count > 0)), Times.Once);
    }
}
