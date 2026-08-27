using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.Batch;
using MES.Core.Models;
using MES.Core.DTOs.Batch;
using MES.Core.Interfaces.Batch;

namespace MES.Tests.Controllers;

public class SectionOutsourceControllerTests : ControllerTestBase
{
    private readonly Mock<ISectionOutsourceService> _serviceMock;
    private readonly SectionOutsourceController _controller;

    public SectionOutsourceControllerTests()
    {
        _serviceMock = new Mock<ISectionOutsourceService>();
        _controller = new SectionOutsourceController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetByIds_ReturnsOk()
    {
        // Arrange
        var list = new List<SectionOutsourceDto> { new() { Id = 1, BatchNo = "BATCH001" } };
        _serviceMock.Setup(x => x.GetByIdsAsync("1,2")).ReturnsAsync(list);

        // Act
        var result = await _controller.GetByIds("1,2");

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<SectionOutsourceDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetByIds_ReturnsBadRequest_WhenIdsEmpty()
    {
        // Act
        var result = await _controller.GetByIds("");

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<SectionOutsourceDto>>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<SectionOutsourceDto>
        {
            Items = new List<SectionOutsourceDto> { new() { Id = 1, BatchNo = "BATCH001" } },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<SectionOutsourceDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetPaged_LimitsPageSize()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<SectionOutsourceDto> { Items = new List<SectionOutsourceDto>() });

        // Act
        var result = await _controller.GetPaged(pageSize: 10000);

        // Assert
        AssertOk<ApiResponse<PagedResult<SectionOutsourceDto>>>(result);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        // Arrange
        var request = new CreateSectionOutsourceRequest { BatchNo = "BATCH001" };
        var dto = new SectionOutsourceDto { Id = 1, BatchNo = "BATCH001" };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<SectionOutsourceDto>>(result);
        Assert.Equal("BATCH001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Create(new CreateSectionOutsourceRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<SectionOutsourceDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task BatchCreate_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreateSectionOutsourceRequest> { new() { BatchNo = "BATCH001" } };
        var dtos = new List<SectionOutsourceDto> { new() { Id = 1, BatchNo = "BATCH001" } };
        _serviceMock.Setup(x => x.BatchCreateAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.BatchCreate(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<SectionOutsourceDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task BatchCreate_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.BatchCreate(new List<CreateSectionOutsourceRequest>());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<SectionOutsourceDto>>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateSectionOutsourceRequest { SendQuantity = 100 };
        var dto = new SectionOutsourceDto { Id = 1, BatchNo = "BATCH002" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<SectionOutsourceDto>>(result);
        Assert.Equal("BATCH002", response.Data?.BatchNo);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Update(1, new UpdateSectionOutsourceRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<SectionOutsourceDto>>(result);
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
    public async Task GetRecoveries_ReturnsOk()
    {
        // Arrange
        var list = new List<OutsourceRecoveryDto> { new() { Id = 1, SectionOutsourceId = 1 } };
        _serviceMock.Setup(x => x.GetRecoveriesAsync(1)).ReturnsAsync(list);

        // Act
        var result = await _controller.GetRecoveries(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<OutsourceRecoveryDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetRecoveriesPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<OutsourceRecoveryDto>
        {
            Items = new List<OutsourceRecoveryDto> { new() { Id = 1, SectionOutsourceId = 1 } },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        _serviceMock.Setup(x => x.GetRecoveriesPagedAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetRecoveriesPaged();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<OutsourceRecoveryDto>>>(result);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task CreateRecovery_ReturnsOk()
    {
        // Arrange
        var request = new CreateOutsourceRecoveryRequest { SectionOutsourceId = 1 };
        var dto = new OutsourceRecoveryDto { Id = 1, SectionOutsourceId = 1 };
        _serviceMock.Setup(x => x.CreateRecoveryAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.CreateRecovery(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<OutsourceRecoveryDto>>(result);
        Assert.Equal(1, response.Data?.SectionOutsourceId);
    }

    [Fact]
    public async Task UpdateRecovery_ReturnsOk()
    {
        // Arrange
        var request = new UpdateOutsourceRecoveryRequest();
        var dto = new OutsourceRecoveryDto { Id = 1, SectionOutsourceId = 1 };
        _serviceMock.Setup(x => x.UpdateRecoveryAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.UpdateRecovery(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<OutsourceRecoveryDto>>(result);
        Assert.Equal(1, response.Data?.SectionOutsourceId);
    }

    [Fact]
    public async Task DeleteRecovery_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteRecoveryAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteRecovery(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task BatchCreateRecoveries_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreateOutsourceRecoveryRequest> { new() { SectionOutsourceId = 1 } };
        var dtos = new List<OutsourceRecoveryDto> { new() { Id = 1, SectionOutsourceId = 1 } };
        _serviceMock.Setup(x => x.BatchCreateRecoveriesAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.BatchCreateRecoveries(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<OutsourceRecoveryDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetFilterContexts_ReturnsOk()
    {
        // Arrange
        var ctx = new Dictionary<string, List<string>> { ["BatchNo"] = new() { "BATCH001" } };
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
    public async Task GetRecoveryFilterContexts_ReturnsOk()
    {
        // Arrange
        var ctx = new Dictionary<string, List<string>> { ["RecoveryDate"] = new() { "2025-01-01" } };
        _serviceMock.Setup(x => x.GetOutsourceRecoveryFilterContextsAsync()).ReturnsAsync(ctx);

        // Act
        var result = await _controller.GetRecoveryFilterContexts();

        // Assert
        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetRecoveryFilterContexts_Empty_ReturnsEmpty()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetOutsourceRecoveryFilterContextsAsync()).ReturnsAsync(new Dictionary<string, List<string>>());

        // Act
        var result = await _controller.GetRecoveryFilterContexts();

        // Assert
        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
        Assert.Empty(response.Data!);
    }

    // ========== GetPaged parameter forwarding ==========

    [Fact]
    public async Task GetPaged_PassesKeyword_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<SectionOutsourceDto> { Items = new List<SectionOutsourceDto>() });
        await _controller.GetPaged(keyword: "测试");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.Keyword == "测试")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesSortBy_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<SectionOutsourceDto> { Items = new List<SectionOutsourceDto>() });
        await _controller.GetPaged(sortBy: "BatchNo");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.SortBy == "BatchNo")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_UsesDefaultSortBy_WhenNotProvided()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<SectionOutsourceDto> { Items = new List<SectionOutsourceDto>() });
        await _controller.GetPaged();
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.SortBy == "createdtime")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesFilters_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<SectionOutsourceDto> { Items = new List<SectionOutsourceDto>() });
        var filtersJson = "[{\"Field\":\"BatchNo\",\"Operator\":\"equals\",\"Value\":\"BATCH001\"}]";
        await _controller.GetPaged(filters: filtersJson);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.Filters != null && q.Filters.Count > 0)), Times.Once);
    }

    // ========== GetRecoveriesPaged parameter forwarding ==========

    [Fact]
    public async Task GetRecoveriesPaged_LimitsPageSize()
    {
        _serviceMock.Setup(x => x.GetRecoveriesPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<OutsourceRecoveryDto> { Items = new List<OutsourceRecoveryDto>() });
        await _controller.GetRecoveriesPaged(pageSize: 10000);
        _serviceMock.Verify(x => x.GetRecoveriesPagedAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetRecoveriesPaged_PassesKeyword_ToService()
    {
        _serviceMock.Setup(x => x.GetRecoveriesPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<OutsourceRecoveryDto> { Items = new List<OutsourceRecoveryDto>() });
        await _controller.GetRecoveriesPaged(keyword: "测试");
        _serviceMock.Verify(x => x.GetRecoveriesPagedAsync(It.Is<QueryParams>(q => q.Keyword == "测试")), Times.Once);
    }

    [Fact]
    public async Task GetRecoveriesPaged_PassesSortBy_ToService()
    {
        _serviceMock.Setup(x => x.GetRecoveriesPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<OutsourceRecoveryDto> { Items = new List<OutsourceRecoveryDto>() });
        await _controller.GetRecoveriesPaged(sortBy: "SectionOutsourceId");
        _serviceMock.Verify(x => x.GetRecoveriesPagedAsync(It.Is<QueryParams>(q => q.SortBy == "SectionOutsourceId")), Times.Once);
    }

    [Fact]
    public async Task GetRecoveriesPaged_UsesDefaultSortBy_WhenNotProvided()
    {
        _serviceMock.Setup(x => x.GetRecoveriesPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<OutsourceRecoveryDto> { Items = new List<OutsourceRecoveryDto>() });
        await _controller.GetRecoveriesPaged();
        _serviceMock.Verify(x => x.GetRecoveriesPagedAsync(It.Is<QueryParams>(q => q.SortBy == "recoverydate")), Times.Once);
    }

    [Fact]
    public async Task GetRecoveriesPaged_PassesFilters_ToService()
    {
        _serviceMock.Setup(x => x.GetRecoveriesPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<OutsourceRecoveryDto> { Items = new List<OutsourceRecoveryDto>() });
        var filtersJson = "[{\"Field\":\"SectionOutsourceId\",\"Operator\":\"equals\",\"Value\":\"1\"}]";
        await _controller.GetRecoveriesPaged(filters: filtersJson);
        _serviceMock.Verify(x => x.GetRecoveriesPagedAsync(It.Is<QueryParams>(q => q.Filters != null && q.Filters.Count > 0)), Times.Once);
    }
}
