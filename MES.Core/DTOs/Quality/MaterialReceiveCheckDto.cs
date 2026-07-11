using System.ComponentModel.DataAnnotations;

using MES.Core.DTOs.Shared;
namespace MES.Core.DTOs.Quality;

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
    public decimal? ProductionWeight { get; set; }

    // ========== 状态 ==========
    public bool IsForceCompleted { get; set; }

    // ========== 批次冗余字段 ==========
    public string? LengthStatus { get; set; }

    // ========== 工单冗余字段 ==========
    public string? Salesman { get; set; }
    public string? DeliveryState { get; set; }

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
