namespace MES.Core.Enums;

/// <summary>
/// NCR 不合格品报告状态
/// </summary>
public enum NcrStatus
{
    /// <summary>待处理</summary>
    Pending,
    /// <summary>处理中</summary>
    Processing,
    /// <summary>已关闭</summary>
    Closed,
    /// <summary>忽略（该待处理组无需完整不合格报告，登记即忽略）</summary>
    Ignored
}
