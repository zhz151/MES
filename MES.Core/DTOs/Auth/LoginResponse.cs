namespace MES.Core.DTOs.Auth;

public class LoginResponse
{

    public string Token { get; set; } = string.Empty;


    public string Email { get; set; } = string.Empty;


    public string UserName { get; set; } = string.Empty;


    public List<string> Roles { get; set; } = new();


    public DateTime Expires { get; set; }

    public string FullName { get; set; } = string.Empty;
}