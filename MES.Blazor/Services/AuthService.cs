using System.Net.Http.Json;
using Microsoft.JSInterop;
using MES.Blazor.Models;
using System.Text.Json.Serialization;

namespace MES.Blazor.Services;

public interface IAuthService
{
    Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request);
    Task LogoutAsync();
    Task<UserInfo?> GetCurrentUserAsync();
    Task<bool> IsAuthenticatedAsync();
}

public class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public T? Data { get; set; }
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
            await LogToConsole("========== Starting Login Request ==========");
            await LogToConsole($"HttpClient BaseAddress: {_httpClient.BaseAddress}");
            await LogToConsole($"Request URL: api/Auth/login");
            await LogToConsole($"Username: {request.Username}");

            var response = await _httpClient.PostAsJsonAsync("api/Auth/login", request);

            // Read raw response for debugging
            var responseContent = await response.Content.ReadAsStringAsync();
            await LogToConsole($"HTTP Response Status: {response.StatusCode}");
            await LogToConsole($"Response Content: {responseContent}");

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = $"API request failed ({(int)response.StatusCode})";
                await LogToConsole(errorMsg);
                return new ApiResponse<LoginResponse>
                {
                    Success = false,
                    Message = $"{errorMsg}"
                };
            }

            // Check if response content is empty
            if (string.IsNullOrWhiteSpace(responseContent))
            {
                await LogToConsole("API returned empty response");
                return new ApiResponse<LoginResponse>
                {
                    Success = false,
                    Message = "API returned empty response"
                };
            }

            await LogToConsole("Attempting to deserialize response...");
            var result = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<LoginResponse>>(responseContent);
            await LogToConsole($"Deserialize Result - Success={result?.Success}, Message={result?.Message}");
            await LogToConsole($"Data is null: {result?.Data == null}");

            if (result != null && result.Success && result.Data != null)
            {
                await LogToConsole("========== Saving Token and User Info ==========");
                await LogToConsole($"Token: {result.Data.Token?.Substring(0, Math.Min(50, result.Data.Token?.Length ?? 0))}...");
                await LogToConsole($"Username: {result.Data.Username}");
                await LogToConsole($"Email: {result.Data.Email}");
                await LogToConsole($"Roles: {string.Join(", ", result.Data.Roles ?? new())}");

                // Save Token
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TOKEN_KEY, result.Data.Token);
                await LogToConsole("Token saved to localStorage");

                // Save user info
                var userInfo = new UserInfo
                {
                    Username = result.Data.Username,
                    Email = result.Data.Email,
                    Roles = result.Data.Roles ?? new(),
                    IsAuthenticated = true
                };

                await LogToConsole("Serializing userInfo...");
                var userInfoJson = System.Text.Json.JsonSerializer.Serialize(userInfo);
                await LogToConsole($"UserInfo JSON: {userInfoJson}");

                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", USER_KEY, userInfoJson);
                await LogToConsole("User info saved to localStorage");

                await LogToConsole("Notifying auth state provider...");
                // Notify auth state change
                _authStateProvider.MarkUserAsAuthenticated(userInfo);
                await LogToConsole("Auth state provider notified");

                await LogToConsole("========== Login Process Succeeded ==========");
            }
            else
            {
                if (result == null)
                {
                    await LogToConsole("ERROR: Result is null after deserialization");
                }
                else if (!result.Success)
                {
                    await LogToConsole($"ERROR: API returned Success={result.Success}, Message={result.Message}");
                }
                else if (result.Data == null)
                {
                    await LogToConsole("ERROR: Result.Success=true but Data is null");
                }
            }

            return result ?? new ApiResponse<LoginResponse> { Success = false, Message = "Login failed: unable to parse response" };
        }
        catch (System.Text.Json.JsonException ex)
        {
            await LogError($"JSON parsing error: {ex.Message}");
            await LogError($"Stack trace: {ex.StackTrace}");
            return new ApiResponse<LoginResponse> { Success = false, Message = $"Response format error: {ex.Message}" };
        }
        catch (Exception ex)
        {
            await LogError($"Login exception: {ex}");
            await LogError($"Stack trace: {ex.StackTrace}");
            return new ApiResponse<LoginResponse> { Success = false, Message = $"Login failed: {ex.Message}" };
        }
    }

    private async Task LogToConsole(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
        try
        {
            await _jsRuntime.InvokeVoidAsync("MesLog", message);
        }
        catch { }
    }

    private async Task LogError(string message)
    {
        System.Diagnostics.Debug.WriteLine($"ERROR: {message}");
        try
        {
            await _jsRuntime.InvokeVoidAsync("MesError", message);
        }
        catch { }
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
