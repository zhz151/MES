namespace MES.Core.Enums;

/// <summary>
/// 钢管类别
/// </summary>
public enum PipeCategory
{
    /// <summary>荒管</summary>
    TubeBlank,
    /// <summary>在制品</summary>
    WorkInProgress,
    /// <summary>余库料</summary>
    SurplusInventory,
    /// <summary>临界成品</summary>
    CriticalFinished,
    /// <summary>订单成品</summary>
    OrderFinished,
    /// <summary>备料成品</summary>
    PreparedFinished,
    /// <summary>特定交态成品</summary>
    SpecialDelivery
}
