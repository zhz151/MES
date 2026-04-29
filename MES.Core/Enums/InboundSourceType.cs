namespace MES.Core.Enums;

/// <summary>
/// 入库来源枚举
/// </summary>
public enum InboundSourceType
{
    /// <summary>
    /// 外购
    /// </summary>
    Purchase,

    /// <summary>
    /// 委外穿孔
    /// </summary>
    SubcontractPiercing,

    /// <summary>
    /// 自产
    /// </summary>
    SelfProduced,

    /// <summary>
    /// 检验入库
    /// </summary>
    InspectionInbound,

    /// <summary>
    /// 生产入库
    /// </summary>
    ProductionInbound,

    /// <summary>
    /// 移库入库
    /// </summary>
    TransferIn,

    /// <summary>
    /// 退货入库
    /// </summary>
    ReturnIn
}
