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

    /// <summary>工资结算模式</summary>
    public string? SalaryMode { get; set; }

    /// <summary>工资结算备注</summary>
    public string? SalaryRemark { get; set; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; set; } = true;
}
