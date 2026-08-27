using System.ComponentModel.DataAnnotations;
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Quality;

// ========== 成品检验 ==========

/// <summary>
/// 成品检验DTO
/// </summary>
public class FinalInspectionDto
{
    public int Id { get; set; }

    /// <summary>检验项目</summary>
    public InspectionItem InspectionItem { get; set; }
    public string InspectionItemDisplay => EnumHelper.GetDisplayName(InspectionItem);

    /// <summary>检验日期</summary>
    public DateTime InspectionDate { get; set; }

    /// <summary>生产编号</summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>关联生产批次ID</summary>
    public int ProductionBatchId { get; set; }

    /// <summary>制造物品</summary>
    public MaterialType? ManufacturingItem { get; set; }
    public string? ManufacturingItemDisplay => ManufacturingItem.HasValue ? EnumHelper.GetDisplayName(ManufacturingItem.Value) : null;

    /// <summary>挂牌号</summary>
    public string? TagNo { get; set; }

    /// <summary>关联工单号</summary>
    public string? WorkOrderNo { get; set; }

    /// <summary>关联订单号</summary>
    public string? SalesOrderNo { get; set; }

    /// <summary>主号（从批次导航属性投影）</summary>
    public string? ProductionMainNo { get; set; }

    /// <summary>来料单位</summary>
    public string? SourceUnit { get; set; }

    /// <summary>炉号</summary>
    public string? FurnaceNo { get; set; }

    /// <summary>工厂牌号</summary>
    public string? PlantGrade { get; set; }

    /// <summary>规格</summary>
    public string? Specification { get; set; }

    /// <summary>定尺长度（批次长度状态=定尺时填写）</summary>
    public string? FixedLength { get; set; }

    /// <summary>非定尺长度范围（批次长度状态&lt;&gt;定尺时填写）</summary>
    public string? NonFixedLengthRange { get; set; }

    /// <summary>定尺切割长度匹配标识（完全匹配/主号匹配；null=不适用，仅正式成检计算）</summary>
    public CutLengthMatchType? CutLengthMatchType { get; set; }
    public string? CutLengthMatchTypeDisplay => CutLengthMatchHelper.GetText(CutLengthMatchType);

    /// <summary>生产类型</summary>
    public ProductionType? ProductionType { get; set; }

    /// <summary>业务员</summary>
    public string? Salesman { get; set; }

    /// <summary>长度状态</summary>
    public LengthStatus? LengthStatus { get; set; }

    /// <summary>交货状态</summary>
    public DeliveryState? DeliveryState { get; set; }

    /// <summary>制造状态（来自关联生产批次）</summary>
    public DeliveryState? ManufacturingStatus { get; set; }

    /// <summary>
    /// 制造状态显示（仅正式成检有效；非正式成检/空统一显示 "-"）
    /// </summary>
    public string? ManufacturingStatusDisplay => !IsFormalInspection
        ? "-"
        : ManufacturingStatus.HasValue ? EnumHelper.GetDisplayName(ManufacturingStatus.Value) : "-";

    /// <summary>最终用户（来自关联生产批次）</summary>
    public string? EndCustomer { get; set; }

    /// <summary>生产支数（来自关联生产批次；需切割→批次 CutQuantity，免切割→批次理论成品支数）</summary>
    public int? ProductionCutQuantity { get; set; }

    /// <summary>生产重量（来自关联生产批次理论成品重量）</summary>
    public decimal? ProductionWeight { get; set; }

    /// <summary>
    /// 是否正式成检（成检类型==FormalInspection；null/其他/预成检均视为非正式成检）
    /// 仅正式成检时「制造状态/是否交付态」才有效，否则统一显示 "-"
    /// </summary>
    public bool IsFormalInspection =>
        InspectionType == MES.Core.Enums.InspectionType.FormalInspection;

    /// <summary>
    /// 是否交付态显示（制造状态==交货状态为"是"；仅正式成检有效，非正式成检显示 "-"）
    /// </summary>
    public string? IsDeliveryStatusDisplay => !IsFormalInspection
        ? "-"
        : ManufacturingStatus.HasValue && DeliveryState.HasValue
          && ManufacturingStatus.Value == DeliveryState.Value ? "是" : "否";

    /// <summary>是否交付态（别名，供打印反射/列取值使用，与显示口径一致）</summary>
    public string? IsDeliveryStatus => IsDeliveryStatusDisplay;

    /// <summary>设备名称</summary>
    public string? EquipmentName { get; set; }

    /// <summary>班次</summary>
    public ShiftType? Shift { get; set; }
    public string? ShiftDisplay => Shift.HasValue ? EnumHelper.GetDisplayName(Shift.Value) : null;

    /// <summary>操作员</summary>
    public string? Operator { get; set; }

    /// <summary>检验支数</summary>
    public int? Quantity { get; set; }

    /// <summary>成检类型</summary>
    public InspectionType? InspectionType { get; set; }
    public string? InspectionTypeDisplay => InspectionType.HasValue ? EnumHelper.GetDisplayName(InspectionType.Value) : null;

    /// <summary>理论检验重量</summary>
    public int? Weight { get; set; }

    /// <summary>合格支数</summary>
    public int? QualifiedQuantity { get; set; }

    /// <summary>理论合格重量</summary>
    public int? QualifiedWeight { get; set; }

    /// <summary>合格中让步放行支数</summary>
    public int? QualifiedConcessionQuantity { get; set; }

    /// <summary>让步说明</summary>
    public string? ConcessionRemark { get; set; }

    /// <summary>次品返整支数</summary>
    public int? DefectReworkQuantity { get; set; }

    /// <summary>次品入库支数</summary>
    public int? DefectWarehouseQuantity { get; set; }

    /// <summary>次品报废支数</summary>
    public int? DefectScrapQuantity { get; set; }

    /// <summary>次品返整重量</summary>
    public int? DefectReworkWeight { get; set; }

    /// <summary>次品入库重量</summary>
    public int? DefectWarehouseWeight { get; set; }

    /// <summary>次品报废重量</summary>
    public int? DefectScrapWeight { get; set; }

    /// <summary>次品情况描述</summary>
    public string? DefectDescription { get; set; }

    /// <summary>外径范围（尺寸检验专用）</summary>
    public string? OuterDiameterRange { get; set; }

    /// <summary>壁厚范围（尺寸检验专用）</summary>
    public string? WallThicknessRange { get; set; }

    /// <summary>长度余量范围（尺寸检验专用）</summary>
    public string? LengthAllowanceRange { get; set; }

    /// <summary>压力Mpa（水压/水下气压专用）</summary>
    public decimal? Pressure { get; set; }

    /// <summary>保压时间s（水压/水下气压专用）</summary>
    public int? HoldTime { get; set; }

    // ========== 涡流/超声波探伤专用 ==========

    /// <summary>资格等级</summary>
    public string? QualificationLevel { get; set; }
    /// <summary>检验标准</summary>
    public string? InspectionStandard { get; set; }
    /// <summary>检验等级</summary>
    public string? InspectionGrade { get; set; }
    /// <summary>检验仪器型号</summary>
    public string? InstrumentModel { get; set; }
    /// <summary>检验方式</summary>
    public string? NdtMethod { get; set; }
    /// <summary>标样尺寸</summary>
    public string? StandardSampleSize { get; set; }
    /// <summary>标样人工缺陷</summary>
    public string? StandardSampleDefect { get; set; }
    /// <summary>探头类型</summary>
    public string? ProbeType { get; set; }
    /// <summary>耦合剂</summary>
    public string? Couplant { get; set; }
    /// <summary>检测设备校准频率</summary>
    public string? CalibrationFrequency { get; set; }
    /// <summary>检测频率</summary>
    public string? DetectionFrequency { get; set; }
    /// <summary>检测灵敏度</summary>
    public string? DetectionSensitivity { get; set; }
    /// <summary>检测相位</summary>
    public string? DetectionPhase { get; set; }
    /// <summary>检测速度</summary>
    public string? DetectionSpeed { get; set; }

    /// <summary>检验备注</summary>
    public string? Remark { get; set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedTime { get; set; }

    /// <summary>数据来源（SCAN=扫码报工，MANUAL=手动录入）</summary>
    public string? DataSource { get; set; }
}

/// <summary>
/// 批次调取结果DTO（用于新建页自动填充）
/// </summary>
public class BatchLookupResultDto
{
    /// <summary>生产批次ID</summary>
    public int ProductionBatchId { get; set; }

    /// <summary>制造物品</summary>
    public string? ManufacturingItem { get; set; }

    /// <summary>挂牌号</summary>
    public string? TagNo { get; set; }

    /// <summary>关联工单号</summary>
    public string? WorkOrderNo { get; set; }

    /// <summary>关联订单号</summary>
    public string? SalesOrderNo { get; set; }

    /// <summary>主号（从批次导航属性投影）</summary>
    public string? ProductionMainNo { get; set; }

    /// <summary>来料单位</summary>
    public string? SourceUnit { get; set; }

    /// <summary>炉号</summary>
    public string? FurnaceNo { get; set; }

    /// <summary>工厂牌号</summary>
    public string? PlantGrade { get; set; }

    /// <summary>规格</summary>
    public string? Specification { get; set; }

    /// <summary>定尺长度（批次长度状态=定尺时填写）</summary>
    public string? FixedLength { get; set; }

    /// <summary>非定尺长度范围（批次长度状态&lt;&gt;定尺时填写）</summary>
    public string? NonFixedLengthRange { get; set; }

    /// <summary>生产类型</summary>
    public ProductionType? ProductionType { get; set; }

    /// <summary>业务员</summary>
    public string? Salesman { get; set; }

    /// <summary>长度状态</summary>
    public LengthStatus? LengthStatus { get; set; }

    /// <summary>交货状态</summary>
    public DeliveryState? DeliveryState { get; set; }

    /// <summary>制造状态</summary>
    public DeliveryState? ManufacturingStatus { get; set; }

    /// <summary>最终用户</summary>
    public string? EndCustomer { get; set; }

    /// <summary>生产支数（需切割→批次成切支数，免切割→理论成品支数）</summary>
    public int? ProductionCutQuantity { get; set; }

    /// <summary>生产重量（理论成品重量）</summary>
    public decimal? ProductionWeight { get; set; }

    /// <summary>成检类型（继承自到料检验，无则默认正式成检）</summary>
    public InspectionType? InspectionType { get; set; }

    /// <summary>单支重（定尺=总重/总支，非定尺=理论单支重；用于前端重量自动回填）</summary>
    public decimal? UnitWeight { get; set; }
}

/// <summary>
/// 创建成品检验请求
/// </summary>
public class CreateFinalInspectionRequest
{
    [Required(ErrorMessage = "检验项目不能为空")]
    public InspectionItem InspectionItem { get; set; }

    [Required(ErrorMessage = "检验日期不能为空")]
    public DateTime InspectionDate { get; set; }

    [Required(ErrorMessage = "生产编号不能为空")]
    [MaxLength(50)]
    public string BatchNo { get; set; } = string.Empty;

    /// <summary>生产批次ID（可由BatchNo自动解析）</summary>
    public int ProductionBatchId { get; set; }

    /// <summary>
    /// 成检类型（PreInspection=预成检，FormalInspection=正式成检）
    /// 不传时服务端按「优先正式成检」自动判定；传了则以传入值为准
    /// </summary>
    public InspectionType? InspectionType { get; set; }

    public MaterialType? ManufacturingItem { get; set; }
    [MaxLength(50)]
    public string? TagNo { get; set; }
    [MaxLength(50)]
    public string? WorkOrderNo { get; set; }
    [MaxLength(50)]
    public string? SalesOrderNo { get; set; }
    [MaxLength(200)]
    public string? SourceUnit { get; set; }
    [MaxLength(50)]
    public string? FurnaceNo { get; set; }
    [MaxLength(50)]
    public string? PlantGrade { get; set; }
    [MaxLength(100)]
    public string? Specification { get; set; }
    [MaxLength(50)]
    public string? FixedLength { get; set; }
    [MaxLength(100)]
    public string? NonFixedLengthRange { get; set; }
    public ProductionType? ProductionType { get; set; }
    [MaxLength(50)]
    public string? Salesman { get; set; }
    public LengthStatus? LengthStatus { get; set; }
    [MaxLength(100)]
    public string? EquipmentName { get; set; }
    public ShiftType? Shift { get; set; }
    [MaxLength(50)]
    public string? Operator { get; set; }
    public int? Quantity { get; set; }
    public int? Weight { get; set; }
    public int? QualifiedQuantity { get; set; }
    public int? QualifiedWeight { get; set; }
    public int? QualifiedConcessionQuantity { get; set; }
    [MaxLength(500)]
    public string? ConcessionRemark { get; set; }
    public int? DefectReworkQuantity { get; set; }
    public int? DefectWarehouseQuantity { get; set; }
    public int? DefectScrapQuantity { get; set; }
    public int? DefectReworkWeight { get; set; }
    public int? DefectWarehouseWeight { get; set; }
    public int? DefectScrapWeight { get; set; }
    [MaxLength(500)]
    public string? DefectDescription { get; set; }
    [MaxLength(100)]
    public string? OuterDiameterRange { get; set; }
    [MaxLength(100)]
    public string? WallThicknessRange { get; set; }
    [MaxLength(100)]
    public string? LengthAllowanceRange { get; set; }
    public decimal? Pressure { get; set; }
    public int? HoldTime { get; set; }

    // ========== 涡流/超声波探伤专用 ==========

    [MaxLength(100)]
    public string? QualificationLevel { get; set; }
    [MaxLength(100)]
    public string? InspectionStandard { get; set; }
    [MaxLength(100)]
    public string? InspectionGrade { get; set; }
    [MaxLength(100)]
    public string? InstrumentModel { get; set; }
    [MaxLength(100)]
    public string? NdtMethod { get; set; }
    [MaxLength(100)]
    public string? StandardSampleSize { get; set; }
    [MaxLength(100)]
    public string? StandardSampleDefect { get; set; }
    [MaxLength(100)]
    public string? ProbeType { get; set; }
    [MaxLength(100)]
    public string? Couplant { get; set; }
    [MaxLength(100)]
    public string? CalibrationFrequency { get; set; }
    [MaxLength(100)]
    public string? DetectionFrequency { get; set; }
    [MaxLength(100)]
    public string? DetectionSensitivity { get; set; }
    [MaxLength(100)]
    public string? DetectionPhase { get; set; }
    [MaxLength(100)]
    public string? DetectionSpeed { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }

    /// <summary>
    /// 数据来源（SCAN=扫码报工，MANUAL=手动录入）
    /// </summary>
    [MaxLength(10)]
    public string? DataSource { get; set; }
}

/// <summary>
/// 更新成品检验请求（内联编辑用）
/// </summary>
public class UpdateFinalInspectionRequest
{
    [Required(ErrorMessage = "检验日期不能为空")]
    public DateTime InspectionDate { get; set; }

    /// <summary>
    /// 成检类型（PreInspection=预成检，FormalInspection=正式成检）
    /// 传了则校验（必须在批次成检到料类型集合内）并更新；不传保留原值
    /// </summary>
    public InspectionType? InspectionType { get; set; }

    [MaxLength(50)]
    public string? FixedLength { get; set; }

    [MaxLength(100)]
    public string? NonFixedLengthRange { get; set; }

    [MaxLength(100)]
    public string? EquipmentName { get; set; }
    public ShiftType? Shift { get; set; }
    [MaxLength(50)]
    public string? Operator { get; set; }
    public int? Quantity { get; set; }
    public int? Weight { get; set; }
    public int? QualifiedQuantity { get; set; }
    public int? QualifiedWeight { get; set; }
    public int? QualifiedConcessionQuantity { get; set; }
    [MaxLength(500)]
    public string? ConcessionRemark { get; set; }
    public int? DefectReworkQuantity { get; set; }
    public int? DefectWarehouseQuantity { get; set; }
    public int? DefectScrapQuantity { get; set; }
    public int? DefectReworkWeight { get; set; }
    public int? DefectWarehouseWeight { get; set; }
    public int? DefectScrapWeight { get; set; }
    [MaxLength(500)]
    public string? DefectDescription { get; set; }
    [MaxLength(100)]
    public string? OuterDiameterRange { get; set; }
    [MaxLength(100)]
    public string? WallThicknessRange { get; set; }
    [MaxLength(100)]
    public string? LengthAllowanceRange { get; set; }
    public decimal? Pressure { get; set; }
    public int? HoldTime { get; set; }

    // ========== 涡流/超声波探伤专用 ==========

    [MaxLength(100)]
    public string? QualificationLevel { get; set; }
    [MaxLength(100)]
    public string? InspectionStandard { get; set; }
    [MaxLength(100)]
    public string? InspectionGrade { get; set; }
    [MaxLength(100)]
    public string? InstrumentModel { get; set; }
    [MaxLength(100)]
    public string? NdtMethod { get; set; }
    [MaxLength(100)]
    public string? StandardSampleSize { get; set; }
    [MaxLength(100)]
    public string? StandardSampleDefect { get; set; }
    [MaxLength(100)]
    public string? ProbeType { get; set; }
    [MaxLength(100)]
    public string? Couplant { get; set; }
    [MaxLength(100)]
    public string? CalibrationFrequency { get; set; }
    [MaxLength(100)]
    public string? DetectionFrequency { get; set; }
    [MaxLength(100)]
    public string? DetectionSensitivity { get; set; }
    [MaxLength(100)]
    public string? DetectionPhase { get; set; }
    [MaxLength(100)]
    public string? DetectionSpeed { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }
}

/// <summary>
/// 成品检验健康汇总DTO（按当前筛选条件统计「成检类型与成检到料不符」的生产编号）
/// </summary>
public class FinalInspectionHealthSummaryDto
{
    /// <summary>筛选后的成品检验记录总数</summary>
    public int TotalCount { get; set; }

    /// <summary>成检类型疑问：批次有成检到料，但记录成检类型不在到料类型集合内</summary>
    public List<string> InspectionTypeMismatchBatchNos { get; set; } = new();

    /// <summary>无成检到料：批次完全无到料记录（本不该存在成品检验）</summary>
    public List<string> NoMaterialCheckBatchNos { get; set; } = new();

    public int InspectionTypeMismatchCount => InspectionTypeMismatchBatchNos.Count;
    public int NoMaterialCheckCount => NoMaterialCheckBatchNos.Count;
    public int NormalCount => TotalCount - InspectionTypeMismatchCount - NoMaterialCheckCount;
    public int IssueCount => InspectionTypeMismatchCount + NoMaterialCheckCount;
}
