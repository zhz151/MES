namespace MES.Blazor.Models;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("expiration")]
    public DateTime Expiration { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = new();
}

public class UserInfo
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public bool IsAuthenticated { get; set; }
}
