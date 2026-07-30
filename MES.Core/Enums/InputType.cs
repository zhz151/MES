namespace MES.Core.Enums;

/// <summary>
/// 批次投料类型
/// </summary>
public enum BatchInputType
{
    /// <summary>
    /// 仓库投料
    /// </summary>
    Warehouse = 0,

    /// <summary>
    /// 编号拆分
    /// </summary>
    SplitFromNumber = 1,

    /// <summary>
    /// 其它
    /// </summary>
    Other = 2
}
