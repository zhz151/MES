// 文件路径: MES.Core/DTOs/WorkOrderDetailDto.cs

using MES.Core.Enums;

namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 工单详情 DTO
/// </summary>
public class WorkOrderDetailDto
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
    /// 合并的项次ID列表（逗号分隔）
    /// </summary>
    public string OrderItemIds { get; set; } = null!;

    /// <summary>
    /// 工单状态
    /// </summary>
    public WorkOrderStatus Status { get; set; }

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
    /// 是否延期罚款
    /// </summary>
    public bool DelayPenalty { get; set; }

    /// <summary>
    /// 钢管制造类别
    /// </summary>
    public PipeManufacturingType PipeManufacturingType { get; set; }

    /// <summary>
    /// 结算方式
    /// </summary>
    public SettlementMethod SettlementMethod { get; set; }

    /// <summary>
    /// 产品标准编码
    /// </summary>
    public string StandardCode { get; set; } = null!;

    /// <summary>
    /// 交货状态
    /// </summary>
    public DeliveryState DeliveryState { get; set; }

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 规格
    /// </summary>
    public string Specification { get; set; } = null!;

    /// <summary>
    /// 外径-外径负公差
    /// </summary>
    public decimal OuterDiameterNegative { get; set; }

    /// <summary>
    /// 外径+外径正公差
    /// </summary>
    public decimal OuterDiameterPositive { get; set; }

    /// <summary>
    /// 壁厚-壁厚负公差
    /// </summary>
    public decimal WallThicknessNegative { get; set; }

    /// <summary>
    /// 壁厚+壁厚正公差
    /// </summary>
    public decimal WallThicknessPositive { get; set; }

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
    /// 总米数
    /// </summary>
    public decimal TotalMeters { get; set; }

    /// <summary>
    /// 总重量
    /// </summary>
    public decimal TotalWeight { get; set; }

    /// <summary>
    /// 总项次数
    /// </summary>
    public int TotalItemCount { get; set; }

    /// <summary>
    /// 明细
    /// </summary>
    public string? ItemDetails { get; set; }

    /// <summary>
    /// 技术要求
    /// </summary>
    public string TechnicalRequirements { get; set; } = null!;

    /// <summary>
    /// 乐观并发控制版本号
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>
    /// 用料计划状态
    /// </summary>
    public MaterialPlanStatus MaterialPlanStatus { get; set; }

    /// <summary>
    /// 用料计划满足率(%)
    /// </summary>
    public decimal MaterialPlanRate { get; set; }

    /// <summary>
    /// 理论单支重(kg/支)
    /// </summary>
    public decimal? UnitWeight { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public string CreatedBy { get; set; } = null!;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTimeOffset UpdatedTime { get; set; }

    /// <summary>
    /// 更新人
    /// </summary>
    public string UpdatedBy { get; set; } = null!;
}