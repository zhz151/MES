namespace MES.Core.DTOs;

/// <summary>
/// 仓库档案 DTO
/// </summary>
public class WarehouseDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public string? Remark { get; set; }
}
