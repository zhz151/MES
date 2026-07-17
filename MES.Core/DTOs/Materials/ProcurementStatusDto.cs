namespace MES.Core.DTOs.Materials;

/// <summary>
/// 工单用料计划采购执行状态
/// </summary>
public class ProcurementStatusDto
{
    /// <summary>工单号</summary>
    public string WorkOrderNo { get; set; } = null!;

    /// <summary>物料名称</summary>
    public string MaterialName { get; set; } = null!;

    /// <summary>物料分类（MaterialType 枚举名）</summary>
    public string? MaterialCategory { get; set; }

    /// <summary>计划总量(kg)</summary>
    public decimal PlanWeight { get; set; }

    /// <summary>已采购重量(kg)</summary>
    public decimal PurchaseWeight { get; set; }

    /// <summary>已委外重量(kg)</summary>
    public decimal SubcontractWeight { get; set; }

    /// <summary>合计已执行(kg)</summary>
    public decimal TotalWeight { get; set; }

    /// <summary>状态：未采购 / 部分采购</summary>
    public string StatusText { get; set; } = null!;
}
