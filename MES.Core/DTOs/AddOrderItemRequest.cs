// 文件路径: MES.Core/DTOs/AddOrderItemRequest.cs
using System.ComponentModel.DataAnnotations;
using MES.Core.Enums;

namespace MES.Core.DTOs;

/// <summary>
/// 添加订单项次请求
/// </summary>
public class AddOrderItemRequest
{
    /// <summary>
    /// 项次号（可选，不填则自动追加到末尾）
    /// </summary>
    public int? Sequence { get; set; }

    /// <summary>
    /// 交货日期
    /// </summary>
    [Required(ErrorMessage = "交货日期不能为空")]
    public DateTime DeliveryDate { get; set; }

    /// <summary>
    /// 是否延期违约金（默认false）
    /// </summary>
    public bool DelayPenalty { get; set; }

    /// <summary>
    /// 结算方式
    /// </summary>
    [Required(ErrorMessage = "结算方式不能为空")]
    public SettlementMethod SettlementMethod { get; set; }

    /// <summary>
    /// 物料名称
    /// </summary>
    [Required(ErrorMessage = "物料名称不能为空")]
    public MaterialName MaterialName { get; set; }

    /// <summary>
    /// 产品标准ID
    /// </summary>
    [Required(ErrorMessage = "产品标准不能为空")]
    public int ProductionStandardId { get; set; }

    /// <summary>
    /// 交货状态
    /// </summary>
    [Required(ErrorMessage = "交货状态不能为空")]
    public DeliveryState DeliveryState { get; set; }

    /// <summary>
    /// 标准牌号
    /// </summary>
    [Required(ErrorMessage = "标准牌号不能为空")]
    public string StandardGrade { get; set; } = null!;

    /// <summary>
    /// 外径
    /// </summary>
    [Required(ErrorMessage = "外径不能为空")]
    public decimal OuterDiameter { get; set; }

    /// <summary>
    /// 壁厚
    /// </summary>
    [Required(ErrorMessage = "壁厚不能为空")]
    public decimal WallThickness { get; set; }

    /// <summary>
    /// 外径下偏差（默认0）
    /// </summary>
    public decimal OuterDiameterNegative { get; set; }

    /// <summary>
    /// 外径上偏差（默认0）
    /// </summary>
    public decimal OuterDiameterPositive { get; set; }

    /// <summary>
    /// 壁厚下偏差（默认0）
    /// </summary>
    public decimal WallThicknessNegative { get; set; }

    /// <summary>
    /// 壁厚上偏差（默认0）
    /// </summary>
    public decimal WallThicknessPositive { get; set; }

    /// <summary>
    /// 长度状态
    /// </summary>
    [Required(ErrorMessage = "长度状态不能为空")]
    public LengthStatus LengthStatus { get; set; }

    /// <summary>
    /// 最小长度（Fixed/Range时必填）
    /// </summary>
    public decimal? MinLength { get; set; }

    /// <summary>
    /// 最大长度（Range时必填）
    /// </summary>
    public decimal? MaxLength { get; set; }

    /// <summary>
    /// 数量（默认0）
    /// </summary>
    public int? Quantity { get; set; }

    /// <summary>
    /// 米数（Range/NonFixed时必填）
    /// </summary>
    public decimal? Meters { get; set; }

    /// <summary>
    /// 合同重量
    /// </summary>
    [Required(ErrorMessage = "合同重量不能为空")]
    public decimal ContractWeight { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}