namespace MES.Data.Entities;

/// <summary>
/// 产品标准实体
/// </summary>
public class ProductionStandard : BaseEntity
{
    /// <summary>
    /// 标准编码，唯一索引
    /// </summary>
    public string StandardCode { get; set; } = null!;

    /// <summary>
    /// 标准名称
    /// </summary>
    public string StandardName { get; set; } = null!;

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 排序序号
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 导航属性 - 订单项次
    /// </summary>
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    /// <summary>
    /// 导航属性 - 产品要求
    /// </summary>
    public ICollection<ProductRequirement> ProductRequirements { get; set; } = new List<ProductRequirement>();
}