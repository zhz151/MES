using MES.Core.Enums;

namespace MES.Data.Entities.Warehouse;

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
public class OutboundRecord : IAuditableEntity
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
    /// 仓库批次号（从 InventoryBatch 冗余快照）
    /// </summary>
    public string? BatchNo { get; set; }

    /// <summary>
    /// 出库类型
    /// </summary>
    public OutboundType OutboundType { get; set; }

    /// <summary>
    /// 出库工单号（创建时默认取关联仓库批的工单号，可手动修改）
    /// </summary>
    public string? WorkOrderNo { get; set; }

    /// <summary>
    /// 退货-原仓库批（退货出库时记录原仓库批号）
    /// </summary>
    public string? ReturnSourceBatchNo { get; set; }

    /// <summary>
    /// 委外-穿孔号（委外出库+圆棒时关联委外单号）
    /// </summary>
    public string? SourceOrderNo { get; set; }

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
    /// 出库米数（仅成品库使用）
    /// </summary>
    public decimal? OutboundMeters { get; set; }

    /// <summary>
    /// 出库日期
    /// </summary>
    public DateTime OutboundDate { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    // ========== 审计字段 ==========

    public DateTimeOffset CreatedTime { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset UpdatedTime { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}
