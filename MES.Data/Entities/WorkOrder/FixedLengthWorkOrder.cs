namespace MES.Data.Entities.WorkOrder;

/// <summary>
/// 定尺工单：按「长度」聚合的定尺计划
/// 仅在工单生成/编辑时从订单项（OrderItem）生成，不做人工增删改
/// </summary>
public class FixedLengthWorkOrder : BaseEntity
{
    /// <summary>
    /// 关联工单ID
    /// </summary>
    public int WorkOrderId { get; set; }

    /// <summary>
    /// 工单号（冗余，从 WorkOrder 填充，用于跨模块按工单号校验）
    /// </summary>
    public string WorkOrderNo { get; set; } = null!;

    /// <summary>
    /// 订单号（冗余，从 WorkOrder 填充，用于跨模块按「订单号+主号」校验定尺长度）
    /// </summary>
    public string SalesOrderNo { get; set; } = null!;

    /// <summary>
    /// 主号（冗余，从 WorkOrder 填充）
    /// </summary>
    public string ProductionMainNo { get; set; } = null!;

    /// <summary>
    /// 定尺长度(mm，来自 OrderItem.MaxLength)
    /// </summary>
    public decimal Length { get; set; }

    /// <summary>
    /// 计划支数（同长度项次 Quantity 求和）
    /// </summary>
    public int PlannedQuantity { get; set; }
}
