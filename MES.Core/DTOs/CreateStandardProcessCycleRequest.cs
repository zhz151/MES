// 文件路径: MES.Core/DTOs/CreateStandardProcessCycleRequest.cs
using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs;

public class CreateStandardProcessCycleRequest
{
    [Required(ErrorMessage = "工厂牌号不能为空")]
    [StringLength(50)]
    public string PlantGrade { get; set; } = string.Empty;

    [Required(ErrorMessage = "原料类型不能为空")]
    [StringLength(50)]
    public string RawMaterialType { get; set; } = string.Empty;

    [Required(ErrorMessage = "原料规格不能为空")]
    [StringLength(100)]
    public string RawSpec { get; set; } = string.Empty;

    [Required(ErrorMessage = "成品规格不能为空")]
    [StringLength(100)]
    public string ProductSpec { get; set; } = string.Empty;

    [Required(ErrorMessage = "交货状态不能为空")]
    [StringLength(50)]
    public string DeliveryState { get; set; } = string.Empty;

    [Required(ErrorMessage = "标准周期天数不能为空")]
    [Range(1, 365, ErrorMessage = "标准周期天数必须在1-365之间")]
    public int StandardCycleDays { get; set; }
}
