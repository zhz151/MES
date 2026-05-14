using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs;

// ========== 牌号验证 ==========

public class ChemicalValidationRuleDto
{
    public int Id { get; set; }
    public string PlantGrade { get; set; } = null!;

    public string? CMin { get; set; }
    public string? CMax { get; set; }
    public string? SiMin { get; set; }
    public string? SiMax { get; set; }
    public string? MnMin { get; set; }
    public string? MnMax { get; set; }
    public string? PMin { get; set; }
    public string? PMax { get; set; }
    public string? SMin { get; set; }
    public string? SMax { get; set; }
    public string? NiMin { get; set; }
    public string? NiMax { get; set; }
    public string? CrMin { get; set; }
    public string? CrMax { get; set; }
    public string? MoMin { get; set; }
    public string? MoMax { get; set; }
    public string? CuMin { get; set; }
    public string? CuMax { get; set; }
    public string? NMin { get; set; }
    public string? NMax { get; set; }
    public string? NbMin { get; set; }
    public string? NbMax { get; set; }
    public string? TiMin { get; set; }
    public string? TiMax { get; set; }
    public string? FeMin { get; set; }
    public string? FeMax { get; set; }
    public string? AlMin { get; set; }
    public string? AlMax { get; set; }
    public string? WMin { get; set; }
    public string? WMax { get; set; }
    public string? PRENMin { get; set; }

    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
}

public class CreateChemicalValidationRuleRequest
{
    [Required(ErrorMessage = "工厂牌号不能为空")]
    [MaxLength(100)]
    public string PlantGrade { get; set; } = null!;

    [MaxLength(50)]
    public string? CMin { get; set; }
    [MaxLength(50)]
    public string? CMax { get; set; }
    [MaxLength(50)]
    public string? SiMin { get; set; }
    [MaxLength(50)]
    public string? SiMax { get; set; }
    [MaxLength(50)]
    public string? MnMin { get; set; }
    [MaxLength(50)]
    public string? MnMax { get; set; }
    [MaxLength(50)]
    public string? PMin { get; set; }
    [MaxLength(50)]
    public string? PMax { get; set; }
    [MaxLength(50)]
    public string? SMin { get; set; }
    [MaxLength(50)]
    public string? SMax { get; set; }
    [MaxLength(50)]
    public string? NiMin { get; set; }
    [MaxLength(50)]
    public string? NiMax { get; set; }
    [MaxLength(50)]
    public string? CrMin { get; set; }
    [MaxLength(50)]
    public string? CrMax { get; set; }
    [MaxLength(50)]
    public string? MoMin { get; set; }
    [MaxLength(50)]
    public string? MoMax { get; set; }
    [MaxLength(50)]
    public string? CuMin { get; set; }
    [MaxLength(50)]
    public string? CuMax { get; set; }
    [MaxLength(50)]
    public string? NMin { get; set; }
    [MaxLength(50)]
    public string? NMax { get; set; }
    [MaxLength(50)]
    public string? NbMin { get; set; }
    [MaxLength(50)]
    public string? NbMax { get; set; }
    [MaxLength(50)]
    public string? TiMin { get; set; }
    [MaxLength(50)]
    public string? TiMax { get; set; }
    [MaxLength(50)]
    public string? FeMin { get; set; }
    [MaxLength(50)]
    public string? FeMax { get; set; }
    [MaxLength(50)]
    public string? AlMin { get; set; }
    [MaxLength(50)]
    public string? AlMax { get; set; }
    [MaxLength(50)]
    public string? WMin { get; set; }
    [MaxLength(50)]
    public string? WMax { get; set; }
    [MaxLength(50)]
    public string? PRENMin { get; set; }
}

public class UpdateChemicalValidationRuleRequest
{
    [MaxLength(100)]
    public string PlantGrade { get; set; } = null!;

    [MaxLength(50)]
    public string? CMin { get; set; }
    [MaxLength(50)]
    public string? CMax { get; set; }
    [MaxLength(50)]
    public string? SiMin { get; set; }
    [MaxLength(50)]
    public string? SiMax { get; set; }
    [MaxLength(50)]
    public string? MnMin { get; set; }
    [MaxLength(50)]
    public string? MnMax { get; set; }
    [MaxLength(50)]
    public string? PMin { get; set; }
    [MaxLength(50)]
    public string? PMax { get; set; }
    [MaxLength(50)]
    public string? SMin { get; set; }
    [MaxLength(50)]
    public string? SMax { get; set; }
    [MaxLength(50)]
    public string? NiMin { get; set; }
    [MaxLength(50)]
    public string? NiMax { get; set; }
    [MaxLength(50)]
    public string? CrMin { get; set; }
    [MaxLength(50)]
    public string? CrMax { get; set; }
    [MaxLength(50)]
    public string? MoMin { get; set; }
    [MaxLength(50)]
    public string? MoMax { get; set; }
    [MaxLength(50)]
    public string? CuMin { get; set; }
    [MaxLength(50)]
    public string? CuMax { get; set; }
    [MaxLength(50)]
    public string? NMin { get; set; }
    [MaxLength(50)]
    public string? NMax { get; set; }
    [MaxLength(50)]
    public string? NbMin { get; set; }
    [MaxLength(50)]
    public string? NbMax { get; set; }
    [MaxLength(50)]
    public string? TiMin { get; set; }
    [MaxLength(50)]
    public string? TiMax { get; set; }
    [MaxLength(50)]
    public string? FeMin { get; set; }
    [MaxLength(50)]
    public string? FeMax { get; set; }
    [MaxLength(50)]
    public string? AlMin { get; set; }
    [MaxLength(50)]
    public string? AlMax { get; set; }
    [MaxLength(50)]
    public string? WMin { get; set; }
    [MaxLength(50)]
    public string? WMax { get; set; }
    [MaxLength(50)]
    public string? PRENMin { get; set; }
}
