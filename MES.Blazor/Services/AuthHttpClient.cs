// 文件路径: MES.Blazor/Services/AuthHttpClient.cs
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using MES.Core.Models;
using MES.Core.DTOs.Auth;

namespace MES.Blazor.Services;

/// <summary>
/// 带认证功能的 HTTP 客户端，支持 401 自动刷新 Token
/// </summary>
public class AuthHttpClient
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _localStorage;
    private readonly NavigationManager _navigation;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AuthHttpClient(HttpClient http, ILocalStorageService localStorage, NavigationManager navigation)
    {
        _http = http;
        _localStorage = localStorage;
        _navigation = navigation;
    }

    /// <summary>
    /// 添加认证 Token 到请求头
    /// </summary>
    private async Task AddAuthHeaderAsync()
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");
        if (!string.IsNullOrEmpty(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    /// <summary>
    /// 尝试刷新 Token，返回 true 表示刷新成功
    /// </summary>
    private async Task<bool> TryRefreshTokenAsync()
    {
        var refreshToken = await _localStorage.GetItemAsync<string>("refreshToken");
        if (string.IsNullOrEmpty(refreshToken))
            return false;

        // 清除旧的 Authorization header，避免干扰刷新请求
        _http.DefaultRequestHeaders.Authorization = null;

        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/refresh-token", new { refreshToken });
            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync();
            var authResponse = JsonSerializer.Deserialize<ApiResponse<LoginResponse>>(json, JsonOptions);
            if (authResponse?.Success != true || authResponse.Data == null)
                return false;

            // 存储新的 Token 和 RefreshToken
            await _localStorage.SetItemAsync("authToken", authResponse.Data.Token);
            await _localStorage.SetItemAsync("refreshToken", authResponse.Data.RefreshToken);
            await _localStorage.SetItemAsync("userEmail", authResponse.Data.Email);
            await _localStorage.SetItemAsync("userName", authResponse.Data.UserName);
            await _localStorage.SetItemAsync("userFullName", authResponse.Data.FullName);
            await _localStorage.SetItemAsync("userRoles", authResponse.Data.Roles);

            // 更新当前请求的 Authorization header
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResponse.Data.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 清除认证信息并跳转到登录页
    /// </summary>
    private async Task ClearAuthAndRedirectAsync()
    {
        await _localStorage.RemoveItemAsync("authToken");
        await _localStorage.RemoveItemAsync("refreshToken");
        await _localStorage.RemoveItemAsync("userEmail");
        await _localStorage.RemoveItemAsync("userName");
        await _localStorage.RemoveItemAsync("userFullName");
        await _localStorage.RemoveItemAsync("userRoles");
        _http.DefaultRequestHeaders.Authorization = null;
        _navigation.NavigateTo("/login", true);
    }

    /// <summary>
    /// 执行请求并在 401 时自动刷新 Token 重试
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRefreshAsync(Func<Task<HttpResponseMessage>> sendFunc)
    {
        await AddAuthHeaderAsync();
        var response = await sendFunc();

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            var refreshed = await TryRefreshTokenAsync();
            if (refreshed)
            {
                response = await sendFunc();
            }
            else
            {
                await ClearAuthAndRedirectAsync();
            }
        }

        return response;
    }

    /// <summary>
    /// 执行请求，反序列化响应，并在 401 时自动刷新 Token 重试
    /// </summary>
    private async Task<T?> SendAndDeserializeAsync<T>(Func<Task<HttpResponseMessage>> sendFunc)
    {
        var response = await SendWithRefreshAsync(sendFunc);
        return await DeserializeResponseAsync<T>(response);
    }

    /// <summary>
    /// 反序列化响应内容
    /// </summary>
    private static async Task<T?> DeserializeResponseAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(json))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            if (response.IsSuccessStatusCode)
                throw;
            return default;
        }
    }

    /// <summary>
    /// GET 请求
    /// </summary>
    public Task<HttpResponseMessage> GetAsync(string url)
        => SendWithRefreshAsync(() => _http.GetAsync(url));

    /// <summary>
    /// GET 请求并反序列化为 T
    /// </summary>
    public Task<T?> GetFromJsonAsync<T>(string url)
        => SendAndDeserializeAsync<T>(() => _http.GetAsync(url));

    /// <summary>
    /// POST 请求
    /// </summary>
    public Task<HttpResponseMessage> PostAsJsonAsync<T>(string url, T data)
        => SendWithRefreshAsync(() => _http.PostAsJsonAsync(url, data));

    /// <summary>
    /// POST 请求并反序列化响应
    /// </summary>
    public Task<TResponse?> PostAsJsonAsync<TRequest, TResponse>(string url, TRequest data)
        => SendAndDeserializeAsync<TResponse>(() => _http.PostAsJsonAsync(url, data));

    /// <summary>
    /// PUT 请求
    /// </summary>
    public Task<HttpResponseMessage> PutAsJsonAsync<T>(string url, T data)
        => SendWithRefreshAsync(() => _http.PutAsJsonAsync(url, data));

    /// <summary>
    /// PUT 请求并反序列化响应
    /// </summary>
    public Task<TResponse?> PutAsJsonAsync<TRequest, TResponse>(string url, TRequest data)
        => SendAndDeserializeAsync<TResponse>(() => _http.PutAsJsonAsync(url, data));

    /// <summary>
    /// DELETE 请求
    /// </summary>
    public Task<HttpResponseMessage> DeleteAsync(string url)
        => SendWithRefreshAsync(() => _http.DeleteAsync(url));

    /// <summary>
    /// DELETE 请求并反序列化响应
    /// </summary>
    public Task<T?> DeleteFromJsonAsync<T>(string url)
        => SendAndDeserializeAsync<T>(() => _http.DeleteAsync(url));
}