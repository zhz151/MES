using Microsoft.AspNetCore.Identity;

namespace MES.Data.Entities.Auth;

public class AppUser : IdentityUser
{
    public string? FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    /// <summary>用户备注：账号用途/岗位说明（因权限为直接分配、无岗位角色概念，用自由文本弥补可读性）</summary>
    public string? Remark { get; set; }
}
