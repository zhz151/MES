namespace MES.Core.Enums;

/// <summary>
/// 工段委外状态
/// </summary>
public enum SectionOutsourceStatus
{
    /// <summary>
    /// 待回收
    /// </summary>
    PendingRecovery = 0,

    /// <summary>
    /// 已回收
    /// </summary>
    Recovered = 1,

    /// <summary>
    /// 在轧（旧系统遗留状态）
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// 略（厂内虚拟发外状态，无需回收）
    /// </summary>
    Virtual = 3
}
