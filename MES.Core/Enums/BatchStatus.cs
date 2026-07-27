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
    Completed = 2,

    /// <summary>
    /// 成检（已完成生产，处于成品检验阶段）
    /// </summary>
    InFinalInspection = 5,

    /// <summary>
    /// 暂停（人工暂停）
    /// </summary>
    Suspended = 3
}
