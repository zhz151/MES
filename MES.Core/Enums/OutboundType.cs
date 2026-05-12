namespace MES.Core.Enums;

/// <summary>
/// 出库类型枚举
/// </summary>
public enum OutboundType
{
    /// <summary>
    /// 生产领料
    /// </summary>
    ProductionPick,

    /// <summary>
    /// 销售出库
    /// </summary>
    SalesOut,

    /// <summary>
    /// 退货出库
    /// </summary>
    ReturnOut,

    /// <summary>
    /// 委外加工
    /// </summary>
    SubcontractOut,

    /// <summary>
    /// 报废出库
    /// </summary>
    ScrapOut,

    /// <summary>
    /// 检验领用
    /// </summary>
    InspectionPick,

    /// <summary>
    /// 移库出库
    /// </summary>
    TransferOut,

    /// <summary>
    /// 其他出库
    /// </summary>
    OtherOut
}
