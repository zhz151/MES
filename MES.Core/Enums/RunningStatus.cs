namespace MES.Core.Enums;

/// <summary>
/// 设备运行状态（由维修记录自动驱动）
/// </summary>
public enum RunningStatus
{
    /// <summary>
    /// 正常
    /// </summary>
    Normal = 0,

    /// <summary>
    /// 待维修（已报修，无开始时间）
    /// </summary>
    Pending = 1,

    /// <summary>
    /// 维修中（有开始时间，无完成时间）
    /// </summary>
    InProgress = 2
}
