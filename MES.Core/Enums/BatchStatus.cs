namespace MES.Core.Enums;

/// <summary>
/// 批次状态
/// </summary>
public enum BatchStatus
{
    /// <summary>
    /// 未产
    /// </summary>
    None = 0,

    /// <summary>
    /// 在产
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// 完成
    /// </summary>
    Completed = 2
}
