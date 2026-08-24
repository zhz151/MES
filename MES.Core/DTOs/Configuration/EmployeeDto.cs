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
    public string? SalaryMode { get; set; }
    public string? SalaryRemark { get; set; }
    /// <summary>工段名（英文 Key），扫码报工按工段过滤操作人</summary>
    public string? SectionName { get; set; }
    /// <summary>组类（逗号串可多组），人多的工段扫码先选组类再选人</summary>
    public string? GroupName { get; set; }
    /// <summary>成检项目资质（InspectionItem 枚举名逗号串），成品检验扫码按工位项目过滤</summary>
    public string? InspectionItems { get; set; }
    /// <summary>是否属于过程检验操作人（勾选=true），过程检验扫码操作人候选</summary>
    public bool? ProcessInspectionItems { get; set; }
    /// <summary>是否属于成检到料确认人（勾选=true），成检到料扫码确认人候选</summary>
    public bool? MaterialReceiveCheckItems { get; set; }
    public bool IsActive { get; set; } = true;
}
