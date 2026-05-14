using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs;

// ========== 牌号化学成分 ==========

public class ChemicalCompositionDto
{
    public int Id { get; set; }

    /// <summary>工厂牌号</summary>
    public string PlantGrade { get; set; } = null!;

    public string? Carbon { get; set; }
    public string? Silicon { get; set; }
    public string? Manganese { get; set; }
    public string? Phosphorus { get; set; }
    public string? Sulfur { get; set; }
    public string? Nickel { get; set; }
    public string? Chromium { get; set; }
    public string? Molybdenum { get; set; }
    public string? Copper { get; set; }
    public string? Nitrogen { get; set; }
    public string? Niobium { get; set; }
    public string? Titanium { get; set; }
    public string? Iron { get; set; }
    public string? Aluminum { get; set; }
    public string? Tungsten { get; set; }
    public string? PREN { get; set; }

    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
}

/// <summary>
/// 创建牌号化学成分请求
/// </summary>
public class CreateChemicalCompositionRequest
{
    [Required(ErrorMessage = "工厂牌号不能为空")]
    [MaxLength(50)]
    public string PlantGrade { get; set; } = null!;

    [MaxLength(100)]
    public string? Carbon { get; set; }

    [MaxLength(100)]
    public string? Silicon { get; set; }

    [MaxLength(100)]
    public string? Manganese { get; set; }

    [MaxLength(100)]
    public string? Phosphorus { get; set; }

    [MaxLength(100)]
    public string? Sulfur { get; set; }

    [MaxLength(100)]
    public string? Nickel { get; set; }

    [MaxLength(100)]
    public string? Chromium { get; set; }

    [MaxLength(100)]
    public string? Molybdenum { get; set; }

    [MaxLength(100)]
    public string? Copper { get; set; }

    [MaxLength(100)]
    public string? Nitrogen { get; set; }

    [MaxLength(100)]
    public string? Niobium { get; set; }

    [MaxLength(100)]
    public string? Titanium { get; set; }

    [MaxLength(100)]
    public string? Iron { get; set; }

    [MaxLength(100)]
    public string? Aluminum { get; set; }

    [MaxLength(100)]
    public string? Tungsten { get; set; }

    [MaxLength(100)]
    public string? PREN { get; set; }
}

/// <summary>
/// 更新牌号化学成分请求
/// </summary>
public class UpdateChemicalCompositionRequest
{
    [MaxLength(50)]
    public string PlantGrade { get; set; } = null!;

    [MaxLength(100)]
    public string? Carbon { get; set; }

    [MaxLength(100)]
    public string? Silicon { get; set; }

    [MaxLength(100)]
    public string? Manganese { get; set; }

    [MaxLength(100)]
    public string? Phosphorus { get; set; }

    [MaxLength(100)]
    public string? Sulfur { get; set; }

    [MaxLength(100)]
    public string? Nickel { get; set; }

    [MaxLength(100)]
    public string? Chromium { get; set; }

    [MaxLength(100)]
    public string? Molybdenum { get; set; }

    [MaxLength(100)]
    public string? Copper { get; set; }

    [MaxLength(100)]
    public string? Nitrogen { get; set; }

    [MaxLength(100)]
    public string? Niobium { get; set; }

    [MaxLength(100)]
    public string? Titanium { get; set; }

    [MaxLength(100)]
    public string? Iron { get; set; }

    [MaxLength(100)]
    public string? Aluminum { get; set; }

    [MaxLength(100)]
    public string? Tungsten { get; set; }

    [MaxLength(100)]
    public string? PREN { get; set; }
}
