namespace MES.Data.Entities;

/// <summary>
/// 产品标准实体
/// </summary>
public class ProductionStandard : BaseEntity
{
    /// <summary>
    /// 标准编码
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
    /// 排序号
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 关联订单项次列表
    /// </summary>
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
