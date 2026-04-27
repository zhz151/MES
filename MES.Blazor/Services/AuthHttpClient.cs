// 文件路径: MES.Blazor/Services/AuthHttpClient.cs
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;

namespace MES.Blazor.Services;

/// <summary>
/// 带认证功能的 HTTP 客户端
/// </summary>
public class AuthHttpClient
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _localStorage;

    public AuthHttpClient(HttpClient http, ILocalStorageService localStorage)
    {
        _http = http;
        _localStorage = localStorage;
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
    /// GET 请求
    /// </summary>
    public async Task<HttpResponseMessage> GetAsync(string url)
    {
        await AddAuthHeaderAsync();
        return await _http.GetAsync(url);
    }

    /// <summary>
    /// GET 请求并反序列化为 T（含非2xx响应的JSON反序列化，保留业务错误消息）
    /// </summary>
    public async Task<T?> GetFromJsonAsync<T>(string url)
    {
        await AddAuthHeaderAsync();
        var response = await _http.GetAsync(url);
        var json = await response.Content.ReadAsStringAsync();

        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                if (response.IsSuccessStatusCode)
                    throw;
            }
        }

        return default;
    }

    /// <summary>
    /// POST 请求
    /// </summary>
    public async Task<HttpResponseMessage> PostAsJsonAsync<T>(string url, T data)
    {
        await AddAuthHeaderAsync();
        return await _http.PostAsJsonAsync(url, data);
    }

    /// <summary>
    /// POST 请求并反序列化响应（含非2xx响应的JSON反序列化，保留业务错误消息）
    /// </summary>
    public async Task<TResponse?> PostAsJsonAsync<TRequest, TResponse>(string url, TRequest data)
    {
        await AddAuthHeaderAsync();
        var response = await _http.PostAsJsonAsync(url, data);
        var json = await response.Content.ReadAsStringAsync();

        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                return JsonSerializer.Deserialize<TResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                if (response.IsSuccessStatusCode)
                    throw;
            }
        }

        return default;
    }

    /// <summary>
    /// PUT 请求
    /// </summary>
    public async Task<HttpResponseMessage> PutAsJsonAsync<T>(string url, T data)
    {
        await AddAuthHeaderAsync();
        return await _http.PutAsJsonAsync(url, data);
    }

    /// <summary>
    /// PUT 请求并反序列化响应（含非2xx响应的JSON反序列化，保留业务错误消息）
    /// </summary>
    public async Task<TResponse?> PutAsJsonAsync<TRequest, TResponse>(string url, TRequest data)
    {
        await AddAuthHeaderAsync();
        var response = await _http.PutAsJsonAsync(url, data);
        var json = await response.Content.ReadAsStringAsync();

        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                return JsonSerializer.Deserialize<TResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                if (response.IsSuccessStatusCode)
                    throw;
            }
        }

        return default;
    }

    /// <summary>
    /// DELETE 请求
    /// </summary>
    public async Task<HttpResponseMessage> DeleteAsync(string url)
    {
        await AddAuthHeaderAsync();
        return await _http.DeleteAsync(url);
    }

    /// <summary>
    /// DELETE 请求并反序列化响应（含非2xx响应的JSON反序列化，保留业务错误消息）
    /// </summary>
    public async Task<T?> DeleteFromJsonAsync<T>(string url)
    {
        await AddAuthHeaderAsync();
        var response = await _http.DeleteAsync(url);
        var json = await response.Content.ReadAsStringAsync();

        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                if (response.IsSuccessStatusCode)
                    throw;
            }
        }

        return default;
    }
}