namespace MES.Data.Entities.Auth;

/// <summary>
/// 刷新令牌实体
/// </summary>
public class RefreshToken : BaseEntity
{
    /// <summary>
    /// 刷新令牌值（64字节随机令牌，Base64编码）
    /// </summary>
    public string Token { get; set; } = null!;

    /// <summary>
    /// 所属用户ID
    /// </summary>
    public string UserId { get; set; } = null!;

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime Expires { get; set; }

    /// <summary>
    /// 是否已撤销
    /// </summary>
    public bool IsRevoked { get; set; }
}
