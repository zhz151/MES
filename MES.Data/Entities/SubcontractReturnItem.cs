namespace MES.Data.Entities;

/// <summary>
/// 委外明细要求—子表（加工要求+费用+工单号）
/// </summary>
public class SubcontractReturnItem : BaseEntity
{
    /// <summary>
    /// 委外单主表ID（FK→SubcontractOrder）
    /// </summary>
    public int SubcontractOrderId { get; set; }

    /// <summary>
    /// 行号（从1开始）
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// 加工类型（如：穿孔/冷拔/热处理）
    /// </summary>
    public string ProcessType { get; set; } = null!;

    /// <summary>
    /// 加工后物料分类
    /// </summary>
    public string MaterialCategory { get; set; } = null!;

    /// <summary>
    /// 加工规格
    /// </summary>
    public string ProcessSpecification { get; set; } = null!;

    /// <summary>
    /// 状态备注
    /// </summary>
    public string? ProcessStatusRemark { get; set; }

    /// <summary>
    /// 加工单价
    /// </summary>
    public decimal? ProcessUnitPrice { get; set; }

    /// <summary>
    /// 加工总价
    /// </summary>
    public decimal? ProcessTotalAmount { get; set; }

    /// <summary>
    /// 对应工单号（为空表示余料）
    /// </summary>
    public string? SourceWorkOrderNo { get; set; }

    /// <summary>
    /// 导航属性
    /// </summary>
    public SubcontractOrder SubcontractOrder { get; set; } = null!;
}
