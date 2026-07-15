namespace MES.Data.Entities.ProductionStandard;

/// <summary>
/// 牌号化学成分 — 工厂牌号的各元素含量范围标准
/// </summary>
public class ChemicalComposition : BaseEntity
{
    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>碳(C)含量范围</summary>
    public string? Carbon { get; set; }

    /// <summary>硅(Si)含量范围</summary>
    public string? Silicon { get; set; }

    /// <summary>锰(Mn)含量范围</summary>
    public string? Manganese { get; set; }

    /// <summary>磷(P)含量范围</summary>
    public string? Phosphorus { get; set; }

    /// <summary>硫(S)含量范围</summary>
    public string? Sulfur { get; set; }

    /// <summary>镍(Ni)含量范围</summary>
    public string? Nickel { get; set; }

    /// <summary>铬(Cr)含量范围</summary>
    public string? Chromium { get; set; }

    /// <summary>钼(Mo)含量范围</summary>
    public string? Molybdenum { get; set; }

    /// <summary>铜(Cu)含量范围</summary>
    public string? Copper { get; set; }

    /// <summary>氮(N)含量范围</summary>
    public string? Nitrogen { get; set; }

    /// <summary>铌(Nb)含量范围</summary>
    public string? Niobium { get; set; }

    /// <summary>钛(Ti)含量范围</summary>
    public string? Titanium { get; set; }

    /// <summary>铁(Fe)含量范围</summary>
    public string? Iron { get; set; }

    /// <summary>铝(Al)含量范围</summary>
    public string? Aluminum { get; set; }

    /// <summary>钨(W)含量范围</summary>
    public string? Tungsten { get; set; }

    /// <summary>PREN腐蚀当量</summary>
    public string? PREN { get; set; }
}
