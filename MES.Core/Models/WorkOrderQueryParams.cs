namespace MES.Core.Models;

/// <summary>
/// 工单分页查询参数
/// </summary>
public class WorkOrderQueryParams : QueryParams
{
    /// <summary>
    /// 订单号筛选
    /// </summary>
    public string? SalesOrderNo { get; set; }

    /// <summary>
    /// 主号筛选
    /// </summary>
    public string? ProductionMainNo { get; set; }

    /// <summary>
    /// 次号筛选
    /// </summary>
    public string? ProductionSubNo { get; set; }

    /// <summary>
    /// 工单状态筛选（0=未编制，1=已确定，2=待修正，3=已取消）
    /// </summary>
    public int? Status { get; set; }

    /// <summary>
    /// 工单状态字符串筛选（NotGenerated/Confirmed/Pending/Cancelled）
    /// </summary>
    public string? WorkOrderStatus { get; set; }

    /// <summary>
    /// 物料名称筛选
    /// </summary>
    public string? MaterialName { get; set; }

    /// <summary>
    /// 规格筛选
    /// </summary>
    public string? Specification { get; set; }

    /// <summary>
    /// 交货日期开始
    /// </summary>
    public DateTime? DeliveryDateStart { get; set; }

    /// <summary>
    /// 交货日期结束
    /// </summary>
    public DateTime? DeliveryDateEnd { get; set; }

    /// <summary>
    /// 业务员筛选
    /// </summary>
    public string? Salesman { get; set; }

    /// <summary>
    /// 最终客户筛选
    /// </summary>
    public string? EndCustomer { get; set; }

    /// <summary>
    /// 工厂牌号筛选
    /// </summary>
    public string? PlantGrade { get; set; }

    /// <summary>
    /// 是否包含已取消订单（用于待删除区域）
    /// </summary>
    public bool IncludeCancelled { get; set; }

    /// <summary>
    /// 用料计划状态筛选（0=未计划，1=部分，2=理论满足，3=满足，4=超量）
    /// </summary>
    public int? MaterialPlanStatus { get; set; }

    /// <summary>
    /// 关联主号用料状态筛选
    /// </summary>
    public int? MainNoMaterialPlanStatus { get; set; }

    /// <summary>
    /// 关联订单用料状态筛选
    /// </summary>
    public int? OrderMaterialPlanStatus { get; set; }

    /// <summary>
    /// 计划类型过滤：仅显示包含指定类型计划的工单
    /// 可选值: Semi(原料采购), Finish(成品采购), Inventory(库存使用), Rework(库料改制), Piercing(圆棒穿孔)
    /// 多个用逗号分隔，如 "Semi,Finish"
    /// </summary>
    public string? PlanTypeFilter { get; set; }
}