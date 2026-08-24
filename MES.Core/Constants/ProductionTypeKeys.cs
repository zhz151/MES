namespace MES.Core.Constants;

/// <summary>
/// 生产类型常量（对应 ProductionBatch.ProductionType 字段的合法值，DB 存储枚举名）
/// </summary>
public static class ProductionTypeKeys
{
    // DB 存储的是枚举名，常量值与枚举名一致
    public const string RoughTube = "RoughTube";
    public const string InProcess = "InProcess";
    public const string Inventory = "Inventory";
    public const string OutsourcedPurchased = "OutsourcedPurchased";
    public const string Rework = "Rework";
    public const string Subcontract = "Subcontract";
    public const string ExternalProcessing = "ExternalProcessing";

    /// <summary>全部生产类型（DB 字符串比较）</summary>
    public static readonly string[] All = { RoughTube, InProcess, Inventory, OutsourcedPurchased, Rework, Subcontract, ExternalProcessing };
}
