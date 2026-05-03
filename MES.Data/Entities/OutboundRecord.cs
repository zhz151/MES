namespace MES.Data.Entities;

/// <summary>
/// 出库记录
/// </summary>
/// <remarks>
/// 不使用 BaseEntity，原因：
/// 1. Id 为 bigint (long)，出库记录量大需要更大范围的主键
/// 2. BaseEntity 使用 int Id，无法满足需求
/// 3. 审计字段手动声明，与 BaseEntity 保持一致的字段名和行为
/// 4. AppDbContext.SaveChangesAsync 对非 BaseEntity 的实体不走自动审计，
///    由 Service 层在创建时手动填充 CreatedTime/CreatedBy 等字段
/// </remarks>
public class OutboundRecord
{
    /// <summary>
    /// 主键，自增(bigint)
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 关联库存批次
    /// </summary>
    public int InventoryBatchId { get; set; }

    /// <summary>
    /// 出库类型
    /// </summary>
    public string OutboundType { get; set; } = null!;

    /// <summary>
    /// 目标单位
    /// </summary>
    public string? TargetCompany { get; set; }

    /// <summary>
    /// 出库支数
    /// </summary>
    public int OutboundQuantity { get; set; }

    /// <summary>
    /// 出库重量(kg)
    /// </summary>
    public decimal OutboundWeight { get; set; }

    /// <summary>
    /// 出库日期
    /// </summary>
    public DateTime OutboundDate { get; set; }

    /// <summary>
    /// 操作人
    /// </summary>
    public string Operator { get; set; } = null!;

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    // ========== 审计字段（手动定义，因Id为bigint不继承BaseEntity） ==========

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public string CreatedBy { get; set; } = null!;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTimeOffset UpdatedTime { get; set; }

    /// <summary>
    /// 更新人
    /// </summary>
    public string UpdatedBy { get; set; } = null!;
}
