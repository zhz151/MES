using System.Net.Http.Json;
using Microsoft.JSInterop;
using MES.Blazor.Models;

namespace MES.Blazor.Services;

public interface IAuthService
{
    Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request);
    Task LogoutAsync();
    Task<UserInfo?> GetCurrentUserAsync();
    Task<bool> IsAuthenticatedAsync();
}

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private readonly CustomAuthenticationStateProvider _authStateProvider;
    private const string TOKEN_KEY = "auth_token";
    private const string USER_KEY = "user_info";

    public AuthService(HttpClient httpClient, IJSRuntime jsRuntime, CustomAuthenticationStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
        _authStateProvider = authStateProvider;
    }

    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Auth/login", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            
            if (result != null && result.Success && result.Data != null)
            {
                // 保存 Token
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TOKEN_KEY, result.Data.Token);
                
                // 保存用户信息
                var userInfo = new UserInfo
                {
                    Username = result.Data.Username,
                    Email = result.Data.Email,
                    Roles = result.Data.Roles,
                    IsAuthenticated = true
                };
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", USER_KEY, System.Text.Json.JsonSerializer.Serialize(userInfo));
                
                // 通知认证状态变化
                _authStateProvider.MarkUserAsAuthenticated(userInfo);
            }
            
            return result ?? new ApiResponse<LoginResponse> { Success = false, Message = "登录失败" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<LoginResponse> { Success = false, Message = ex.Message };
        }
    }

    public async Task LogoutAsync()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TOKEN_KEY);
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", USER_KEY);
        _authStateProvider.MarkUserAsLoggedOut();
    }

    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        try
        {
            var userJson = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", USER_KEY);
            if (!string.IsNullOrEmpty(userJson))
            {
                return System.Text.Json.JsonSerializer.Deserialize<UserInfo>(userJson);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetCurrentUserAsync error: {ex.Message}");
        }
        return null;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", TOKEN_KEY);
        return !string.IsNullOrEmpty(token);
    }
}
