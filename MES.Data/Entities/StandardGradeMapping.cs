namespace MES.Data.Entities;

/// <summary>
/// 牌号对照实体
/// </summary>
public class StandardGradeMapping : BaseEntity
{
    /// <summary>
    /// 标准牌号，唯一索引
    /// </summary>
    public string StandardGrade { get; set; } = null!;

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 密度（g/cm³）
    /// </summary>
    public decimal Density { get; set; }

    /// <summary>
    /// 热处理工艺
    /// </summary>
    public string? HeatTreatment { get; set; }

    /// <summary>
    /// 是否特殊材料
    /// </summary>
    public bool SpecialMaterial { get; set; }

    /// <summary>
    /// 特殊注意事项
    /// </summary>
    public string? SpecialNote { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 导航属性 - 订单项次
    /// </summary>
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}