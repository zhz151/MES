using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs.Warehouse;

public class CreateWarehouseRequest
{
    [Required(ErrorMessage = "仓库代码不能为空")]
    [StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "仓库名称不能为空")]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    [StringLength(500)]
    public string? Remark { get; set; }
}

public class UpdateWarehouseRequest
{
    [StringLength(20)]
    public string? Code { get; set; }

    [StringLength(50)]
    public string? Name { get; set; }

    public int? SortOrder { get; set; }
    public bool? IsActive { get; set; }

    [StringLength(500)]
    public string? Remark { get; set; }
}
