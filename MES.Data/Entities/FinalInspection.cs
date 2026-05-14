using MES.Core.Enums;

namespace MES.Data.Entities;

/// <summary>
/// 成品检验 — 成品最终质量检验记录
/// </summary>
public class FinalInspection : BaseEntity
{
    // ========== 检验基本信息 ==========

    /// <summary>
    /// 检验项目（PMI检验/表检/尺寸/内窥/水压/水下气压/涡流/超声波/端口着色）
    /// </summary>
    public InspectionItem InspectionItem { get; set; }

    /// <summary>
    /// 检验日期
    /// </summary>
    public DateTime InspectionDate { get; set; }

    /// <summary>
    /// 生产编号（用户输入）
    /// </summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>
    /// 关联生产批次ID（由BatchNo自动解析）
    /// </summary>
    public int ProductionBatchId { get; set; }

    // ========== 批次冗余字段（从ProductionBatch自动调取） ==========

    /// <summary>
    /// 物料名称（从批次冗余）
    /// </summary>
    public string? MaterialName { get; set; }

    /// <summary>
    /// 挂牌号（从批次冗余）
    /// </summary>
    public string? TagNo { get; set; }

    /// <summary>
    /// 关联工单号（从批次冗余）
    /// </summary>
    public string? WorkOrderNo { get; set; }

    /// <summary>
    /// 关联订单号（从批次冗余）
    /// </summary>
    public string? SalesOrderNo { get; set; }

    /// <summary>
    /// 来料单位（从批次冗余）
    /// </summary>
    public string? SourceUnit { get; set; }

    /// <summary>
    /// 炉号（从批次冗余）
    /// </summary>
    public string? FurnaceNo { get; set; }

    /// <summary>
    /// 工厂牌号（从批次冗余）
    /// </summary>
    public string? PlantGrade { get; set; }

    /// <summary>
    /// 规格（从批次冗余）
    /// </summary>
    public string? Specification { get; set; }

    /// <summary>
    /// 定尺长度（从批次冗余）
    /// </summary>
    public string? FixedLength { get; set; }

    // ========== 执行信息 ==========

    /// <summary>
    /// 设备名称
    /// </summary>
    public string? EquipmentName { get; set; }

    /// <summary>
    /// 班次
    /// </summary>
    public string? Shift { get; set; }

    /// <summary>
    /// 操作员
    /// </summary>
    public string? Operator { get; set; }

    // ========== 数量/重量 ==========

    /// <summary>
    /// 检验支数
    /// </summary>
    public int? Quantity { get; set; }

    /// <summary>
    /// 检验重量(kg)
    /// </summary>
    public decimal? Weight { get; set; }

    // ========== 检验结果 ==========

    /// <summary>
    /// 合格支数
    /// </summary>
    public int? QualifiedQuantity { get; set; }

    /// <summary>
    /// 合格重量(kg)
    /// </summary>
    public decimal? QualifiedWeight { get; set; }

    /// <summary>
    /// 不合格返整支数
    /// </summary>
    public int? DefectReworkQuantity { get; set; }

    /// <summary>
    /// 不合格入库支数
    /// </summary>
    public int? DefectWarehouseQuantity { get; set; }

    /// <summary>
    /// 不合格报废支数
    /// </summary>
    public int? DefectScrapQuantity { get; set; }

    /// <summary>
    /// 不合格情况描述
    /// </summary>
    public string? DefectDescription { get; set; }

    // ========== 尺寸检验专用字段（仅InspectionItem=Dimension时有效） ==========

    /// <summary>
    /// 外径范围（尺寸检验专用）
    /// </summary>
    public string? OuterDiameterRange { get; set; }

    /// <summary>
    /// 壁厚范围（尺寸检验专用）
    /// </summary>
    public string? WallThicknessRange { get; set; }

    /// <summary>
    /// 长度余量范围（尺寸检验专用）
    /// </summary>
    public string? LengthAllowanceRange { get; set; }

    // ========== 水压/水下气压专用字段 ==========

    /// <summary>
    /// 压力Mpa（水压/水下气压专用）
    /// </summary>
    public decimal? Pressure { get; set; }

    /// <summary>
    /// 保压时间s（水压/水下气压专用）
    /// </summary>
    public int? HoldTime { get; set; }

    // ========== 其他 ==========

    /// <summary>
    /// 检验备注
    /// </summary>
    public string? Remark { get; set; }

    // ========== 导航属性 ==========

    /// <summary>
    /// 所属生产批次
    /// </summary>
    public ProductionBatch ProductionBatch { get; set; } = null!;
}
