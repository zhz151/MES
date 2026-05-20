using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace MES.Tests.Components;

/// <summary>
/// 始终返回固定认证状态的 AuthenticationStateProvider
/// </summary>
internal class TestAuthStateProvider : AuthenticationStateProvider
{
    private readonly AuthenticationState _state;
    public TestAuthStateProvider(ClaimsPrincipal principal)
        => _state = new AuthenticationState(principal);
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(_state);
}

/// <summary>
/// IJSRuntime 桩：所有 JS 调用静默成功，不抛异常
/// </summary>
internal class SilentJsRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => default!;

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
        => default!;
}
