using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MES.Api.Controllers.StandardRegister;
using MES.Core.Models;
using MES.Core.DTOs.StandardRegister;
using MES.Core.Interfaces.StandardRegister;
using System.Security.Claims;

namespace MES.Tests.Controllers;

public class StandardInspectionRequirementControllerTests : ControllerTestBase
{
    private readonly Mock<IStandardInspectionRequirementService> _serviceMock;
    private readonly StandardInspectionRequirementController _controller;

    public StandardInspectionRequirementControllerTests()
    {
        _serviceMock = new Mock<IStandardInspectionRequirementService>();
        _controller = new StandardInspectionRequirementController(_serviceMock.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "admin") }))
            }
        };
    }

    [Fact]
    public async Task GetPaged_ReturnsOk()
    {
        var pagedResult = new PagedResult<StandardInspectionRequirementDto>
        {
            Items = new List<StandardInspectionRequirementDto> { new() { Id = 1, StandardNo = "GB/T 14976" } },
            TotalCount = 1, PageIndex = 1, PageSize = 10
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        var result = await _controller.GetPaged();

        var (_, response) = AssertOk<ApiResponse<PagedResult<StandardInspectionRequirementDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetPaged_LimitsPageSize()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<StandardInspectionRequirementDto> { Items = new List<StandardInspectionRequirementDto>() });

        await _controller.GetPaged(pageSize: 10000);

        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesFilters_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<StandardInspectionRequirementDto> { Items = new List<StandardInspectionRequirementDto>() });

        var filtersJson = "[{\"Field\":\"StandardNo\",\"Operator\":\"contains\",\"Value\":\"GB/T\"}]";
        await _controller.GetPaged(filters: filtersJson);

        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q =>
            q.Filters != null && q.Filters.Count == 1)), Times.Once);
    }

    [Fact]
    public async Task GetPaged_DefaultSortBy_IsCreatedTime()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<StandardInspectionRequirementDto> { Items = new List<StandardInspectionRequirementDto>() });

        await _controller.GetPaged();

        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.SortBy == "CreatedTime")), Times.Once);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        var dto = new StandardInspectionRequirementDto { Id = 1, StandardNo = "GB/T 14976" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        var result = await _controller.GetById(1);

        var (_, response) = AssertOk<ApiResponse<StandardInspectionRequirementDto>>(result);
        Assert.Equal("GB/T 14976", response.Data?.StandardNo);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        var request = new CreateStandardInspectionRequirementRequest { StandardNo = "GB/T 8163" };
        var dto = new StandardInspectionRequirementDto { Id = 1, StandardNo = "GB/T 8163" };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        var result = await _controller.Create(request);

        var (_, response) = AssertOk<ApiResponse<StandardInspectionRequirementDto>>(result);
        Assert.Equal("GB/T 8163", response.Data?.StandardNo);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        var request = new UpdateStandardInspectionRequirementRequest { StandardNo = "GB/T 8163" };
        var dto = new StandardInspectionRequirementDto { Id = 1, StandardNo = "GB/T 8163" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        var result = await _controller.Update(1, request);

        var (_, response) = AssertOk<ApiResponse<StandardInspectionRequirementDto>>(result);
        Assert.Equal("GB/T 8163", response.Data?.StandardNo);
    }

    [Fact]
    public async Task Delete_ReturnsOk()
    {
        _serviceMock.Setup(x => x.DeleteAsync(1)).Returns(Task.CompletedTask);

        var result = await _controller.Delete(1);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task GetFilterContexts_ReturnsOk()
    {
        var contexts = new Dictionary<string, List<string>> { ["StandardNo"] = new() { "GB/T 14976", "GB/T 8163" } };
        _serviceMock.Setup(x => x.GetFilterContextsAsync()).ReturnsAsync(contexts);

        var result = await _controller.GetFilterContexts();

        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.Single(response.Data!);
    }
}
