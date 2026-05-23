using System.ComponentModel.DataAnnotations;
using MES.Core.Enums;

namespace MES.Core.DTOs;

// ========== 成品检验 ==========

/// <summary>
/// 成品检验DTO
/// </summary>
public class FinalInspectionDto
{
    public int Id { get; set; }

    /// <summary>检验项目</summary>
    public InspectionItem InspectionItem { get; set; }

    /// <summary>检验日期</summary>
    public DateTime InspectionDate { get; set; }

    /// <summary>生产编号</summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>关联生产批次ID</summary>
    public int ProductionBatchId { get; set; }

    /// <summary>物料名称</summary>
    public string? MaterialName { get; set; }

    /// <summary>挂牌号</summary>
    public string? TagNo { get; set; }

    /// <summary>关联工单号</summary>
    public string? WorkOrderNo { get; set; }

    /// <summary>关联订单号</summary>
    public string? SalesOrderNo { get; set; }

    /// <summary>来料单位</summary>
    public string? SourceUnit { get; set; }

    /// <summary>炉号</summary>
    public string? FurnaceNo { get; set; }

    /// <summary>工厂牌号</summary>
    public string? PlantGrade { get; set; }

    /// <summary>规格</summary>
    public string? Specification { get; set; }

    /// <summary>定尺长度</summary>
    public string? FixedLength { get; set; }

    /// <summary>设备名称</summary>
    public string? EquipmentName { get; set; }

    /// <summary>班次</summary>
    public string? Shift { get; set; }

    /// <summary>操作员</summary>
    public string? Operator { get; set; }

    /// <summary>检验支数</summary>
    public int? Quantity { get; set; }

    /// <summary>检验重量</summary>
    public decimal? Weight { get; set; }

    /// <summary>合格支数</summary>
    public int? QualifiedQuantity { get; set; }

    /// <summary>合格重量</summary>
    public decimal? QualifiedWeight { get; set; }

    /// <summary>合格中让步放行支数</summary>
    public int? QualifiedConcessionQuantity { get; set; }

    /// <summary>让步说明</summary>
    public string? ConcessionRemark { get; set; }

    /// <summary>不合格返整支数</summary>
    public int? DefectReworkQuantity { get; set; }

    /// <summary>不合格入库支数</summary>
    public int? DefectWarehouseQuantity { get; set; }

    /// <summary>不合格报废支数</summary>
    public int? DefectScrapQuantity { get; set; }

    /// <summary>不合格情况描述</summary>
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

    /// <summary>物料名称</summary>
    public string? MaterialName { get; set; }

    /// <summary>挂牌号</summary>
    public string? TagNo { get; set; }

    /// <summary>关联工单号</summary>
    public string? WorkOrderNo { get; set; }

    /// <summary>关联订单号</summary>
    public string? SalesOrderNo { get; set; }

    /// <summary>来料单位</summary>
    public string? SourceUnit { get; set; }

    /// <summary>炉号</summary>
    public string? FurnaceNo { get; set; }

    /// <summary>工厂牌号</summary>
    public string? PlantGrade { get; set; }

    /// <summary>规格</summary>
    public string? Specification { get; set; }

    /// <summary>定尺长度</summary>
    public string? FixedLength { get; set; }
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

    [MaxLength(50)]
    public string? MaterialName { get; set; }
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
    public string? EquipmentName { get; set; }
    [MaxLength(10)]
    public string? Shift { get; set; }
    [MaxLength(50)]
    public string? Operator { get; set; }
    public int? Quantity { get; set; }
    public decimal? Weight { get; set; }
    public int? QualifiedQuantity { get; set; }
    public decimal? QualifiedWeight { get; set; }
    public int? QualifiedConcessionQuantity { get; set; }
    [MaxLength(500)]
    public string? ConcessionRemark { get; set; }
    public int? DefectReworkQuantity { get; set; }
    public int? DefectWarehouseQuantity { get; set; }
    public int? DefectScrapQuantity { get; set; }
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

    [MaxLength(100)]
    public string? EquipmentName { get; set; }
    [MaxLength(10)]
    public string? Shift { get; set; }
    [MaxLength(50)]
    public string? Operator { get; set; }
    public int? Quantity { get; set; }
    public decimal? Weight { get; set; }
    public int? QualifiedQuantity { get; set; }
    public decimal? QualifiedWeight { get; set; }
    public int? QualifiedConcessionQuantity { get; set; }
    [MaxLength(500)]
    public string? ConcessionRemark { get; set; }
    public int? DefectReworkQuantity { get; set; }
    public int? DefectWarehouseQuantity { get; set; }
    public int? DefectScrapQuantity { get; set; }
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
    [MaxLength(500)]
    public string? Remark { get; set; }
}
