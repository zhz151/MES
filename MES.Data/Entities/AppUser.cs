using Microsoft.AspNetCore.Identity;

namespace MES.Data.Entities;

public class AppUser : IdentityUser
{
    public string? FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
}
