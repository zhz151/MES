using MES.Core.Enums;
namespace MES.Core.DTOs.Materials;

/// <summary>
/// 用料计划详情（用于点击工单号自动填充采购订单行）
/// </summary>
public class PlanDetailDto
{
    /// <summary>
    /// 工单号
    /// </summary>
    public string WorkOrderNo { get; set; } = null!;

    /// <summary>
    /// 物料类别（DB 中文值，如"荒管"、"半成品"）
    /// </summary>
    public string MaterialCategory { get; set; } = null!;

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string? PlantGrade { get; set; }

    /// <summary>
    /// 规格
    /// </summary>
    public string? Specification { get; set; }

    /// <summary>
    /// 单重(kg/支)
    /// </summary>
    public decimal? UnitWeight { get; set; }

    /// <summary>
    /// 数量（支数）
    /// </summary>
    public int? Quantity { get; set; }

    /// <summary>
    /// 重量(kg)
    /// </summary>
    public decimal Weight { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 要求到货日期
    /// </summary>
    public DateTime? RequiredDate { get; set; }

    /// <summary>
    /// 投料倍率(1制几)
    /// </summary>
    public int? InputMultiple { get; set; }
}
