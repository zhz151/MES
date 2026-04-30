namespace MES.Core.Enums;

/// <summary>
/// 库料生产改制类型
/// </summary>
public enum ReworkType
{
    /// <summary>
    /// 空拉改制
    /// </summary>
    EmptyDrawing = 0,

    /// <summary>
    /// 少道次改制
    /// </summary>
    FewerPass = 1,

    /// <summary>
    /// 人工选择改制
    /// </summary>
    ManualSelect = 2
}
