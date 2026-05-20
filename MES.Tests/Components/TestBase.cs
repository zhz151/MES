using System.Security.Claims;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using MudBlazor.Services;
using MES.Blazor.Services;

namespace MES.Tests.Components;

/// <summary>
/// 组件测试基类，封装 bUnit TestContext 的通用注册逻辑
/// </summary>
public abstract class TestBase : IDisposable
{
    protected readonly TestContext Ctx = new();
    protected readonly FakeHttpMessageHandler HttpHandler = new();

    private readonly NavigationManager _nav;
    private readonly Blazored.LocalStorage.ILocalStorageService _localStorage;

    protected TestBase()
    {
        // MudBlazor
        Ctx.Services.AddMudServices();

        // 认证
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "TestUser"),
            new Claim(ClaimTypes.Role, "Admin")
        }, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        Ctx.Services.AddSingleton<AuthenticationStateProvider>(
            new TestAuthStateProvider(principal));

        // 导航
        _nav = new FakeNavigationManager(Ctx);
        Ctx.Services.AddSingleton<NavigationManager>(_nav);

        // localStorage mock
        var localStorageMock = new Mock<Blazored.LocalStorage.ILocalStorageService>();
        localStorageMock.Setup(x => x.GetItemAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _localStorage = localStorageMock.Object;
        Ctx.Services.AddSingleton(_localStorage);

        // ColumnPrefsService
        Ctx.Services.AddSingleton(new ColumnPrefsService(_localStorage));

        // Silent JS Runtime
        Ctx.Services.AddSingleton<IJSRuntime>(new SilentJsRuntime());

        // ISnackbar mock
        Ctx.Services.AddSingleton(new Mock<MudBlazor.ISnackbar>().Object);

        // IDialogService mock
        Ctx.Services.AddSingleton(new Mock<MudBlazor.IDialogService>().Object);

        // IAuthorizationPolicyProvider mock（用于 AuthorizeView 组件）
        var authPolicyMock = new Mock<IAuthorizationPolicyProvider>();
        authPolicyMock.Setup(x => x.GetPolicyAsync(It.IsAny<string>()))
            .ReturnsAsync(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        authPolicyMock.Setup(x => x.GetDefaultPolicyAsync())
            .ReturnsAsync(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        Ctx.Services.AddSingleton(authPolicyMock.Object);

        // IAuthorizationService mock（用于 AuthorizeView Roles="..." 授权检查）
        var authServiceMock = new Mock<IAuthorizationService>();
        authServiceMock.Setup(x => x.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        authServiceMock.Setup(x => x.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());
        Ctx.Services.AddSingleton(authServiceMock.Object);
    }

    /// <summary>
    /// 注册 Blazor Services（BatchService / PurchaseOrderService 等）
    /// </summary>
    protected void RegisterServices(params Type[] serviceTypes)
    {
        var httpClient = new HttpClient(HttpHandler) { BaseAddress = new Uri("https://localhost:7001") };
        var authHttpClient = new AuthHttpClient(httpClient, _localStorage, _nav);

        foreach (var type in serviceTypes)
        {
            var service = Activator.CreateInstance(type, authHttpClient);
            if (service != null)
                Ctx.Services.AddSingleton(type, service);
        }
    }

    /// <summary>
    /// 配置 HTTP 响应路由：路径前缀 → 响应内容
    /// </summary>
    protected void ConfigureResponse(string pathPrefix, object response, string method = "GET")
    {
        HttpHandler.Configure(pathPrefix, response, method);
    }

    /// <summary>
    /// 配置返回空数据的默认端点
    /// </summary>
    protected void ConfigureEmptyResponse(string pathPrefix, string method = "GET")
    {
        HttpHandler.ConfigureEmpty(pathPrefix, method);
    }

    public void Dispose() => Ctx.Dispose();

    /// <summary>
    /// Fake HttpMessageHandler，支持按路径前缀匹配返回 JSON
    /// </summary>
    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (object? Response, string Method)> _routes = new();
        private readonly HashSet<string> _capturePaths = new();
        public List<KeyValuePair<string, string>> CapturedQueries { get; } = new();

        public void Configure(string pathPrefix, object response, string method = "GET")
        {
            _routes[Key(pathPrefix, method)] = (response, method);
        }

        public void ConfigureEmpty(string pathPrefix, string method = "GET")
        {
            Configure(pathPrefix, new { }, method);
        }

        public void CaptureQueryFor(string pathPrefix)
        {
            _capturePaths.Add(pathPrefix.ToLowerInvariant());
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri?.AbsolutePath?.ToLowerInvariant() ?? "";
            var method = request.Method.ToString().ToUpperInvariant();

            if (_capturePaths.Any(p => path.Contains(p.ToLowerInvariant())))
            {
                var query = request.RequestUri?.Query ?? "";
                CapturedQueries.Add(new KeyValuePair<string, string>(path, query));
            }

            // 精确匹配
            if (_routes.TryGetValue(Key(path, method), out var route))
            {
                return Task.FromResult(MakeResponse(route.Response));
            }

            // 前缀匹配
            foreach (var kvp in _routes)
            {
                var keyParts = kvp.Key.Split('|');
                var routePath = keyParts[0];
                var routeMethod = keyParts[1];

                if (routeMethod == method && path.StartsWith(routePath))
                {
                    return Task.FromResult(MakeResponse(kvp.Value.Response));
                }
            }

            // 默认 404
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage MakeResponse(object? data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(json,
                    System.Text.Encoding.UTF8, "application/json")
            };
        }

        private static string Key(string path, string method) => $"{path.ToLowerInvariant()}|{method.ToUpperInvariant()}";
    }
}
