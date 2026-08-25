using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Web;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor.Services;
using MES.Blazor.Pages.WorkOrders;
using MES.Blazor.Services;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Core.DTOs.WorkOrder;

namespace MES.Tests.Components;

public class MaterialPlanOverviewTests : IDisposable
{
    private readonly TestContext _ctx = new();
    private readonly RequestSnapshot _captured = new();
    private readonly Mock<Blazored.LocalStorage.ILocalStorageService> _localStorage;
    private Action<PagedResult<WorkOrderListDto>>? _configureResponse;

    public MaterialPlanOverviewTests()
    {
        // ---- 全部服务在构造函数中注册，渲染前不可再注册 ----

        // MudBlazor
        _ctx.Services.AddMudServices();

        // 认证
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "TestUser"),
            new Claim(ClaimTypes.Role, "WorkOrderEditor")
        }, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _ctx.Services.AddSingleton<AuthenticationStateProvider>(
            new TestAuthStateProvider(principal));

        // 导航
        var fakeNav = new FakeNavigationManager(_ctx);
        _ctx.Services.AddSingleton<NavigationManager>(fakeNav);

        // localStorage mock
        _localStorage = new Mock<Blazored.LocalStorage.ILocalStorageService>();
        _localStorage.Setup(x => x.GetItemAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _localStorage.Setup(x => x.GetItemAsStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _ctx.Services.AddSingleton(_localStorage.Object);

        // ColumnPrefsService（依赖 ILocalStorageService）
        _ctx.Services.AddSingleton(new ColumnPrefsService(_localStorage.Object));

        // PageStateService（依赖 ILocalStorageService）
        _ctx.Services.AddSingleton(new PageStateService(_localStorage.Object));

        // JS 运行时 stub（MudBlazor Popover 需要真实 IJSRuntime）
        _ctx.Services.AddSingleton<Microsoft.JSInterop.IJSRuntime>(
            new SilentJsRuntime());

        // ISnackbar mock
        _ctx.Services.AddSingleton(new Mock<MudBlazor.ISnackbar>().Object);

        // Fake HttpClient → AuthHttpClient → WorkOrderService
        var handler = new FakeHandler(req =>
        {
            // 筛选上下文端点：返回空字典
            if (req.RequestUri!.AbsolutePath.Contains("/api/workorder/filter-contexts"))
            {
                var filterJson = JsonSerializer.Serialize(new ApiResponse<Dictionary<string, List<string>>>
                {
                    Success = true,
                    Code = 200,
                    Message = "OK",
                    Data = new Dictionary<string, List<string>>()
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(filterJson, new MediaTypeHeaderValue("application/json"))
                };
            }

            // 列表端点：捕获查询参数
            if (req.RequestUri!.AbsolutePath.Contains("/api/workorder/list"))
            {
                var q = HttpUtility.ParseQueryString(req.RequestUri.Query);
                _captured.PageIndex = int.TryParse(q["pageIndex"], out var pi) ? pi : -1;
                _captured.PageSize = int.TryParse(q["pageSize"], out var ps) ? ps : -1;
                _captured.MaterialPlanStatus = q["materialPlanStatus"] is { Length: > 0 } s
                    ? (MaterialPlanStatus)int.Parse(s) : null;
                _captured.MainNoStatus = q["mainNoMaterialPlanStatus"] is { Length: > 0 } ms
                    ? (MaterialPlanStatus)int.Parse(ms) : null;
                _captured.OrderStatus = q["orderMaterialPlanStatus"] is { Length: > 0 } os
                    ? (MaterialPlanStatus)int.Parse(os) : null;
            }

            var result = new PagedResult<WorkOrderListDto>
            {
                Items = new List<WorkOrderListDto>(),
                TotalCount = 0,
                PageIndex = 1,
                PageSize = 10
            };
            _configureResponse?.Invoke(result);

            var json = JsonSerializer.Serialize(new ApiResponse<PagedResult<WorkOrderListDto>>
            {
                Success = true,
                Code = 200,
                Message = "OK",
                Data = result
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, new MediaTypeHeaderValue("application/json"))
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7001") };
        var authHttpClient = new AuthHttpClient(httpClient, _localStorage.Object, fakeNav);
        _ctx.Services.AddSingleton(new WorkOrderService(authHttpClient));
        _ctx.Services.AddSingleton(new MaterialPlanService(authHttpClient));
    }

    private void ConfigureResponse(Action<PagedResult<WorkOrderListDto>> configure)
        => _configureResponse = configure;

    private IRenderedComponent<MaterialPlanOverview> Render()
        => _ctx.RenderComponent<MaterialPlanOverview>();

    // ========== 分页 ==========

    [Fact]
    public void PageIndex_FirstPage_IsOne()
    {
        var cut = Render();
        cut.WaitForState(() => _captured.PageIndex > 0);
        _captured.PageIndex.Should().Be(1);
    }

    [Fact]
    public void PageSize_MatchesRowsPerPage()
    {
        var cut = Render();
        cut.WaitForState(() => _captured.PageSize > 0);
        _captured.PageSize.Should().Be(10);
    }

    // ========== 筛选 ==========

    [Fact]
    public void NoFilters_SendsNullStatusParams()
    {
        var cut = Render();
        cut.WaitForState(() => _captured.PageIndex > 0);
        _captured.MaterialPlanStatus.Should().BeNull();
        _captured.MainNoStatus.Should().BeNull();
        _captured.OrderStatus.Should().BeNull();
    }

    // ========== 渲染 ==========

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Render();
        cut.Markup.Should().Contain("用料计划总览");
    }

    [Fact]
    public void Render_HasFilters()
    {
        var cut = Render();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Fact]
    public void Render_WithData_ShowsRows()
    {
        ConfigureResponse(r =>
        {
            r.Items = new List<WorkOrderListDto>
            {
                new() { Id = 1, WorkOrderNo = "WO-TEST-001", SalesOrderNo = "SO-001",
                    ProductionMainNo = "M-001", ProductionSubNo = "001", MaterialPlanStatus = (MaterialPlanStatus)2,
                    MaterialPlanRate = 100, MainNoMaterialPlanStatus = (MaterialPlanStatus)2, OrderMaterialPlanStatus = (MaterialPlanStatus)2 }
            };
            r.TotalCount = 1;
        });
        var cut = Render();
        cut.WaitForState(() => cut.Markup.Contains("WO-TEST-001"));
    }

    // ========== 状态文本 ==========

    [Theory]
    [InlineData(0, "未计划")]
    [InlineData(1, "部分")]
    [InlineData(2, "满足")]
    [InlineData(3, "超量")]
    public void StatusText_ShowsCorrectLabel(int status, string expected)
    {
        ConfigureResponse(r =>
        {
            r.Items = new List<WorkOrderListDto>
            {
                new() { Id = 1, WorkOrderNo = "WO-001", SalesOrderNo = "SO-001",
                    ProductionMainNo = "M-001", ProductionSubNo = "001",
                    MaterialPlanStatus = (MaterialPlanStatus)status, MaterialPlanRate = 0,
                    MainNoMaterialPlanStatus = MaterialPlanStatus.NotPlanned, OrderMaterialPlanStatus = MaterialPlanStatus.NotPlanned }
            };
            r.TotalCount = 1;
        });
        var cut = Render();
        cut.WaitForState(() => cut.Markup.Contains(expected));
    }

    [Theory]
    [InlineData(0, "未计划")]
    [InlineData(1, "部分")]
    [InlineData(2, "满足")]
    public void OrderStatusText_ShowsCorrectLabel(int status, string expected)
    {
        // OrderMaterialPlanStatus 默认隐藏，模拟列偏好将其显示
        _localStorage.Setup(x => x.GetItemAsStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Json.JsonSerializer.Serialize(new[]
            {
                new { k = "OrderMaterialPlanStatus", v = true }
            }));
        ConfigureResponse(r =>
        {
            r.Items = new List<WorkOrderListDto>
            {
                new() { Id = 1, WorkOrderNo = "WO-001", SalesOrderNo = "SO-001",
                    ProductionMainNo = "M-001", ProductionSubNo = "001",
                    MaterialPlanStatus = MaterialPlanStatus.NotPlanned, MaterialPlanRate = 0,
                    MainNoMaterialPlanStatus = MaterialPlanStatus.NotPlanned, OrderMaterialPlanStatus = (MaterialPlanStatus)status }
            };
            r.TotalCount = 1;
        });
        var cut = Render();
        cut.WaitForState(() => cut.Markup.Contains(expected));
    }

    // ========== 辅助 ==========

    private class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _fn;
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> fn) => _fn = fn;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage r, CancellationToken ct) => Task.FromResult(_fn(r));
    }

    public class RequestSnapshot
    {
        public int PageIndex { get; set; } = -1;
        public int PageSize { get; set; } = -1;
        public MaterialPlanStatus? MaterialPlanStatus { get; set; }
        public MaterialPlanStatus? MainNoStatus { get; set; }
        public MaterialPlanStatus? OrderStatus { get; set; }
    }

    public void Dispose() => _ctx.Dispose();
}
