// 文件路径: MES.Core/DTOs/WorkOrderListDto.cs

using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 工单列表 DTO
/// </summary>
public class WorkOrderListDto
{
    /// <summary>
    /// 工单ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 工单号
    /// </summary>
    public string WorkOrderNo { get; set; } = null!;

    /// <summary>
    /// 源订单号
    /// </summary>
    public string SalesOrderNo { get; set; } = null!;

    /// <summary>
    /// 主号
    /// </summary>
    public string ProductionMainNo { get; set; } = null!;

    /// <summary>
    /// 次号
    /// </summary>
    public string? ProductionSubNo { get; set; }

    /// <summary>
    /// 签订日期
    /// </summary>
    public DateTime SignDate { get; set; }

    /// <summary>
    /// 业务员
    /// </summary>
    public string Salesman { get; set; } = null!;

    /// <summary>
    /// 最终用户
    /// </summary>
    public string? EndCustomer { get; set; }

    /// <summary>
    /// 交货日期
    /// </summary>
    public DateTime DeliveryDate { get; set; }

    /// <summary>
    /// 延期罚款
    /// </summary>
    public bool DelayPenalty { get; set; }

    /// <summary>
    /// 结算方式
    /// </summary>
    public SettlementMethod SettlementMethod { get; set; }

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 物料名称
    /// </summary>
    public PipeManufacturingType PipeManufacturingType { get; set; }

    /// <summary>
    /// 规格
    /// </summary>
    public string Specification { get; set; } = null!;

    /// <summary>
    /// 长度状态
    /// </summary>
    public LengthStatus LengthStatus { get; set; }

    /// <summary>
    /// 最小长度
    /// </summary>
    public decimal? MinLength { get; set; }

    /// <summary>
    /// 最大长度
    /// </summary>
    public decimal? MaxLength { get; set; }

    /// <summary>
    /// 总数量
    /// </summary>
    public int TotalQuantity { get; set; }

    /// <summary>
    /// 总重量
    /// </summary>
    public decimal TotalWeight { get; set; }

    /// <summary>
    /// 交货状态
    /// </summary>
    public DeliveryState DeliveryState { get; set; }

    /// <summary>
    /// 总项次数（含项次数）
    /// </summary>
    public int TotalItemCount { get; set; }

    /// <summary>
    /// 主号-关注（int 5 档：0=主号暂停 1=主号完成 2=原料锁定 3=生产执行 4=成品检验）
    /// 来自 WorkOrderExecutionSummary（主号级），无执行摘要时为空
    /// </summary>
    public int? ScheduleStage { get; set; }

    /// <summary>
    /// 主号-原锁备注（仅 ScheduleStage=2 原料锁定时有值；四类英文 Key，中文显示走 RawMaterialLockRemarkKeys）
    /// </summary>
    public string? RawMaterialLockRemark { get; set; }

    /// <summary>
    /// 主号-计划性（紧急性英文 Key：APlusUrgent/AUrgent/BOrder/CSlow/DSlow/EPaused）
    /// </summary>
    public string? UrgencyLevel { get; set; }

    /// <summary>
    /// 工单到料未投（kg）= Max(0, 现可投料总重 TotalAvailableWeight − 工单投料量 InputWeight)。
    /// 现可投料总重 = 七类到货/出库/投料量之和（G4委外到货+G5采购到货+G6采购到货+G7出库量+G8投料量+G9投料量+G10投料量，与 WorkOrderExecutionSummaryDto.TotalAvailableWeight 同口径）
    /// </summary>
    public decimal? PendingInputWeight { get; set; }

    /// <summary>
    /// 理论缺失总料重（kg）= Max(0, 计划投料总重 TotalPlanWeight − 现可投料总重 TotalAvailableWeight)，
    /// 与 WorkOrderExecutionSummaryDto.TotalMissingWeight 同口径（原料未至），无执行摘要时为空
    /// </summary>
    public decimal? TotalMissingWeight { get; set; }

    /// <summary>
    /// 工单投料量（kg）：WorkOrderExecutionSummary.InputWeight（原始投料组「总重量」），无执行摘要时为空
    /// </summary>
    public decimal? InputWeight { get; set; }

    /// <summary>
    /// 工单投料比（%）：WorkOrderExecutionSummary.InputOutputRatio（原始投料组「工单投料比」）
    /// </summary>
    public decimal? InputOutputRatio { get; set; }

    /// <summary>
    /// 工单投料状态：WorkOrderExecutionSummary.InputStatus（0 未投料/1 部分/2 满足/3 超量），中文显示走 IntStatusDisplayHelper.GetInputStatusText
    /// </summary>
    public int? InputStatus { get; set; }

    /// <summary>
    /// 工单状态
    /// </summary>
    public WorkOrderStatus Status { get; set; }

    /// <summary>
    /// 工单用料计划状态
    /// </summary>
    public MaterialPlanStatus MaterialPlanStatus { get; set; }

    /// <summary>
    /// 工单满足率(%)
    /// </summary>
    public decimal MaterialPlanRate { get; set; }

    /// <summary>
    /// 关联主号用料状态（同一订单+主号下所有工单聚合后的状态，4 档：未计划/部分/满足/超量）
    /// </summary>
    public MaterialPlanStatus MainNoMaterialPlanStatus { get; set; }

    /// <summary>
    /// 主号满足率(%)
    /// </summary>
    public decimal MainNoMaterialPlanRate { get; set; }

    /// <summary>
    /// 关联订单用料状态（同一订单下所有主号均无"部分"和"未计划"即为全部满足）
    /// </summary>
    public MaterialPlanStatus OrderMaterialPlanStatus { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedTime { get; set; }

    // ========== 最新计划日期 ==========

    /// <summary>
    /// 4种用料计划中最新的计划日期（取最大值），无计划时为 null
    /// </summary>
    public DateTime? LatestPlanDate { get; set; }

    // ========== 各类用料重量/数量汇总 ==========

    /// <summary>
    /// 原料采购计划总重量(kg)
    /// </summary>
    public decimal? SemiPlanTotalWeight { get; set; }

    /// <summary>
    /// 成品采购计划总重量(kg)
    /// </summary>
    public decimal? FinishedPlanTotalWeight { get; set; }

    /// <summary>
    /// 库存使用计划总重量(kg)
    /// </summary>
    public decimal? InventoryPlanTotalWeight { get; set; }

    /// <summary>
    /// 库料改制计划总重量(kg)
    /// </summary>
    public decimal? ReworkPlanTotalWeight { get; set; }

    // 各类计划总支数（定尺时使用）

    /// <summary>
    /// 原料采购计划总支数
    /// </summary>
    public int? SemiPlanTotalPieces { get; set; }

    /// <summary>
    /// 成品采购计划总支数
    /// </summary>
    public int? FinishedPlanTotalPieces { get; set; }

    /// <summary>
    /// 库存使用计划出库总支数
    /// </summary>
    public int? InventoryPlanTotalPieces { get; set; }

    /// <summary>
    /// 库料改制计划出库总支数
    /// </summary>
    public int? ReworkPlanTotalPieces { get; set; }

    /// <summary>
    /// 圆棒穿孔计划总重量(kg)
    /// </summary>
    public decimal? PiercingPlanTotalWeight { get; set; }

    /// <summary>
    /// 圆棒穿孔计划总支数
    /// </summary>
    public int? PiercingPlanTotalPieces { get; set; }

    /// <summary>
    /// 在产改制计划总重量(kg)
    /// </summary>
    public decimal? InProcessReworkPlanTotalWeight { get; set; }

    /// <summary>
    /// 在产改制计划总支数
    /// </summary>
    public int? InProcessReworkPlanTotalPieces { get; set; }

    /// <summary>
    /// 在产主工单计划总重量(kg)
    /// </summary>
    public decimal? InMainWorkOrderPlanTotalWeight { get; set; }

    /// <summary>
    /// 在产主工单计划总支数
    /// </summary>
    public int? InMainWorkOrderPlanTotalPieces { get; set; }

    /// <summary>
    /// 最大工艺周期（天）
    /// </summary>
    public int MaxStandardCycle { get; set; }

    /// <summary>
    /// 主号最大工艺周期（天）：同主号下所有工单 MaxStandardCycle 的最大值
    /// </summary>
    public int MainNoMaxStandardCycle { get; set; }

    /// <summary>
    /// 产能工量（天）：主号完成时为 null（显示「-」），其余无剩余产能时为 0
    /// </summary>
    public int? CapacityWorkDays { get; set; }

    /// <summary>
    /// 理论截止投料日
    /// </summary>
    public DateTime? TheoreticalCutoffDate { get; set; }

    /// <summary>
    /// 用料占比：有做计划的料态种数(0-4)
    /// </summary>
    public int MaterialPlanCoveredCount { get; set; }

    /// <summary>
    /// 用料占比文本（如 "穿105% 荒160% 成20% 库40% 改30%"）
    /// </summary>
    public string? MaterialPlanProportion { get; set; }

    /// <summary>
    /// 最新要求到货日
    /// </summary>
    public DateTime? LatestRequiredDate { get; set; }

    /// <summary>
    /// 获取各类占比文本（如 "原30% 成20% 库40% 改10% 穿5%"）
    /// 定尺按支数，非定尺/范围尺按重量
    /// </summary>
    public string? PlanProportionText
    {
        get
        {
            var isFixed = LengthStatus == LengthStatus.Fixed;
            var parts = new List<string>();

            if (isFixed)
            {
                var totalQty = TotalQuantity;
                if (totalQty <= 0) return null;
                if (PiercingPlanTotalPieces > 0)
                    parts.Add($"穿{PiercingPlanTotalPieces.Value / (decimal)totalQty * 100:F0}%");
                if (SemiPlanTotalPieces > 0)
                    parts.Add($"荒{SemiPlanTotalPieces.Value / (decimal)totalQty * 100:F0}%");
                if (FinishedPlanTotalPieces > 0)
                    parts.Add($"成{FinishedPlanTotalPieces.Value / (decimal)totalQty * 100:F0}%");
                if (InventoryPlanTotalPieces > 0)
                    parts.Add($"库{InventoryPlanTotalPieces.Value / (decimal)totalQty * 100:F0}%");
                if (ReworkPlanTotalPieces > 0)
                    parts.Add($"改{ReworkPlanTotalPieces.Value / (decimal)totalQty * 100:F0}%");
                if (InProcessReworkPlanTotalPieces > 0)
                    parts.Add($"在{InProcessReworkPlanTotalPieces.Value / (decimal)totalQty * 100:F0}%");
                if (InMainWorkOrderPlanTotalPieces > 0)
                    parts.Add($"主{InMainWorkOrderPlanTotalPieces.Value / (decimal)totalQty * 100:F0}%");
            }
            else
            {
                var totalWt = TotalWeight;
                if (totalWt <= 0) return null;
                if (PiercingPlanTotalWeight > 0)
                    parts.Add($"穿{PiercingPlanTotalWeight.Value / totalWt * 100:F0}%");
                if (SemiPlanTotalWeight > 0)
                    parts.Add($"荒{SemiPlanTotalWeight.Value / totalWt * 100:F0}%");
                if (FinishedPlanTotalWeight > 0)
                    parts.Add($"成{FinishedPlanTotalWeight.Value / totalWt * 100:F0}%");
                if (InventoryPlanTotalWeight > 0)
                    parts.Add($"库{InventoryPlanTotalWeight.Value / totalWt * 100:F0}%");
                if (ReworkPlanTotalWeight > 0)
                    parts.Add($"改{ReworkPlanTotalWeight.Value / totalWt * 100:F0}%");
                if (InProcessReworkPlanTotalWeight > 0)
                    parts.Add($"在{InProcessReworkPlanTotalWeight.Value / totalWt * 100:F0}%");
                if (InMainWorkOrderPlanTotalWeight > 0)
                    parts.Add($"主{InMainWorkOrderPlanTotalWeight.Value / totalWt * 100:F0}%");
            }

            return parts.Any() ? string.Join(" ", parts) : null;
        }
    }
}