namespace MES.Data.Entities.Quality;

/// <summary>
/// 来料炉号登记 — 原料来料的炉号及化学成分登记
/// </summary>
public class FurnaceRegistration : BaseEntity
{
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

    /// <summary>炉号（唯一）</summary>
    public string FurnaceNumber { get; set; } = null!;

    /// <summary>规格</summary>
    public string? Specification { get; set; }

    /// <summary>支数</summary>
    public int? Quantity { get; set; }

    /// <summary>重量</summary>
    public decimal? Weight { get; set; }

    /// <summary>碳(C)</summary>
    public decimal? Carbon { get; set; }

    /// <summary>硅(Si)</summary>
    public decimal? Silicon { get; set; }

    /// <summary>锰(Mn)</summary>
    public decimal? Manganese { get; set; }

    /// <summary>磷(P)</summary>
    public decimal? Phosphorus { get; set; }

    /// <summary>硫(S)</summary>
    public decimal? Sulfur { get; set; }

    /// <summary>镍(Ni)</summary>
    public decimal? Nickel { get; set; }

    /// <summary>铬(Cr)</summary>
    public decimal? Chromium { get; set; }

    /// <summary>钼(Mo)</summary>
    public decimal? Molybdenum { get; set; }

    /// <summary>铜(Cu)</summary>
    public decimal? Copper { get; set; }

    /// <summary>氮(N)</summary>
    public decimal? Nitrogen { get; set; }

    /// <summary>铌(Nb)</summary>
    public decimal? Niobium { get; set; }

    /// <summary>钛(Ti)</summary>
    public decimal? Titanium { get; set; }

    /// <summary>铁(Fe)</summary>
    public decimal? Iron { get; set; }

    /// <summary>铝(Al)</summary>
    public decimal? Aluminum { get; set; }

    /// <summary>钨(W)</summary>
    public decimal? Tungsten { get; set; }

    /// <summary>PREN腐蚀当量</summary>
    public decimal? PREN { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}
