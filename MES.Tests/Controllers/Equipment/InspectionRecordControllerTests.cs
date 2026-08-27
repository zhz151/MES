using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.Equipment;
using MES.Core.Models;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Shared;
using MES.Core.Interfaces.Equipment;

namespace MES.Tests.Controllers;

public class InspectionRecordControllerTests : ControllerTestBase
{
    private readonly Mock<IInspectionRecordService> _serviceMock;
    private readonly InspectionRecordController _controller;

    public InspectionRecordControllerTests()
    {
        _serviceMock = new Mock<IInspectionRecordService>();
        _controller = new InspectionRecordController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<InspectionRecordListDto>
        {
            Items = new List<InspectionRecordListDto> { new() { Id = 1, EquipmentName = "设备A" } },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<InspectionRecordQueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<InspectionRecordListDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new InspectionRecordListDto { Id = 1, EquipmentName = "设备A" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<InspectionRecordListDto>>(result);
        Assert.Equal("设备A", response.Data?.EquipmentName);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        // Arrange
        var request = new CreateInspectionRecordRequest { EquipmentId = 1 };
        var dto = new InspectionRecordListDto { Id = 1, EquipmentName = "设备A" };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<InspectionRecordListDto>>(result);
        Assert.Equal("设备A", response.Data?.EquipmentName);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Create(new CreateInspectionRecordRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<InspectionRecordListDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateInspectionRequest { Inspector = "新检验员" };
        var dto = new InspectionRecordListDto { Id = 1, EquipmentName = "设备A", Inspector = "新检验员" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<InspectionRecordListDto>>(result);
        Assert.Equal("新检验员", response.Data?.Inspector);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Update(1, new UpdateInspectionRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<InspectionRecordListDto>>(result);
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
        var requests = new List<CreateInspectionRecordRequest> { new() { EquipmentId = 1 } };
        var dtos = new List<InspectionRecordListDto> { new() { Id = 1, EquipmentName = "设备A" } };
        _serviceMock.Setup(x => x.CreateBatchAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.BatchCreate(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<InspectionRecordListDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task BatchCreate_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.BatchCreate(new List<CreateInspectionRecordRequest>());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<InspectionRecordListDto>>>(result);
        Assert.False(response.Success);
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
    public async Task GetPaged_LimitsPageSize()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<InspectionRecordQueryParams>()))
            .ReturnsAsync(new PagedResult<InspectionRecordListDto> { Items = new List<InspectionRecordListDto>() });
        await _controller.GetPaged(pageSize: 9999);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<InspectionRecordQueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesKeyword_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<InspectionRecordQueryParams>()))
            .ReturnsAsync(new PagedResult<InspectionRecordListDto> { Items = new List<InspectionRecordListDto>() });
        await _controller.GetPaged(keyword: "测试");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<InspectionRecordQueryParams>(q => q.Keyword == "测试")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesSortBy_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<InspectionRecordQueryParams>()))
            .ReturnsAsync(new PagedResult<InspectionRecordListDto> { Items = new List<InspectionRecordListDto>() });
        await _controller.GetPaged(sortBy: "Code", isDescending: false);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<InspectionRecordQueryParams>(q => q.SortBy == "Code" && q.IsDescending == false)), Times.Once);
    }

    [Fact]
    public async Task GetPaged_DefaultSortBy_IsId()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<InspectionRecordQueryParams>()))
            .ReturnsAsync(new PagedResult<InspectionRecordListDto> { Items = new List<InspectionRecordListDto>() });
        await _controller.GetPaged();
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<InspectionRecordQueryParams>(q => q.SortBy == "Id")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesFiltersJson_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<InspectionRecordQueryParams>()))
            .ReturnsAsync(new PagedResult<InspectionRecordListDto> { Items = new List<InspectionRecordListDto>() });
        var filtersJson = "[{\"Field\":\"Code\",\"Operator\":\"contains\",\"Value\":\"T\"}]";
        await _controller.GetPaged(filters: filtersJson);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<InspectionRecordQueryParams>(q =>
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
