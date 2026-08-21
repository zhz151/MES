using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Materials;

/// <summary>
/// 委外子项执行查询 — 列表 DTO
/// </summary>
public class SubcontractReturnItemListDto
{
    public int Id { get; set; }
    public int SubcontractOrderId { get; set; }

    /// <summary>
    /// 委外序号（委外单内明细行号，与进库批次 SourceOrderSequence 匹配）
    /// </summary>
    public int Sequence { get; set; }

    public string? OrderNo { get; set; }
    public string? SupplierName { get; set; }

    /// <summary>
    /// 下单日期（主表 OrderDate）
    /// </summary>
    public DateTime OrderDate { get; set; }

    public string? SourceWorkOrderNo { get; set; }
    public string? PlantGrade { get; set; }
    public string ProcessSpecification { get; set; } = null!;
    public decimal? UnitWeight { get; set; }
    public int? RequiredQuantity { get; set; }
    public decimal? RequiredWeight { get; set; }

    /// <summary>
    /// 要求到货日（主表 ReturnDeadline 收回期限）
    /// </summary>
    public DateTime? RequiredArrivalDate { get; set; }

    /// <summary>
    /// 委外备注（子项 Remark）
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 截止回收日（实际收回入库日期：按委外单号反查仓库批 InventoryBatch.InboundDate 最大值）
    /// </summary>
    public DateTime? ReturnDeadline { get; set; }

    public int ReturnedQuantity { get; set; }
    public decimal ReturnedWeight { get; set; }

    /// <summary>
    /// 退货支数（退货出库 ReturnSourceBatchNo → 原仓库批 → SourceOrderNo==委外单号）
    /// </summary>
    public int ReturnQuantity { get; set; }

    /// <summary>
    /// 退货重量(kg)
    /// </summary>
    public decimal ReturnWeight { get; set; }

    /// <summary>
    /// 属强制完成（子项 IsForceCompleted）
    /// </summary>
    public bool IsForceCompleted { get; set; }

    public SubcontractOrderStatus? ProcessStatus { get; set; }

    public string? ProcessStatusDisplay => ProcessStatus.HasValue ? EnumHelper.GetDisplayName(ProcessStatus.Value) : null;

    // ========== 工单实时关注（从工单执行状况读模型 WorkOrderExecutionSummary 按来源工单号关联，无记录默认 null → 前端 "-"） ==========
    /// <summary>工单关注(0=主号暂停 1=主号完成 2=原料锁定 3=生产执行 4=成品检验)</summary>
    public int? ExecutionScheduleStage { get; set; }

    /// <summary>原锁执行备注（原料锁定原因）</summary>
    public string? ExecutionRawMaterialLockRemark { get; set; }

    /// <summary>计划性（A+急/A急/B顺/C缓/D缓）</summary>
    public string? ExecutionUrgencyLevel { get; set; }

    /// <summary>理论截止投料日</summary>
    public DateTime? ExecutionTheoreticalCutoffDate { get; set; }
}
