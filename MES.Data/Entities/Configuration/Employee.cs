using MES.Core.Enums;

namespace MES.Data.Entities.Configuration;

/// <summary>
/// 员工 — 扫码报工和工资结算基础信息
/// </summary>
public class Employee : BaseEntity
{
    /// <summary>工号（二维码内容）</summary>
    public string Code { get; set; } = null!;

    /// <summary>姓名</summary>
    public string Name { get; set; } = null!;

    /// <summary>部门</summary>
    public string? Department { get; set; }

    /// <summary>岗位</summary>
    public string? Position { get; set; }

    /// <summary>岗位备注</summary>
    public string? PositionRemark { get; set; }

    /// <summary>工资结算模式（SalaryMode 枚举，存枚举名英文）</summary>
    public SalaryMode? SalaryMode { get; set; }

    /// <summary>工资结算备注</summary>
    public string? SalaryRemark { get; set; }

    /// <summary>靠工系数（仅靠工计件模式使用：靠工基准时薪 × 出勤 × 系数；默认 1.0）</summary>
    public decimal? AttendanceCoefficient { get; set; } = 1.0m;

    /// <summary>小时工资（仅计小时模式使用）</summary>
    public decimal? HourlyWage { get; set; }

    /// <summary>日工资（仅计日期模式使用）</summary>
    public decimal? DailyWage { get; set; }

    /// <summary>月工资（仅固定月薪模式使用）</summary>
    public decimal? MonthlyWage { get; set; }

    /// <summary>工段名（存工段英文 Key，如 ColdRollDraw）— 扫码报工按工段过滤操作人下拉</summary>
    public string? SectionName { get; set; }

    /// <summary>
    /// 工序组（存工序英文 Key 逗号串，如 "ColdRoll60,Cut"）— 操作人候选按「工段 ∩ 工序组」过滤；
    /// 空 = 全工序组通配；与批次工序组 ProcessGroup.ProcessName 同构匹配
    /// </summary>
    public string? GroupName { get; set; }

    /// <summary>
    /// 成检项目资质（存 InspectionItem 枚举名逗号串，如 "Ultrasonic,EddyCurrent"）
    /// — 成品检验工位扫码按工位绑定的检验项目过滤操作人（仅按项目，不看工段）
    /// </summary>
    public string? InspectionItems { get; set; }

    /// <summary>
    /// 是否属于成检到料确认人（勾选=true）— 成检到料工位扫码确认人候选 = 此开关为真的启用员工，与检验项目/工段无关
    /// </summary>
    public bool? MaterialReceiveCheckItems { get; set; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; set; } = true;
}
