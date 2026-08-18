namespace MES.Core.Enums;

/// <summary>
/// 入库来源
/// </summary>
public enum InboundSource
{
    /// <summary>外购</summary>
    Purchase,
    /// <summary>委外</summary>
    Subcontract,
    /// <summary>生产入库</summary>
    ProductionInbound,
    /// <summary>检验入库</summary>
    InspectionInbound,
    /// <summary>其它</summary>
    Other
}
