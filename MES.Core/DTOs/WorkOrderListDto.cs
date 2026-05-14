// 文件路径: MES.Core/DTOs/WorkOrderListDto.cs

using MES.Core.Enums;

namespace MES.Core.DTOs;

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
    public MaterialName MaterialName { get; set; }

    /// <summary>
    /// 规格
    /// </summary>
    public string Specification { get; set; } = null!;

    /// <summary>
    /// 长度状态
    /// </summary>
    public LengthStatus LengthStatus { get; set; }

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
    /// 工单状态值
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// 工单状态文本
    /// </summary>
public string StatusText
{
    get
    {
        return Status switch
        {
            0 => "未编制",
            1 => "已确定",
            2 => "待修正",
            3 => "已取消",
            _ => "未知"
        };
    }
}

    /// <summary>
    /// 工单用料计划状态
    /// </summary>
    public int MaterialPlanStatus { get; set; }

    /// <summary>
    /// 工单满足率(%)
    /// </summary>
    public decimal MaterialPlanRate { get; set; }

    /// <summary>
    /// 关联主号用料状态（同一订单+主号下所有工单聚合后的状态，使用原始标准不含"理论满足"）
    /// </summary>
    public int MainNoMaterialPlanStatus { get; set; }

    /// <summary>
    /// 主号满足率(%)
    /// </summary>
    public decimal MainNoMaterialPlanRate { get; set; }

    /// <summary>
    /// 关联订单用料状态（同一订单下所有主号均无"部分"和"未计划"即为全部满足）
    /// </summary>
    public int OrderMaterialPlanStatus { get; set; }

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
                    parts.Add($"穿{Math.Round(PiercingPlanTotalPieces.Value / (decimal)totalQty * 100)}%");
                if (SemiPlanTotalPieces > 0)
                    parts.Add($"荒{Math.Round(SemiPlanTotalPieces.Value / (decimal)totalQty * 100)}%");
                if (FinishedPlanTotalPieces > 0)
                    parts.Add($"成{Math.Round(FinishedPlanTotalPieces.Value / (decimal)totalQty * 100)}%");
                if (InventoryPlanTotalPieces > 0)
                    parts.Add($"库{Math.Round(InventoryPlanTotalPieces.Value / (decimal)totalQty * 100)}%");
                if (ReworkPlanTotalPieces > 0)
                    parts.Add($"改{Math.Round(ReworkPlanTotalPieces.Value / (decimal)totalQty * 100)}%");
            }
            else
            {
                var totalWt = TotalWeight;
                if (totalWt <= 0) return null;
                if (PiercingPlanTotalWeight > 0)
                    parts.Add($"穿{Math.Round(PiercingPlanTotalWeight.Value / totalWt * 100)}%");
                if (SemiPlanTotalWeight > 0)
                    parts.Add($"荒{Math.Round(SemiPlanTotalWeight.Value / totalWt * 100)}%");
                if (FinishedPlanTotalWeight > 0)
                    parts.Add($"成{Math.Round(FinishedPlanTotalWeight.Value / totalWt * 100)}%");
                if (InventoryPlanTotalWeight > 0)
                    parts.Add($"库{Math.Round(InventoryPlanTotalWeight.Value / totalWt * 100)}%");
                if (ReworkPlanTotalWeight > 0)
                    parts.Add($"改{Math.Round(ReworkPlanTotalWeight.Value / totalWt * 100)}%");
            }

            return parts.Any() ? string.Join(" ", parts) : null;
        }
    }
}