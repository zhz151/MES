namespace MES.Core.DTOs.Auth;

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public bool IsActive { get; set; }
    public List<string> Roles { get; set; } = new();
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public List<string> Roles { get; set; } = new();
    public string? UserName { get; set; }
}

public class UpdateUserRequest
{
    public string? FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> Roles { get; set; } = new();
}

public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}
