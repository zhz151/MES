namespace MES.Core.Interfaces.Infrastructure;

/// <summary>
/// 当前用户信息抽象（跨层获取当前登录用户，避免 Data/Service 层直接依赖 HTTP 上下文）
/// </summary>
public interface ICurrentUser
{
    string GetUserName();
}
