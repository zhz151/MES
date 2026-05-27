// 文件路径: MES.Data/Entities/StandardProcessCycle.cs
using System.ComponentModel.DataAnnotations;

namespace MES.Data.Entities;

/// <summary>
/// 标准工艺生产周期：定义每种规格产品的标准工序天数
/// </summary>
public class StandardProcessCycle : BaseEntity
{
    /// <summary>工厂牌号</summary>
    [Required]
    [MaxLength(50)]
    public string PlantGrade { get; set; } = string.Empty;

    /// <summary>原料类型（荒管/余库料）</summary>
    [Required]
    [MaxLength(50)]
    public string RawMaterialType { get; set; } = string.Empty;

    /// <summary>原料规格</summary>
    [Required]
    [MaxLength(100)]
    public string RawSpec { get; set; } = string.Empty;

    /// <summary>成品规格</summary>
    [Required]
    [MaxLength(100)]
    public string ProductSpec { get; set; } = string.Empty;

    /// <summary>交货状态</summary>
    [Required]
    [MaxLength(50)]
    public string DeliveryState { get; set; } = string.Empty;

    /// <summary>标准工序生产周期（天）</summary>
    public int StandardCycleDays { get; set; }
}
