namespace MES.Core.DTOs;

/// <summary>
/// 业务参数配置 DTO
/// </summary>
public class ConfigParameterDto
{
    public int Id { get; set; }
    public string Category { get; set; } = null!;
    public string ParamKey { get; set; } = null!;
    public decimal ParamValue { get; set; }
    public string? Remark { get; set; }
}
