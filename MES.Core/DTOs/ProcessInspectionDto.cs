using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs;

// ========== 过程检验 ==========

/// <summary>
/// 过程检验DTO
/// </summary>
public class ProcessInspectionDto
{
    public int Id { get; set; }
    public int ProductionBatchId { get; set; }
    public int ProcessGroupId { get; set; }
    public string ProcessName { get; set; } = null!;
    public string? ManufacturingSpec { get; set; }
    public string SectionName { get; set; } = null!;
    public int SequenceNumber { get; set; }
    public DateTime InspectionDate { get; set; }
    public string? EquipmentName { get; set; }
    public string? Inspector { get; set; }
    public string? Shift { get; set; }
    public int? Quantity { get; set; }
    public decimal? Weight { get; set; }
    public string? InspectionItem { get; set; }
    public int? QualifiedQuantity { get; set; }
    public decimal? QualifiedWeight { get; set; }

    /// <summary>合格中让步放行支数</summary>
    public int? QualifiedConcessionQuantity { get; set; }

    /// <summary>让步说明</summary>
    public string? ConcessionRemark { get; set; }

    public int? DefectReworkQuantity { get; set; }
    public int? DefectWarehouseQuantity { get; set; }
    public int? DefectScrapQuantity { get; set; }
    public string? DefectDescription { get; set; }
    public string? SourceUnit { get; set; }
    public string? TagNo { get; set; }
    public string? PlantGrade { get; set; }
    public string? Remark { get; set; }

    /// <summary>批次号（冗余，用于跨批次列表展示）</summary>
    public string? BatchNo { get; set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedTime { get; set; }
}

/// <summary>
/// 创建过程检验请求
/// </summary>
public class CreateProcessInspectionRequest
{
    /// <summary>
    /// 批次号，由前端文本输入，提交时服务端自动解析为ProductionBatchId
    /// </summary>
    [Required(ErrorMessage = "批次号不能为空")]
    [MaxLength(50)]
    public string BatchNo { get; set; } = string.Empty;

    /// <summary>
    /// 生产批次ID（内部使用，可由BatchNo自动解析）
    /// </summary>
    public int ProductionBatchId { get; set; }

    /// <summary>
    /// 工序组ID（可为空，服务端自动根据批次号+工序名称解析）
    /// </summary>
    public int? ProcessGroupId { get; set; }

    [Required(ErrorMessage = "工序名称不能为空")]
    [MaxLength(50)]
    public string ProcessName { get; set; } = null!;

    [Required(ErrorMessage = "制造规格不能为空")]
    [MaxLength(100)]
    public string ManufacturingSpec { get; set; } = null!;

    [Required(ErrorMessage = "工段名称不能为空")]
    [MaxLength(50)]
    public string SectionName { get; set; } = null!;

    /// <summary>
    /// 执行序号（传0则由服务端自动从ProcessGroup解析）
    /// </summary>
    public int SequenceNumber { get; set; }

    [Required(ErrorMessage = "检验日期不能为空")]
    public DateTime InspectionDate { get; set; }

    [MaxLength(100)]
    public string? EquipmentName { get; set; }

    [MaxLength(50)]
    public string? Inspector { get; set; }

    [MaxLength(10)]
    public string? Shift { get; set; }

    public int? Quantity { get; set; }
    public decimal? Weight { get; set; }

    [MaxLength(100)]
    public string? InspectionItem { get; set; }
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

    [MaxLength(200)]
    public string? SourceUnit { get; set; }

    [MaxLength(50)]
    public string? TagNo { get; set; }

    [MaxLength(50)]
    public string? PlantGrade { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }
}

/// <summary>
/// 更新过程检验请求（内联编辑用）
/// </summary>
public class UpdateProcessInspectionRequest
{
    [Required(ErrorMessage = "检验日期不能为空")]
    public DateTime InspectionDate { get; set; }

    [MaxLength(100)]
    public string? EquipmentName { get; set; }

    [MaxLength(50)]
    public string? Inspector { get; set; }

    [MaxLength(10)]
    public string? Shift { get; set; }

    public int? Quantity { get; set; }
    public decimal? Weight { get; set; }

    [MaxLength(100)]
    public string? InspectionItem { get; set; }
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

    [MaxLength(200)]
    public string? SourceUnit { get; set; }

    [MaxLength(50)]
    public string? TagNo { get; set; }

    [MaxLength(50)]
    public string? PlantGrade { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }
}
