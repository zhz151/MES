namespace MES.Data.Entities;

/// <summary>
/// 牌号验证 — 工厂牌号的各化学元素成分验证规则（含上下限和公式）
/// </summary>
public class ChemicalValidationRule : BaseEntity
{
    /// <summary>工厂牌号</summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>C最小值（可为数值或公式，如"10×C%"）</summary>
    public string? CMin { get; set; }
    /// <summary>C最大值</summary>
    public string? CMax { get; set; }
    /// <summary>Si最小值</summary>
    public string? SiMin { get; set; }
    /// <summary>Si最大值</summary>
    public string? SiMax { get; set; }
    /// <summary>Mn最小值</summary>
    public string? MnMin { get; set; }
    /// <summary>Mn最大值</summary>
    public string? MnMax { get; set; }
    /// <summary>P最小值</summary>
    public string? PMin { get; set; }
    /// <summary>P最大值</summary>
    public string? PMax { get; set; }
    /// <summary>S最小值</summary>
    public string? SMin { get; set; }
    /// <summary>S最大值</summary>
    public string? SMax { get; set; }
    /// <summary>Ni最小值</summary>
    public string? NiMin { get; set; }
    /// <summary>Ni最大值</summary>
    public string? NiMax { get; set; }
    /// <summary>Cr最小值</summary>
    public string? CrMin { get; set; }
    /// <summary>Cr最大值</summary>
    public string? CrMax { get; set; }
    /// <summary>Mo最小值</summary>
    public string? MoMin { get; set; }
    /// <summary>Mo最大值</summary>
    public string? MoMax { get; set; }
    /// <summary>Cu最小值</summary>
    public string? CuMin { get; set; }
    /// <summary>Cu最大值</summary>
    public string? CuMax { get; set; }
    /// <summary>N最小值</summary>
    public string? NMin { get; set; }
    /// <summary>N最大值</summary>
    public string? NMax { get; set; }
    /// <summary>Nb最小值（可为公式，如"10×C%"）</summary>
    public string? NbMin { get; set; }
    /// <summary>Nb最大值</summary>
    public string? NbMax { get; set; }
    /// <summary>Ti最小值（可为公式，如"5×C%"）</summary>
    public string? TiMin { get; set; }
    /// <summary>Ti最大值</summary>
    public string? TiMax { get; set; }
    /// <summary>Fe最小值</summary>
    public string? FeMin { get; set; }
    /// <summary>Fe最大值</summary>
    public string? FeMax { get; set; }
    /// <summary>Al最小值</summary>
    public string? AlMin { get; set; }
    /// <summary>Al最大值</summary>
    public string? AlMax { get; set; }
    /// <summary>W最小值</summary>
    public string? WMin { get; set; }
    /// <summary>W最大值</summary>
    public string? WMax { get; set; }
    /// <summary>PREN腐蚀当量最小值</summary>
    public string? PRENMin { get; set; }
}
