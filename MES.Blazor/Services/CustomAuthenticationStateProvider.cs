using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using MES.Blazor.Models;

namespace MES.Blazor.Services;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _jsRuntime;
    private const string USER_KEY = "user_info";
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());
    private bool _isInitialized = false;

    public CustomAuthenticationStateProvider(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var userJson = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", USER_KEY);
            Console.WriteLine($"[AuthState] GetAuthenticationStateAsync - userJson length: {(userJson?.Length ?? 0)}");

            if (!string.IsNullOrEmpty(userJson))
            {
                Console.WriteLine("[AuthState] Parsing user info from localStorage...");
                var userInfo = System.Text.Json.JsonSerializer.Deserialize<UserInfo>(userJson);
                Console.WriteLine($"[AuthState] Parsed userInfo - Username: {userInfo?.Username}, IsAuthenticated: {userInfo?.IsAuthenticated}");

                if (userInfo != null && userInfo.IsAuthenticated)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, userInfo.Username),
                        new Claim(ClaimTypes.Email, userInfo.Email)
                    };

                    foreach (var role in userInfo.Roles)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, role));
                    }

                    Console.WriteLine($"[AuthState] Creating authenticated user with {claims.Count} claims");
                    var identity = new ClaimsIdentity(claims, "jwt");
                    _currentUser = new ClaimsPrincipal(identity);
                    _isInitialized = true;
                    return new AuthenticationState(_currentUser);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuthState] GetAuthenticationStateAsync error: {ex.Message}");
        }

        Console.WriteLine("[AuthState] Returning unauthenticated user");
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        _isInitialized = true;
        return new AuthenticationState(_currentUser);
    }

    public void MarkUserAsAuthenticated(UserInfo userInfo)
    {
        Console.WriteLine($"[AuthState] ========== MarkUserAsAuthenticated ==========");
        Console.WriteLine($"[AuthState] Username: {userInfo.Username}");

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, userInfo.Username),
            new Claim(ClaimTypes.Email, userInfo.Email)
        };

        foreach (var role in userInfo.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        Console.WriteLine($"[AuthState] Creating ClaimsPrincipal with {claims.Count} claims");
        var identity = new ClaimsIdentity(claims, "jwt");
        _currentUser = new ClaimsPrincipal(identity);

        Console.WriteLine("[AuthState] Calling NotifyAuthenticationStateChanged...");
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));

        Console.WriteLine("[AuthState] NotifyAuthenticationStateChanged completed");
        Console.WriteLine($"[AuthState] Current user authenticated: {_currentUser.Identity?.IsAuthenticated}");
        Console.WriteLine($"[AuthState] Current user name: {_currentUser.Identity?.Name}");
        Console.WriteLine($"[AuthState] Current user has claims: {_currentUser.Claims?.Count()}");
        Console.WriteLine($"[AuthState] ========== MarkUserAsAuthenticated Completed ==========");
    }

    public void MarkUserAsLoggedOut()
    {
        Console.WriteLine("[AuthState] ========== MarkUserAsLoggedOut ==========");
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
        Console.WriteLine("[AuthState] ========== MarkUserAsLoggedOut Completed ==========");
    }
}
