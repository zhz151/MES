// 文件路径: MES.Core/DTOs/UpdateStandardProcessCycleRequest.cs
using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs;

public class UpdateStandardProcessCycleRequest
{
    [StringLength(50)]
    public string? PlantGrade { get; set; }

    [StringLength(50)]
    public string? RawMaterialType { get; set; }

    [StringLength(100)]
    public string? RawSpec { get; set; }

    [StringLength(100)]
    public string? ProductSpec { get; set; }

    [StringLength(50)]
    public string? DeliveryState { get; set; }

    [Range(1, 365)]
    public int? StandardCycleDays { get; set; }
}
