using System.ComponentModel.DataAnnotations;

namespace MES.Data.Entities.Configuration;

/// <summary>
/// 业务参数配置表
/// 存储所有可配置的业务数值常量（阈值、倍率、天数、默认值等）
/// 替代硬编码的业务数值常量，通过 Category 分组 + ParamKey 标识
/// </summary>
public class ConfigParameter : BaseEntity
{
    /// <summary>
    /// 参数分类（英文代码，如 "WarehouseThreshold"）
    /// </summary>
    [Required(ErrorMessage = "参数分类不能为空")]
    [MaxLength(50)]
    public string Category { get; set; } = null!;

    /// <summary>
    /// 分类及用途（中文解读，由用户填写，如"仓库-完工阈值"）
    /// </summary>
    [MaxLength(100)]
    public string? CategoryDisplay { get; set; }

    /// <summary>
    /// 所属上下文（如"采购+仓库"、"工单"）
    /// </summary>
    [MaxLength(50)]
    public string? Context { get; set; }

    /// <summary>
    /// 参数键（同一分类下唯一，如 "CompleteRatio"）
    /// </summary>
    [Required(ErrorMessage = "参数键不能为空")]
    [MaxLength(100)]
    public string ParamKey { get; set; } = null!;

    /// <summary>
    /// 参数值（数值型）
    /// </summary>
    public decimal ParamValue { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(200)]
    public string? Remark { get; set; }
}
