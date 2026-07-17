namespace MES.Data.Entities.Materials;

public class Material : BaseEntity
{
    /// <summary>
    /// 物料编码（MA + 4位数字流水）
    /// </summary>
    public string MaterialCode { get; set; } = null!;

    /// <summary>
    /// 物料分类（MaterialType 枚举名，兼作物料名称）
    /// </summary>
    public string MaterialCategory { get; set; } = null!;

    /// <summary>
    /// 厂内钢种
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 名义规格
    /// </summary>
    public string Specification { get; set; } = null!;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}
