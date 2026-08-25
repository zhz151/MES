using System.Net.Http.Headers;
using System.Security.Claims;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Text.Json;

namespace MES.Blazor.Services;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _localStorage;

    public CustomAuthStateProvider(HttpClient http, ILocalStorageService localStorage)
    {
        _http = http;
        _localStorage = localStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");
        if (string.IsNullOrEmpty(token))
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", token);
        var identity = new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt");
        var user = new ClaimsPrincipal(identity);
        return new AuthenticationState(user);
    }

    public void MarkUserAsLoggedIn(string token)
    {
        var identity = new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt");
        var user = new ClaimsPrincipal(identity);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    public async Task MarkUserAsLoggedOut()
    {
        await _localStorage.RemoveItemAsync("authToken");
        _http.DefaultRequestHeaders.Authorization = null;
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        try
        {
            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
            return keyValuePairs?.SelectMany(kvp =>
            {
                // JWT 角色 claim 可能以短名 "role"/"roles"，也可能以完整 URI（ClaimTypes.Role，
                // 即 http://schemas.microsoft.com/ws/2008/06/identity/claims/role）出现——
                // 后端 JwtService 用 new Claim(ClaimTypes.Role, role) 生成且序列化时不映射短名。
                // 多角色时值为数组，须逐元素展开为多条 Role claim，Blazor 的 IsInRole 才能精确匹配；
                // 否则 kvp.Value.ToString() 输出 JsonElement 原文（JSON 数组字符串）导致菜单门控静默失效。
                if (kvp.Key is "role" or "roles" || kvp.Key == ClaimTypes.Role)
                {
                    if (kvp.Value is JsonElement { ValueKind: JsonValueKind.Array } arr)
                    {
                        return arr.EnumerateArray()
                                  .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString())
                                  .Where(s => !string.IsNullOrEmpty(s))
                                  .Select(s => new Claim(ClaimTypes.Role, s!));
                    }
                    return new[] { new Claim(ClaimTypes.Role, kvp.Value?.ToString() ?? string.Empty) };
                }
                return new[] { new Claim(kvp.Key, kvp.Value?.ToString() ?? string.Empty) };
            })
                   ?? new List<Claim>();
        }
        catch
        {
            return new List<Claim>();
        }
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        // JWT 使用 base64url 编码（- 和 _ 代替 + 和 /），Convert.FromBase64String 只接受标准 base64。
        // 必须先转回标准字符，否则 payload 含 -/_ 时（角色数组、UUID 等长 payload 几乎必然命中）
        // 会抛 FormatException，被上层 catch 吞掉后返回空 claims，导致角色丢失、菜单门控全灭。
        base64 = base64.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}