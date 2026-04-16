namespace MES.Core.Enums;

/// <summary>
/// 交货状态枚举
/// </summary>
public enum DeliveryState
{
    /// <summary>
    /// 固溶酸洗
    /// </summary>
    SolutionAnnealedAndPickled,
    
    /// <summary>
    /// 固溶酸洗-U型管
    /// </summary>
    SolutionAnnealedAndPickledUTube,
    
    /// <summary>
    /// 固溶酸洗-外抛光
    /// </summary>
    SolutionAnnealedAndPickledExternalPolished,
    
    /// <summary>
    /// 固溶酸洗-内抛光
    /// </summary>
    SolutionAnnealedAndPickledInternalPolished,
    
    /// <summary>
    /// 固溶酸洗-内外抛光
    /// </summary>
    SolutionAnnealedAndPickledBothPolished,
    
    /// <summary>
    /// 固溶酸洗-盘管
    /// </summary>
    SolutionAnnealedAndPickledCoiled,
    
    /// <summary>
    /// 光亮
    /// </summary>
    Bright,
    
    /// <summary>
    /// 光亮-U型管
    /// </summary>
    BrightUTube,
    
    /// <summary>
    /// 光亮-盘管
    /// </summary>
    BrightCoiled,
    
    /// <summary>
    /// 硬态
    /// </summary>
    Hard
}