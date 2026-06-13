namespace MES.Core.Enums;

/// <summary>
/// 去油/酸洗入缸状态
/// </summary>
public enum PicklingStatus
{
    /// <summary>
    /// 浸泡中（已入缸，尚未出缸）
    /// </summary>
    Soaking = 0,

    /// <summary>
    /// 已完工（出缸+冲洗完成）
    /// </summary>
    Completed = 1
}
