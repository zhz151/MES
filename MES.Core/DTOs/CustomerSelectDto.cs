namespace MES.Core.DTOs;

/// <summary>
/// 客户下拉选择轻量 DTO（仅含级联下拉所需字段）
/// </summary>
public class CustomerSelectDto
{
    public int Id { get; set; }
    public string CustomerUnit { get; set; } = string.Empty;
    public string Salesman { get; set; } = string.Empty;
    public string? EndCustomer { get; set; }
}
