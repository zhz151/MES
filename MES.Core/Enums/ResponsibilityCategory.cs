namespace MES.Core.Enums;

/// <summary>
/// NCR 责任类别
/// </summary>
public enum ResponsibilityCategory
{
    /// <summary>生产-厂内</summary>
    ProductionInternal,
    /// <summary>生产-外协</summary>
    ProductionOutsource,
    /// <summary>原料-荒管</summary>
    MaterialTubeBlank,
    /// <summary>原料-外购成品</summary>
    MaterialPurchased,
    /// <summary>原料-余库料</summary>
    MaterialSurplus
}
