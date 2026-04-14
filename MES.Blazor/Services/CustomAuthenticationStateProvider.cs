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

    public CustomAuthenticationStateProvider(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var userJson = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", USER_KEY);
            
            if (!string.IsNullOrEmpty(userJson))
            {
                var userInfo = System.Text.Json.JsonSerializer.Deserialize<UserInfo>(userJson);
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
                    
                    var identity = new ClaimsIdentity(claims, "jwt");
                    _currentUser = new ClaimsPrincipal(identity);
                    return new AuthenticationState(_currentUser);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAuthenticationStateAsync error: {ex.Message}");
        }
        
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        return new AuthenticationState(_currentUser);
    }

    public void MarkUserAsAuthenticated(UserInfo userInfo)
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
        
        var identity = new ClaimsIdentity(claims, "jwt");
        _currentUser = new ClaimsPrincipal(identity);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    public void MarkUserAsLoggedOut()
    {
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }
}
