using Microsoft.AspNetCore.Identity;

namespace MES.Core.Models;

/// <summary>
/// 应用程序用户模型
/// </summary>
public class AppUser : IdentityUser
{
    /// <summary>
    /// 用户全名
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// 部门
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// 工号
    /// </summary>
    public string? EmployeeId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后登录时间
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// 是否激活
    /// </summary>
    public bool IsActive { get; set; } = true;
}