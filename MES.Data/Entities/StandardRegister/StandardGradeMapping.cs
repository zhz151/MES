namespace MES.Data.Entities.StandardRegister;

/// <summary>
/// 标准牌号映射 — 客户标准牌号与工厂内部牌号的对照关系
/// </summary>
public class StandardGradeMapping : BaseEntity
{
    /// <summary>标准牌号（客户标准）</summary>
    public string StandardGrade { get; set; } = null!;

    /// <summary>标准牌号类别</summary>
    public string? StandardGradeCategory { get; set; }

    /// <summary>工厂牌号</summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>密度(g/cm³)</summary>
    public decimal Density { get; set; }

    /// <summary>热处理工艺</summary>
    public string? HeatTreatment { get; set; }

    /// <summary>是否特殊材料</summary>
    public bool SpecialMaterial { get; set; }

    /// <summary>特殊注意事项</summary>
    public string? SpecialNote { get; set; }

    /// <summary>
    /// 钢性：根据工厂牌号首字自动计算（3/9→奥氏体，2→双相钢，其他→镍基合金）
    /// </summary>
    public string SteelProperty { get; set; } = "镍基合金";

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}