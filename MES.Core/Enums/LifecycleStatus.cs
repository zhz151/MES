namespace MES.Core.Enums;

/// <summary>
/// 设备生命周期状态
/// </summary>
public enum LifecycleStatus
{
    /// <summary>
    /// 在用
    /// </summary>
    Active = 0,

    /// <summary>
    /// 备用
    /// </summary>
    Standby = 1,

    /// <summary>
    /// 报废
    /// </summary>
    Scrapped = 2
}
