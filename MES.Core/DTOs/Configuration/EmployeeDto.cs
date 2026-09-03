using MES.Core.Enums;

namespace MES.Core.DTOs.Configuration;

/// <summary>
/// 员工信息
/// </summary>
public class EmployeeDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Department { get; set; }
    public string? Position { get; set; }
    public string? PositionRemark { get; set; }
    public SalaryMode? SalaryMode { get; set; }
    public string? SalaryRemark { get; set; }
    /// <summary>靠工岗位（岗位英文 Key 逗号串；仅靠工计件模式使用）</summary>
    public string? AttendancePositions { get; set; }
    /// <summary>靠工系数（仅靠工计件模式使用，默认 1.0）</summary>
    public decimal? AttendanceCoefficient { get; set; } = 1.0m;
    /// <summary>小时工资（仅计小时模式使用）</summary>
    public decimal? HourlyWage { get; set; }
    /// <summary>日工资（仅计日期模式使用）</summary>
    public decimal? DailyWage { get; set; }
    /// <summary>月工资（仅固定月薪模式使用）</summary>
    public decimal? MonthlyWage { get; set; }
    /// <summary>工段名（英文 Key），扫码报工按工段过滤操作人</summary>
    public string? SectionName { get; set; }
    /// <summary>工序组（工序英文 Key 逗号串可多工序），操作人候选按「工段 ∩ 工序组」过滤；空=全工序组通配</summary>
    public string? GroupName { get; set; }
    /// <summary>成检项目资质（InspectionItem 枚举名逗号串），成品检验扫码按工位项目过滤</summary>
    public string? InspectionItems { get; set; }
    /// <summary>是否属于成检到料确认人（勾选=true），成检到料扫码确认人候选</summary>
    public bool? MaterialReceiveCheckItems { get; set; }
    public bool IsActive { get; set; } = true;
}
