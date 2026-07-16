using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MES.Api.Controllers.StandardRegister;
using MES.Core.Models;
using MES.Core.DTOs.StandardRegister;
using MES.Core.Interfaces.StandardRegister;
using System.Security.Claims;

namespace MES.Tests.Controllers;

public class StandardRegisterControllerTests : ControllerTestBase
{
    private readonly Mock<IStandardRegisterService> _serviceMock;
    private readonly StandardRegisterController _controller;

    public StandardRegisterControllerTests()
    {
        _serviceMock = new Mock<IStandardRegisterService>();
        _controller = new StandardRegisterController(_serviceMock.Object);
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
        var pagedResult = new PagedResult<StandardRegisterDto>
        {
            Items = new List<StandardRegisterDto> { new() { Id = 1, StandardNo = "GB/T 14976" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        var result = await _controller.GetPaged();

        var (_, response) = AssertOk<ApiResponse<PagedResult<StandardRegisterDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetPaged_LimitsPageSize()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<StandardRegisterDto> { Items = new List<StandardRegisterDto>() });

        await _controller.GetPaged(pageSize: 10000);

        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.PageSize == 5000)), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesKeyword_ToService()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<StandardRegisterDto> { Items = new List<StandardRegisterDto>() });

        await _controller.GetPaged(keyword: "GB/T");

        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.Keyword == "GB/T")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_DefaultSortBy_IsStandardNo()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<StandardRegisterDto> { Items = new List<StandardRegisterDto>() });

        await _controller.GetPaged();

        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.SortBy == "StandardNo")), Times.Once);
    }

    [Fact]
    public async Task GetPaged_PassesSortBy()
    {
        _serviceMock.Setup(x => x.GetPagedAsync(It.IsAny<QueryParams>()))
            .ReturnsAsync(new PagedResult<StandardRegisterDto> { Items = new List<StandardRegisterDto>() });

        await _controller.GetPaged(sortBy: "StandardName", isDescending: false);

        _serviceMock.Verify(x => x.GetPagedAsync(It.Is<QueryParams>(q => q.SortBy == "StandardName" && q.IsDescending == false)), Times.Once);
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        var dto = new StandardRegisterDto { Id = 1, StandardNo = "GB/T 14976" };
        _serviceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(dto);

        var result = await _controller.GetById(1);

        var (_, response) = AssertOk<ApiResponse<StandardRegisterDto>>(result);
        Assert.Equal("GB/T 14976", response.Data?.StandardNo);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenNull()
    {
        _serviceMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((StandardRegisterDto?)null);

        var result = await _controller.GetById(999);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<StandardRegisterDto>>(notFound.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task Save_ReturnsOk()
    {
        var dto = new StandardRegisterDto { StandardNo = "GB/T 14976" };
        _serviceMock.Setup(x => x.SaveAsync(dto)).ReturnsAsync(42);

        var result = await _controller.Save(dto);

        var (_, response) = AssertOk<ApiResponse<int>>(result);
        Assert.Equal(42, response.Data);
    }

    [Fact]
    public async Task Delete_ReturnsOk()
    {
        _serviceMock.Setup(x => x.DeleteAsync(1)).ReturnsAsync(true);

        var result = await _controller.Delete(1);

        var (_, response) = AssertOk<ApiResponse<bool>>(result);
        Assert.True(response.Data);
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var list = new List<StandardRegisterDto> { new() { Id = 1, StandardNo = "GB/T 14976" } };
        _serviceMock.Setup(x => x.GetAllAsync()).ReturnsAsync(list);

        var result = await _controller.GetAll();

        var (_, response) = AssertOk<ApiResponse<List<StandardRegisterDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task ResolveName_WithStandardNo_ReturnsName()
    {
        _serviceMock.Setup(x => x.ResolveNameAsync("GB/T 14976")).ReturnsAsync("流体输送用无缝钢管");

        var result = await _controller.ResolveName("GB/T 14976");

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<string>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("流体输送用无缝钢管", response.Data);
    }

    [Fact]
    public async Task ResolveName_Empty_ReturnsEmpty()
    {
        var result = await _controller.ResolveName("");

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<string>>(okResult.Value);
        Assert.Equal("", response.Data);
    }

    [Fact]
    public async Task ResolveName_Null_ReturnsEmpty()
    {
        var result = await _controller.ResolveName(null);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<string>>(okResult.Value);
        Assert.Equal("", response.Data);
    }

    [Fact]
    public async Task GetFilterContexts_ReturnsOk()
    {
        var contexts = new Dictionary<string, List<string>> { ["StandardLevel"] = new() { "国标", "行标" } };
        _serviceMock.Setup(x => x.GetFilterContextsAsync()).ReturnsAsync(contexts);

        var result = await _controller.GetFilterContexts();

        var (_, response) = AssertOk<ApiResponse<Dictionary<string, List<string>>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetItems_ReturnsOk()
    {
        var items = new List<StandardRegisterItemDto> { new() { Id = 1, InspectionItem = "拉伸" } };
        _serviceMock.Setup(x => x.GetItemsAsync(1)).ReturnsAsync(items);

        var result = await _controller.GetItems(1);

        var (_, response) = AssertOk<ApiResponse<List<StandardRegisterItemDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task SaveItem_ReturnsOk()
    {
        var dto = new StandardRegisterItemDto { InspectionItem = "拉伸" };
        _serviceMock.Setup(x => x.SaveItemAsync(dto)).ReturnsAsync(42);

        var result = await _controller.SaveItem(dto);

        var (_, response) = AssertOk<ApiResponse<int>>(result);
        Assert.Equal(42, response.Data);
    }

    [Fact]
    public async Task DeleteItem_ReturnsOk()
    {
        _serviceMock.Setup(x => x.DeleteItemAsync(1)).ReturnsAsync(true);

        var result = await _controller.DeleteItem(1);

        var (_, response) = AssertOk<ApiResponse<bool>>(result);
        Assert.True(response.Data);
    }

    [Fact]
    public async Task CleanupOrphanedItems_ReturnsOk()
    {
        _serviceMock.Setup(x => x.CleanupOrphanedItemsAsync()).ReturnsAsync(3);

        var result = await _controller.CleanupOrphanedItems();

        var (_, response) = AssertOk<ApiResponse<int>>(result);
        Assert.Equal(3, response.Data);
    }
}
