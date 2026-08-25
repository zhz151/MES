using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Blazored.LocalStorage;
using FluentAssertions;
using MES.Blazor.Services;
using Moq;
using Xunit;

namespace MES.Tests;

/// <summary>
/// JWT 多角色解析测试：role 为数组时须逐元素展开为多条 ClaimTypes.Role，
/// 否则多角色用户前端菜单门控静默失效。
/// </summary>
public class CustomAuthStateProviderTests
{
    private static string Encode(string s)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(s))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string MakeJwt(string payloadJson)
        => $"{Encode(JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" }))}.{Encode(payloadJson)}.sig";

    private static (CustomAuthStateProvider Provider, Mock<ILocalStorageService> Storage) CreateProvider()
    {
        var storage = new Mock<ILocalStorageService>();
        var provider = new CustomAuthStateProvider(new HttpClient(), storage.Object);
        return (provider, storage);
    }

    [Fact]
    public async Task RoleArray_ParsesToMultipleRoleClaims()
    {
        var (provider, storage) = CreateProvider();
        var token = MakeJwt(@"{""sub"":""u1"",""role"":[""OrderViewer"",""Admin""]}");
        storage.Setup(s => s.GetItemAsync<string>("authToken", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.IsInRole("OrderViewer").Should().BeTrue();
        state.User.IsInRole("Admin").Should().BeTrue();
        state.User.Claims.Where(c => c.Type == ClaimTypes.Role).Should().HaveCount(2);
    }

    [Fact]
    public async Task FullUriRoleArray_ParsesToMultipleRoleClaims()
    {
        // 后端 JwtService 用 new Claim(ClaimTypes.Role, role) 生成，序列化后 key 为完整 URI
        var (provider, storage) = CreateProvider();
        var token = MakeJwt(@"{""sub"":""u1"",""http://schemas.microsoft.com/ws/2008/06/identity/claims/role"":[""OrderViewer"",""Admin""]}");
        storage.Setup(s => s.GetItemAsync<string>("authToken", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.IsInRole("OrderViewer").Should().BeTrue();
        state.User.IsInRole("Admin").Should().BeTrue();
        state.User.Claims.Where(c => c.Type == ClaimTypes.Role).Should().HaveCount(2);
    }

    [Fact]
    public async Task FullUriRoleSingleString_ParsesToSingleRoleClaim()
    {
        var (provider, storage) = CreateProvider();
        var token = MakeJwt(@"{""sub"":""u1"",""http://schemas.microsoft.com/ws/2008/06/identity/claims/role"":""OrderViewer""}");
        storage.Setup(s => s.GetItemAsync<string>("authToken", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.IsInRole("OrderViewer").Should().BeTrue();
        state.User.IsInRole("Admin").Should().BeFalse();
        state.User.Claims.Where(c => c.Type == ClaimTypes.Role).Should().HaveCount(1);
    }

    [Fact]
    public async Task SingleRoleString_ParsesToSingleRoleClaim()
    {
        var (provider, storage) = CreateProvider();
        var token = MakeJwt(@"{""sub"":""u1"",""role"":""OrderViewer""}");
        storage.Setup(s => s.GetItemAsync<string>("authToken", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.IsInRole("OrderViewer").Should().BeTrue();
        state.User.IsInRole("Admin").Should().BeFalse();
        state.User.Claims.Where(c => c.Type == ClaimTypes.Role).Should().HaveCount(1);
    }

    [Fact]
    public async Task Base64UrlPayload_WithDashAndUnderscore_ParsesRoles()
    {
        // 真实 JWT 用 base64url 编码（-/_ 代替 +//）。payload 含中文等字节时 base64 必然含 +//，
        // 转成 base64url 后出现 -/_。旧实现 Convert.FromBase64String 遇 -/_ 抛 FormatException 被 catch 吞掉，
        // 返回空 claims → 角色丢失、菜单门控全灭（真库 CJ 用户 token 第 174 位即命中）。此用例防回归。
        var (provider, storage) = CreateProvider();
        var token = MakeJwt("{\"sub\":\"u1\",\"role\":[\"OrderViewer\",\"Admin\"],\"fullName\":\"测试\"}");
        storage.Setup(s => s.GetItemAsync<string>("authToken", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.IsInRole("OrderViewer").Should().BeTrue();
        state.User.IsInRole("Admin").Should().BeTrue();
        state.User.Claims.Where(c => c.Type == ClaimTypes.Role).Should().HaveCount(2);
    }

    [Fact]
    public async Task EmptyToken_ReturnsAnonymous()
    {
        var (provider, storage) = CreateProvider();
        storage.Setup(s => s.GetItemAsync<string>("authToken", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity?.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task CorruptToken_ParsesNoClaims()
    {
        var (provider, storage) = CreateProvider();
        storage.Setup(s => s.GetItemAsync<string>("authToken", It.IsAny<CancellationToken>()))
            .ReturnsAsync("not-a-jwt");

        var state = await provider.GetAuthenticationStateAsync();

        // 损坏 token 解析失败 → 空 claims（identity 仍有 "jwt" 认证类型，故无任何角色）
        state.User.Claims.Should().BeEmpty();
        state.User.IsInRole("Admin").Should().BeFalse();
    }
}
