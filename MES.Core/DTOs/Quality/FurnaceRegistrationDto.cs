using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs.Quality;

// ========== 来料炉号登记 ==========

public class FurnaceRegistrationDto
{
    public int Id { get; set; }

    /// <summary>来料日期</summary>
    public DateTime IncomingDate { get; set; }

    /// <summary>原料单位</summary>
    public string RawMaterialUnit { get; set; } = null!;

    /// <summary>原料类型</summary>
    public string RawMaterialType { get; set; } = null!;

    /// <summary>登记牌号</summary>
    public string RegisteredGrade { get; set; } = null!;

    /// <summary>关联工厂牌号</summary>
    public string? RelatedPlantGrade { get; set; }

    /// <summary>炉号</summary>
    public string FurnaceNumber { get; set; } = null!;

    /// <summary>规格</summary>
    public string? Specification { get; set; }

    /// <summary>支数</summary>
    public int? Quantity { get; set; }

    /// <summary>重量</summary>
    public decimal? Weight { get; set; }

    public decimal? Carbon { get; set; }
    public decimal? Silicon { get; set; }
    public decimal? Manganese { get; set; }
    public decimal? Phosphorus { get; set; }
    public decimal? Sulfur { get; set; }
    public decimal? Nickel { get; set; }
    public decimal? Chromium { get; set; }
    public decimal? Molybdenum { get; set; }
    public decimal? Copper { get; set; }
    public decimal? Nitrogen { get; set; }
    public decimal? Niobium { get; set; }
    public decimal? Titanium { get; set; }
    public decimal? Iron { get; set; }
    public decimal? Aluminum { get; set; }
    public decimal? Tungsten { get; set; }
    public decimal? PREN { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
}

/// <summary>
/// 创建来料炉号登记请求
/// </summary>
public class CreateFurnaceRegistrationRequest
{
    [Required(ErrorMessage = "来料日期不能为空")]
    public DateTime IncomingDate { get; set; }

    [Required(ErrorMessage = "原料单位不能为空")]
    [MaxLength(100)]
    public string RawMaterialUnit { get; set; } = null!;

    [Required(ErrorMessage = "原料类型不能为空")]
    [MaxLength(50)]
    public string RawMaterialType { get; set; } = null!;

    [Required(ErrorMessage = "登记牌号不能为空")]
    [MaxLength(100)]
    public string RegisteredGrade { get; set; } = null!;

    [MaxLength(100)]
    public string? RelatedPlantGrade { get; set; }

    [Required(ErrorMessage = "炉号不能为空")]
    [MaxLength(100)]
    public string FurnaceNumber { get; set; } = null!;

    [MaxLength(100)]
    public string? Specification { get; set; }

    public int? Quantity { get; set; }

    public decimal? Weight { get; set; }

    public decimal? Carbon { get; set; }
    public decimal? Silicon { get; set; }
    public decimal? Manganese { get; set; }
    public decimal? Phosphorus { get; set; }
    public decimal? Sulfur { get; set; }
    public decimal? Nickel { get; set; }
    public decimal? Chromium { get; set; }
    public decimal? Molybdenum { get; set; }
    public decimal? Copper { get; set; }
    public decimal? Nitrogen { get; set; }
    public decimal? Niobium { get; set; }
    public decimal? Titanium { get; set; }
    public decimal? Iron { get; set; }
    public decimal? Aluminum { get; set; }
    public decimal? Tungsten { get; set; }
    public decimal? PREN { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }
}

/// <summary>
/// 更新来料炉号登记请求
/// </summary>
public class UpdateFurnaceRegistrationRequest
{
    public DateTime IncomingDate { get; set; }

    [MaxLength(100)]
    public string RawMaterialUnit { get; set; } = null!;

    [MaxLength(50)]
    public string RawMaterialType { get; set; } = null!;

    [MaxLength(100)]
    public string RegisteredGrade { get; set; } = null!;

    [MaxLength(100)]
    public string? RelatedPlantGrade { get; set; }

    [MaxLength(100)]
    public string FurnaceNumber { get; set; } = null!;

    [MaxLength(100)]
    public string? Specification { get; set; }

    public int? Quantity { get; set; }
    public decimal? Weight { get; set; }

    public decimal? Carbon { get; set; }
    public decimal? Silicon { get; set; }
    public decimal? Manganese { get; set; }
    public decimal? Phosphorus { get; set; }
    public decimal? Sulfur { get; set; }
    public decimal? Nickel { get; set; }
    public decimal? Chromium { get; set; }
    public decimal? Molybdenum { get; set; }
    public decimal? Copper { get; set; }
    public decimal? Nitrogen { get; set; }
    public decimal? Niobium { get; set; }
    public decimal? Titanium { get; set; }
    public decimal? Iron { get; set; }
    public decimal? Aluminum { get; set; }
    public decimal? Tungsten { get; set; }
    public decimal? PREN { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }
}
