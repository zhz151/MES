using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MES.Api.Controllers.StandardRegister;
using MES.Core.Models;
using MES.Core.DTOs.StandardRegister;
using MES.Core.Interfaces.StandardRegister;
using System.Security.Claims;

namespace MES.Tests.Controllers;

public class GradePhysicalPropertyControllerTests : ControllerTestBase
{
    private readonly Mock<IGradePhysicalPropertyService> _serviceMock;
    private readonly GradePhysicalPropertyController _controller;

    public GradePhysicalPropertyControllerTests()
    {
        _serviceMock = new Mock<IGradePhysicalPropertyService>();
        _controller = new GradePhysicalPropertyController(_serviceMock.Object);
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
        var pagedResult = new PagedResult<GradePhysicalPropertyDto>
        {
            Items = new List<GradePhysicalPropertyDto> { new() { Id = 1, StandardGrade = "304", TensileStrength = "520" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        var result = await _controller.GetPaged();

        var (_, response) = AssertOk<ApiResponse<PagedResult<GradePhysicalPropertyDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetPaged_LimitsPageSize()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<GradePhysicalPropertyDto> { Items = new List<GradePhysicalPropertyDto>() });

        await _controller.GetPaged(pageSize: 10000);

        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesFilters_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<GradePhysicalPropertyDto> { Items = new List<GradePhysicalPropertyDto>() });

        var filtersJson = "[{\"Field\":\"StandardGrade\",\"Operator\":\"contains\",\"Value\":\"304\"}]";
        await _controller.GetPaged(filters: filtersJson);

        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q =>
            q.Filters != null && q.Filters.Count == 1)), Times.Once);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        var dto = new GradePhysicalPropertyDto { Id = 1, StandardGrade = "304" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        var result = await _controller.GetById(1);

        var (_, response) = AssertOk<ApiResponse<GradePhysicalPropertyDto>>(result);
        Assert.Equal("304", response.Data?.StandardGrade);
    }

    [Fact]
    public async Task Create_ReturnsOk()
    {
        var request = new CreateGradePhysicalPropertyRequest { StandardGrade = "316L" };
        var dto = new GradePhysicalPropertyDto { Id = 1, StandardGrade = "316L" };
        _serviceMock.Setup(x => x.CreateAsync(request)).ReturnsAsync(dto);

        var result = await _controller.Create(request);

        var (_, response) = AssertOk<ApiResponse<GradePhysicalPropertyDto>>(result);
        Assert.Equal("316L", response.Data?.StandardGrade);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelInvalid()
    {
        AddModelError(_controller);

        var result = await _controller.Create(new CreateGradePhysicalPropertyRequest());

        var (_, response) = AssertBadRequest<ApiResponse<GradePhysicalPropertyDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        var request = new UpdateGradePhysicalPropertyRequest { StandardGrade = "316L" };
        var dto = new GradePhysicalPropertyDto { Id = 1, StandardGrade = "316L" };
        _serviceMock.Setup(x => x.UpdateAsync(1, request)).ReturnsAsync(dto);

        var result = await _controller.Update(1, request);

        var (_, response) = AssertOk<ApiResponse<GradePhysicalPropertyDto>>(result);
        Assert.Equal("316L", response.Data?.StandardGrade);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        AddModelError(_controller);

        var result = await _controller.Update(1, new UpdateGradePhysicalPropertyRequest());

        var (_, response) = AssertBadRequest<ApiResponse<GradePhysicalPropertyDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Delete_ReturnsOk()
    {
        _serviceMock.Setup(x => x.DeleteAsync(1)).Returns(Task.CompletedTask);

        var result = await _controller.Delete(1);

        var (_, response) = AssertOk<ApiResponse<bool>>(result);
        Assert.True(response.Data);
    }

    [Fact]
    public async Task GetFilterContexts_ReturnsOk()
    {
        var contexts = new Dictionary<string, List<string>> { ["StandardGrade"] = new() { "304", "316L" } };
        _serviceMock.Setup(x => x.GetFilterContextsAsync()).ReturnsAsync(contexts);

        var result = await _controller.GetFilterContexts();

        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.Single(response.Data!);
    }
}
