namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 用料计划总览「待投料量汇总」卡片点击联动筛选条件。
/// 由卡片矩阵单元格/行列点击产生，后端在工单列表查询时附加：
/// 备注(RawMaterialLockRemark) + 计划性(UrgencyLevel) 精确匹配，且严格限定 ScheduleStage=2（原料锁定主号），
/// 保证联动结果与卡片统计域（原料锁定主号）完全一致。
/// </summary>
public class MaterialPlanLinkFilterDto
{
    /// <summary>原锁备注英文 Key（RawMaterialLockRemarkKeys.All），null=不限</summary>
    public string? Remark { get; set; }

    /// <summary>计划性英文 Key（UrgencyLevelKeys 排除 EPaused），null=不限</summary>
    public string? Urgency { get; set; }

    /// <summary>是否成购矩阵联动（外购成品「包含」口径）：仅筛成品采购计划量 &gt; 0 的工单（FinishPlanWeight &gt; 0，含单一成品采购）</summary>
    public bool PurchaseOnly { get; set; }

    /// <summary>是否待投料矩阵联动（排除「单一成品采购」工单）：成品采购计划量 &gt; 0 且其余 6 类计划量全部 ≤ 0 的工单排除，单数与重量与卡片待投料口径同步</summary>
    public bool ExcludeSingleFinishPurchase { get; set; }
}
