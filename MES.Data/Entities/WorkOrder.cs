// 文件路径: MES.Data/Entities/WorkOrder.cs

using MES.Core.Enums;

namespace MES.Data.Entities;

/// <summary>
/// 工单实体
/// </summary>
public class WorkOrder : BaseEntity
{
    /// <summary>
    /// 工单号（业务唯一标识）
    /// 格式：{订单号}-{主号}[-{次号}]，如 PO2026001-D01-C01
    /// </summary>
    public string WorkOrderNo { get; set; } = null!;

    /// <summary>
    /// 源订单号（冗余字段，对应 SalesOrder.OrderNumber）
    /// </summary>
    public string SalesOrderNo { get; set; } = null!;

    /// <summary>
    /// 主号
    /// 格式：前缀 + 2位数字（H/D/F/L + 01-99）
    /// </summary>
    public string ProductionMainNo { get; set; } = null!;

    /// <summary>
    /// 次号（可为空）
    /// 格式：C + 2位数字（定尺时必填）
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
    /// 乐观并发控制版本号
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    // ========== 订单基本信息（冗余） ==========

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

    // ========== 项次公共字段（从OrderItem合并） ==========

    /// <summary>
    /// 交货日期
    /// </summary>
    public DateTime DeliveryDate { get; set; }

    /// <summary>
    /// 是否延期罚款
    /// </summary>
    public bool DelayPenalty { get; set; }

    /// <summary>
    /// 物料名称
    /// </summary>
    public MaterialName MaterialName { get; set; }

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
    /// 规格（外径*壁厚）
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

    // ========== 长度与汇总字段 ==========

    /// <summary>
    /// 最小长度（mm）
    /// </summary>
    public decimal? MinLength { get; set; }

    /// <summary>
    /// 最大长度（mm）
    /// </summary>
    public decimal? MaxLength { get; set; }

    /// <summary>
    /// 总数量（支数）
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
    /// 明细（格式：项次号,长度mm,支数;）
    /// </summary>
    public string? ItemDetails { get; set; }

    // ========== 技术要求 ==========

    /// <summary>
    /// 技术要求（Special=特殊，Normal=常规）
    /// </summary>
    public RequirementType TechnicalRequirements { get; set; } = RequirementType.Normal;

    // ========== 用料计划状态 ==========

    /// <summary>
    /// 用料计划状态（0=未计划 1=部分 2=理论满足 3=满足 4=超量）
    /// </summary>
    public MaterialPlanStatus MaterialPlanStatus { get; set; } = MaterialPlanStatus.NotPlanned;

    /// <summary>
    /// 用料计划满足率(%)
    /// </summary>
    public decimal MaterialPlanRate { get; set; }
}