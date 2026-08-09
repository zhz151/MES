using System.ComponentModel.DataAnnotations;
using MES.Core.Enums;
using MES.Data.Entities.Batch;

namespace MES.Data.Entities.Quality;

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

    // ========== 成检类型 ==========

    /// <summary>
    /// 成检类型（PreInspection=预成检，FormalInspection=正式成检）
    /// </summary>
    public string? InspectionType { get; set; }

    // ========== 执行信息 ==========

    /// <summary>
    /// 设备名称
    /// </summary>
    public string? EquipmentName { get; set; }

    /// <summary>
    /// 班次
    /// </summary>
    public ShiftType? Shift { get; set; }

    /// <summary>
    /// 操作员
    /// </summary>
    public string? Operator { get; set; }

    // ========== 长度信息 ==========

    /// <summary>
    /// 定尺长度（批次长度状态=定尺时填写）
    /// </summary>
    [MaxLength(50)]
    public string? FixedLength { get; set; }

    /// <summary>
    /// 定尺切割长度匹配标识（存枚举名 FullMatch=完全匹配/MainNoMatch=主号匹配；null=不适用，仅正式成检计算）
    /// </summary>
    [MaxLength(20)]
    public string? CutLengthMatchType { get; set; }

    /// <summary>
    /// 非定尺长度范围（批次长度状态&lt;&gt;定尺时填写）
    /// </summary>
    [MaxLength(100)]
    public string? NonFixedLengthRange { get; set; }

    // ========== 数量/重量 ==========

    /// <summary>
    /// 检验支数
    /// </summary>
    public int? Quantity { get; set; }

    /// <summary>
    /// 理论检验重量(kg，整数)
    /// </summary>
    public int? Weight { get; set; }

    // ========== 检验结果 ==========

    /// <summary>
    /// 合格支数
    /// </summary>
    public int? QualifiedQuantity { get; set; }

    /// <summary>
    /// 理论合格重量(kg，整数)
    /// </summary>
    public int? QualifiedWeight { get; set; }

    /// <summary>
    /// 合格中让步放行支数
    /// </summary>
    public int? QualifiedConcessionQuantity { get; set; }

    /// <summary>
    /// 让步说明
    /// </summary>
    public string? ConcessionRemark { get; set; }

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

    // ========== 不合格处理重量 ==========

    /// <summary>
    /// 次品返整重量(kg，整数)
    /// </summary>
    public int? DefectReworkWeight { get; set; }

    /// <summary>
    /// 次品入库重量(kg，整数)
    /// </summary>
    public int? DefectWarehouseWeight { get; set; }

    /// <summary>
    /// 次品报废重量(kg，整数)
    /// </summary>
    public int? DefectScrapWeight { get; set; }

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

    // ========== 涡流/超声波探伤专用字段（仅InspectionItem=EddyCurrent/Ultrasonic时有效） ==========

    /// <summary>
    /// 资格等级
    /// </summary>
    public string? QualificationLevel { get; set; }

    /// <summary>
    /// 检验标准
    /// </summary>
    public string? InspectionStandard { get; set; }

    /// <summary>
    /// 检验等级
    /// </summary>
    public string? InspectionGrade { get; set; }

    /// <summary>
    /// 检验仪器型号
    /// </summary>
    public string? InstrumentModel { get; set; }

    /// <summary>
    /// 检验方式
    /// </summary>
    public string? NdtMethod { get; set; }

    /// <summary>
    /// 标样尺寸
    /// </summary>
    public string? StandardSampleSize { get; set; }

    /// <summary>
    /// 标样人工缺陷
    /// </summary>
    public string? StandardSampleDefect { get; set; }

    /// <summary>
    /// 探头类型
    /// </summary>
    public string? ProbeType { get; set; }

    /// <summary>
    /// 耦合剂
    /// </summary>
    public string? Couplant { get; set; }

    /// <summary>
    /// 检测设备校准频率
    /// </summary>
    public string? CalibrationFrequency { get; set; }

    /// <summary>
    /// 检测频率
    /// </summary>
    public string? DetectionFrequency { get; set; }

    /// <summary>
    /// 检测灵敏度
    /// </summary>
    public string? DetectionSensitivity { get; set; }

    /// <summary>
    /// 检测相位
    /// </summary>
    public string? DetectionPhase { get; set; }

    /// <summary>
    /// 检测速度
    /// </summary>
    public string? DetectionSpeed { get; set; }

    // ========== 其他 ==========

    /// <summary>
    /// 检验备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 数据来源（SCAN=扫码报工，MANUAL=手动录入），默认 MANUAL
    /// </summary>
    public string? DataSource { get; set; }

    // ========== 导航属性 ==========

    /// <summary>
    /// 所属生产批次
    /// </summary>
    public ProductionBatch ProductionBatch { get; set; } = null!;
}
