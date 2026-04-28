// 文件路径: MES.Core/DTOs/WorkOrderListDto.cs

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
    /// 工厂牌号
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 物料名称
    /// </summary>
    public string MaterialName { get; set; } = null!;

    /// <summary>
    /// 规格
    /// </summary>
    public string Specification { get; set; } = null!;

    /// <summary>
    /// 交货日期
    /// </summary>
    public DateTime DeliveryDate { get; set; }

    /// <summary>
    /// 总数量
    /// </summary>
    public int TotalQuantity { get; set; }

    /// <summary>
    /// 总重量
    /// </summary>
    public decimal TotalWeight { get; set; }

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
}