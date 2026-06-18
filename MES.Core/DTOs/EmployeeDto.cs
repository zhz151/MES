namespace MES.Core.DTOs;

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
    public bool IsActive { get; set; } = true;
}
