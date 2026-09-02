namespace MES.Core.Enums;

/// <summary>
/// 工资结算模式（员工配置固定化）。计件工资结算分派键：
/// 个人计件/集体计件/靠工计件 走计件，计小时/计日期 走计时，固定月薪 走固定。
/// 显示中文经 EnumDisplayDefinition 配置表优先 → EnumHelper 静态字典兜底。
/// </summary>
public enum SalaryMode
{
    /// <summary>个人计件</summary>
    PieceIndividual,

    /// <summary>集体计件（先按岗位结算岗位工资 → 岗位内按出勤+月度评分分配）</summary>
    PieceCollective,

    /// <summary>靠工计件（相关计件工段平均时薪 × 个人出勤 × 员工级固定系数）</summary>
    PieceAttendance,

    /// <summary>计小时</summary>
    Hourly,

    /// <summary>计日期</summary>
    Daily,

    /// <summary>固定月薪</summary>
    Fixed
}
