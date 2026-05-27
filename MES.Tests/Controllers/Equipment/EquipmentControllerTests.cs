using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class EquipmentControllerTests : ControllerTestBase
{
    private readonly Mock<IEquipmentService> _serviceMock;
    private readonly Mock<ILogger<EquipmentController>> _loggerMock;
    private readonly EquipmentController _controller;

    public EquipmentControllerTests()
    {
        _serviceMock = new Mock<IEquipmentService>();
        _loggerMock = CreateLoggerMock<EquipmentController>();
        _controller = new EquipmentController(_serviceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<EquipmentListDto>
        {
            Items = new List<EquipmentListDto> { new() { Id = 1, EquipmentName = "设备A" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<EquipmentQueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetPaged();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<EquipmentListDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetPaged_LimitsPageSize()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<EquipmentQueryParams>()))
            .ReturnsAsync(new PagedResult<EquipmentListDto> { Items = new List<EquipmentListDto>() });

        // Act
        var result = await _controller.GetPaged(pageSize: 10000);

        // Assert
        AssertOk<ApiResponse<PagedResult<EquipmentListDto>>>(result);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<EquipmentQueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        // Arrange
        var dto = new EquipmentDetailDto { Id = 1, EquipmentName = "设备A" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<EquipmentDetailDto>>(result);
        Assert.Equal("设备A", response.Data?.EquipmentName);
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        // Arrange
        var list = new List<EquipmentListDto> { new() { Id = 1, EquipmentName = "设备A" } };
        _serviceMock.Setup(x => x.GetAllAsync()).ReturnsAsync(list);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<EquipmentListDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        // Arrange
        var request = new CreateEquipmentRequest { EquipmentName = "新设备" };
        var dto = new EquipmentDetailDto { Id = 1, EquipmentName = "新设备" };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<EquipmentDetailDto>>(result);
        Assert.Equal("新设备", response.Data?.EquipmentName);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Create(new CreateEquipmentRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<EquipmentDetailDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var request = new UpdateEquipmentRequest { EquipmentName = "更新名称" };
        var dto = new EquipmentDetailDto { Id = 1, EquipmentName = "更新名称" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<EquipmentDetailDto>>(result);
        Assert.Equal("更新名称", response.Data?.EquipmentName);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Update(1, new UpdateEquipmentRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<EquipmentDetailDto>>(result);
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
    public async Task PrintBatch_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintBatch(new EquipmentPrintBatchRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintBatch_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintBatchAsync(It.IsAny<int[]>(), It.IsAny<List<PrintColumnDef>>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintBatch(new EquipmentPrintBatchRequest { Ids = new[] { 1, 2 } });

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintAll_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintAll(new EquipmentPrintAllRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintAll_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintAllAsync(It.IsAny<EquipmentQueryParams>(), It.IsAny<List<PrintColumnDef>>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintAll(new EquipmentPrintAllRequest { Keyword = "设备" });

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task GetFilterContexts_ReturnsOk()
    {
        // Arrange
        var filterContexts = new Dictionary<string, List<string>>
        {
            ["Field1"] = new() { "A", "B" }
        };
        _serviceMock.Setup(x => x.GetEquipmentFilterContextsAsync()).ReturnsAsync(filterContexts);

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
        _serviceMock.Setup(x => x.GetEquipmentFilterContextsAsync()).ReturnsAsync(new Dictionary<string, List<string>>());

        // Act
        var result = await _controller.GetFilterContexts();

        // Assert
        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.True(response.Success);
        Assert.Empty(response.Data!);
    }

    [Fact]
    public async Task GetPaged_PassesKeyword_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<EquipmentQueryParams>()))
            .ReturnsAsync(new PagedResult<EquipmentListDto> { Items = new List<EquipmentListDto>() });
        await _controller.GetPaged(keyword: "测试");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<EquipmentQueryParams>(q => q.Keyword == "测试")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesSortBy_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<EquipmentQueryParams>()))
            .ReturnsAsync(new PagedResult<EquipmentListDto> { Items = new List<EquipmentListDto>() });
        await _controller.GetPaged(sortBy: "EquipmentName", isDescending: false);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<EquipmentQueryParams>(q => q.SortBy == "EquipmentName" && q.IsDescending == false)), Times.Once);
    }

    [Fact]
    public async Task GetPaged_DefaultSortBy_IsCreatedTime()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<EquipmentQueryParams>()))
            .ReturnsAsync(new PagedResult<EquipmentListDto> { Items = new List<EquipmentListDto>() });
        await _controller.GetPaged();
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<EquipmentQueryParams>(q => q.SortBy == "CreatedTime")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesFiltersJson_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<EquipmentQueryParams>()))
            .ReturnsAsync(new PagedResult<EquipmentListDto> { Items = new List<EquipmentListDto>() });
        var filtersJson = "[{\"Field\":\"EquipmentName\",\"Operator\":\"contains\",\"Value\":\"TEST\"}]";
        await _controller.GetPaged(filters: filtersJson);
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<EquipmentQueryParams>(q =>
            q.Filters != null && q.Filters.Count == 1 && q.Filters[0].Field == "EquipmentName")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesLifecycleStatus_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<EquipmentQueryParams>()))
            .ReturnsAsync(new PagedResult<EquipmentListDto> { Items = new List<EquipmentListDto>() });
        await _controller.GetPaged(lifecycleStatus: "正常");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<EquipmentQueryParams>(q => q.LifecycleStatus == "正常")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesUsageType_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<EquipmentQueryParams>()))
            .ReturnsAsync(new PagedResult<EquipmentListDto> { Items = new List<EquipmentListDto>() });
        await _controller.GetPaged(usageType: "生产");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<EquipmentQueryParams>(q => q.UsageType == "生产")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesRunningStatus_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<EquipmentQueryParams>()))
            .ReturnsAsync(new PagedResult<EquipmentListDto> { Items = new List<EquipmentListDto>() });
        await _controller.GetPaged(runningStatus: "运行");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<EquipmentQueryParams>(q => q.RunningStatus == "运行")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesInspectionStatus_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<EquipmentQueryParams>()))
            .ReturnsAsync(new PagedResult<EquipmentListDto> { Items = new List<EquipmentListDto>() });
        await _controller.GetPaged(inspectionStatus: "已检");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<EquipmentQueryParams>(q => q.InspectionStatus == "已检")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesMaintStatus_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<EquipmentQueryParams>()))
            .ReturnsAsync(new PagedResult<EquipmentListDto> { Items = new List<EquipmentListDto>() });
        await _controller.GetPaged(maintStatus: "正常");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<EquipmentQueryParams>(q => q.MaintStatus == "正常")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesLocation_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<EquipmentQueryParams>()))
            .ReturnsAsync(new PagedResult<EquipmentListDto> { Items = new List<EquipmentListDto>() });
        await _controller.GetPaged(location: "A区");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<EquipmentQueryParams>(q => q.Location == "A区")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesRelatedSection_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<EquipmentQueryParams>()))
            .ReturnsAsync(new PagedResult<EquipmentListDto> { Items = new List<EquipmentListDto>() });
        await _controller.GetPaged(relatedSection: "热处理");
        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<EquipmentQueryParams>(q => q.RelatedSection == "热处理")), Times.Once);
    }
}
