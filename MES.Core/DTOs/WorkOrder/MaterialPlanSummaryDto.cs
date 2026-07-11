using MES.Core.Enums;

namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 工单用料计划汇总
/// </summary>
public class WorkOrderMaterialPlanDto
{
    public int WorkOrderId { get; set; }
    public string WorkOrderNo { get; set; } = null!;

    /// <summary>用料计划状态</summary>
    public MaterialPlanStatus MaterialPlanStatus { get; set; }

    /// <summary>满足率(%)</summary>
    public decimal MaterialPlanRate { get; set; }

    /// <summary>各计划明细</summary>
    public List<MaterialPlanItemDto> Items { get; set; } = new();
}

/// <summary>
/// 单个用料计划明细
/// </summary>
public class MaterialPlanItemDto
{
    /// <summary>计划类型：Semi=原料采购, Finished=成品采购, Inventory=自有料, Rework=改制</summary>
    public string PlanType { get; set; } = null!;

    public string PlanTypeText { get; set; } = null!;
    public int RecordCount { get; set; }
    public string Summary { get; set; } = null!;
    public DateTime? RequiredDate { get; set; }

    /// <summary>该计划对工单总状态的贡献</summary>
    public MaterialPlanStatus Status { get; set; }
}
