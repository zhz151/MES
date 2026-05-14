namespace MES.Core.Enums;

/// <summary>
/// 成品检验项目枚举
/// </summary>
public enum InspectionItem
{
    /// <summary>PMI检验</summary>
    PMIInspection,
    /// <summary>表检</summary>
    VisualInspection,
    /// <summary>尺寸</summary>
    Dimension,
    /// <summary>内窥</summary>
    Endoscopy,
    /// <summary>水压</summary>
    HydrostaticPressure,
    /// <summary>水下气压</summary>
    UnderwaterPneumatic,
    /// <summary>涡流</summary>
    EddyCurrent,
    /// <summary>超声波</summary>
    Ultrasonic,
    /// <summary>端口着色</summary>
    PortColoring
}
