namespace MES.Core.Enums;

/// <summary>
/// 生产类型
/// </summary>
public enum ProductionType
{
    /// <summary>荒管生产</summary>
    RoughTube,
    /// <summary>在制生产</summary>
    InProcess,
    /// <summary>库存</summary>
    Inventory,
    /// <summary>外购</summary>
    OutsourcedPurchased,
    /// <summary>返整</summary>
    Rework,
    /// <summary>委外生产</summary>
    Subcontract,
    /// <summary>对外加工</summary>
    ExternalProcessing
}
