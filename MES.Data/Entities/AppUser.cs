using Microsoft.AspNetCore.Identity;

namespace MES.Data.Entities;

public class AppUser : IdentityUser
{
    public string? FullName { get; set; }
}
