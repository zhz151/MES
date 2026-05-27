using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs;

// ========== 内部生产记录 ==========

/// <summary>
/// 内部生产记录DTO
/// </summary>
public class ProductionRecordDto
{
    public int Id { get; set; }
    public int ProductionBatchId { get; set; }
    public int ProcessGroupId { get; set; }
    public string ProcessName { get; set; } = null!;
    public string? ManufacturingSpec { get; set; }
    public string SectionName { get; set; } = null!;
    public int SequenceNumber { get; set; }
    public DateTime ExecDate { get; set; }
    public string? EquipmentName { get; set; }
    public string? Operator { get; set; }
    public string? Shift { get; set; }
    public int? Quantity { get; set; }
    public decimal? Weight { get; set; }
    public bool IsFinished { get; set; }
    public decimal? CuttingMultiple { get; set; }
    public decimal? FinishedCutLength { get; set; }
    public int? PostCutQuantity { get; set; }
    public string? TagNo { get; set; }
    public string? PlantGrade { get; set; }
    public string? Remark { get; set; }

    /// <summary>数据来源（SCAN=扫码报工，MANUAL=手动录入）</summary>
    public string? DataSource { get; set; }

    /// <summary>批次号（冗余，用于跨批次列表展示）</summary>
    public string? BatchNo { get; set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedTime { get; set; }
}

/// <summary>
/// 创建内部生产记录请求
/// </summary>
public class CreateProductionRecordRequest
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

    [Required(ErrorMessage = "执行日期不能为空")]
    public DateTime ExecDate { get; set; }

    [MaxLength(100)]
    public string? EquipmentName { get; set; }

    [MaxLength(50)]
    public string? Operator { get; set; }

    [MaxLength(10)]
    public string? Shift { get; set; }

    public int? Quantity { get; set; }
    public decimal? Weight { get; set; }
    public bool IsFinished { get; set; }
    public decimal? CuttingMultiple { get; set; }
    public decimal? FinishedCutLength { get; set; }
    public int? PostCutQuantity { get; set; }

    [MaxLength(50)]
    public string? TagNo { get; set; }

    [MaxLength(50)]
    public string? PlantGrade { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }

    /// <summary>
    /// 数据来源（SCAN=扫码报工，MANUAL=手动录入）
    /// </summary>
    [MaxLength(10)]
    public string? DataSource { get; set; }
}

/// <summary>
    /// 更新生产记录请求（内联编辑用）
    /// </summary>
public class UpdateProductionRecordRequest
{
    [Required(ErrorMessage = "执行日期不能为空")]
    public DateTime ExecDate { get; set; }

    [MaxLength(100)]
    public string? EquipmentName { get; set; }

    [MaxLength(50)]
    public string? Operator { get; set; }

    [MaxLength(10)]
    public string? Shift { get; set; }

    public int? Quantity { get; set; }
    public decimal? Weight { get; set; }
    public bool IsFinished { get; set; }
    public decimal? CuttingMultiple { get; set; }
    public decimal? FinishedCutLength { get; set; }
    public int? PostCutQuantity { get; set; }

    [MaxLength(50)]
    public string? TagNo { get; set; }

    [MaxLength(50)]
    public string? PlantGrade { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }
}

/// <summary>
/// 生产记录打印请求（批量）
/// </summary>
public class ProductionRecordPrintBatchRequest
{
    public int[] Ids { get; set; } = Array.Empty<int>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 生产记录打印请求（全部）
/// </summary>
public class ProductionRecordPrintAllRequest
{
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; }
    public DateTime? ExecDateFrom { get; set; }
    public DateTime? ExecDateTo { get; set; }
    public List<PrintColumnDef> Columns { get; set; } = new();
}

// ========== 检验到料 ==========

/// <summary>
/// 成检到料DTO
/// </summary>
public class MaterialReceiveCheckDto
{
    public int Id { get; set; }
    public int ProductionBatchId { get; set; }
    public DateTime ReceiveDate { get; set; }
    public string? Shift { get; set; }
    public string? Checker { get; set; }
    public string? Remark { get; set; }
    public string? DataSource { get; set; }

    // ========== 批次冗余字段 ==========
    public string? BatchNo { get; set; }
    public string? ManufacturingItem { get; set; }
    public string? TagNo { get; set; }
    public string? WorkOrderNo { get; set; }
    public string? SalesOrderNo { get; set; }
    public string? SourceUnit { get; set; }
    public string? FurnaceNo { get; set; }
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
    public string? ProductionType { get; set; }

    // ========== 汇总计算字段 ==========
    public int ProductionCutQuantity { get; set; }

    // ========== 状态 ==========
    public bool IsForceCompleted { get; set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedTime { get; set; }
}

/// <summary>
/// 检验到料打印请求（批量）
/// </summary>
public class MaterialCheckPrintBatchRequest
{
    public int[] Ids { get; set; } = Array.Empty<int>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 检验到料打印请求（全部）
/// </summary>
public class MaterialCheckPrintAllRequest
{
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; }
    public DateTime? ReceiveDateFrom { get; set; }
    public DateTime? ReceiveDateTo { get; set; }
    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 创建成检到料请求
/// </summary>
public class CreateMaterialReceiveCheckRequest
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

    [Required(ErrorMessage = "到料日期不能为空")]
    public DateTime ReceiveDate { get; set; }

    [MaxLength(10)]
    public string? Shift { get; set; }

    [MaxLength(50)]
    public string? Checker { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }

    /// <summary>
    /// 数据来源（SCAN=扫码报工，MANUAL=手动录入）
    /// </summary>
    [MaxLength(10)]
    public string? DataSource { get; set; }
}

/// <summary>
/// 更新成检到料请求（内联编辑用）
/// </summary>
public class UpdateMaterialReceiveCheckRequest
{
    [Required(ErrorMessage = "到料日期不能为空")]
    public DateTime ReceiveDate { get; set; }

    [MaxLength(10)]
    public string? Shift { get; set; }

    [MaxLength(50)]
    public string? Checker { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }

    /// <summary>强制完成</summary>
    public bool? IsForceCompleted { get; set; }
}
