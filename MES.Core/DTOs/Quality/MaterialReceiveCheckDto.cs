using System.ComponentModel.DataAnnotations;

using MES.Core.DTOs.Shared;
using MES.Core.Enums;
using MES.Core.Helpers;
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
    public ShiftType? Shift { get; set; }
    public string? ShiftDisplay => Shift.HasValue ? EnumHelper.GetDisplayName(Shift.Value) : null;
    public string? Checker { get; set; }
    public string? Remark { get; set; }
    public string? DataSource { get; set; }

    // ========== 批次冗余字段 ==========
    public string? BatchNo { get; set; }
    public MaterialType? ManufacturingItem { get; set; }
    public string? ManufacturingItemDisplay => ManufacturingItem.HasValue ? EnumHelper.GetDisplayName(ManufacturingItem.Value) : null;
    public string? TagNo { get; set; }
    public string? WorkOrderNo { get; set; }
    public string? SalesOrderNo { get; set; }
    public string? SourceUnit { get; set; }
    public string? FurnaceNo { get; set; }
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
    public ProductionType? ProductionType { get; set; }
    public string? ProductionTypeDisplay => ProductionType.HasValue ? EnumHelper.GetDisplayName(ProductionType.Value) : null;

    // ========== 工序关联 ==========
    public int ProcessGroupId { get; set; }
    public string ProcessName { get; set; } = "检验";
    public int SequenceNumber { get; set; }

    // ========== 状态 ==========
    public bool IsForceCompleted { get; set; }

    /// <summary>是否是批次中的最后一个工序组（交货状态仅最后工序组有效）</summary>
    public bool IsLastProcessGroup { get; set; }

    /// <summary>成检类型</summary>
    public string? InspectionType { get; set; }
    public string? InspectionTypeDisplay => !string.IsNullOrEmpty(InspectionType) && EnumHelper.TryParse<InspectionType>(InspectionType) is { } it ? EnumHelper.GetDisplayName(it) : null;

    // ========== 批次冗余字段 ==========
    public LengthStatus? LengthStatus { get; set; }
    public string? LengthStatusDisplay => LengthStatus.HasValue ? EnumHelper.GetDisplayName(LengthStatus.Value) : null;

    // ========== 工单冗余字段 ==========
    public string? Salesman { get; set; }
    public DeliveryState? DeliveryState { get; set; }
    public string? DeliveryStateDisplay => DeliveryState.HasValue ? EnumHelper.GetDisplayName(DeliveryState.Value) : null;

    /// <summary>制造状态（批次执行的实际制造状态，与交货状态同枚举）</summary>
    public string? ManufacturingStatus { get; set; }
    public string? ManufacturingStatusDisplay => !string.IsNullOrEmpty(ManufacturingStatus) && EnumHelper.TryParse<DeliveryState>(ManufacturingStatus) is { } ms ? EnumHelper.GetDisplayName(ms) : null;

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
    public string? Filters { get; set; }
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

    public ShiftType? Shift { get; set; }

    [MaxLength(50)]
    public string? Checker { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }

    /// <summary>
    /// 数据来源（SCAN=扫码报工，MANUAL=手动录入）
    /// </summary>
    [MaxLength(10)]
    public string? DataSource { get; set; }

    // ========== 工序关联 ==========

    /// <summary>
    /// 所属工序组ID（不传则服务端按规格匹配自动查找）
    /// </summary>
    public int ProcessGroupId { get; set; }

    /// <summary>
    /// 工序名称（不传则服务端从ProcessGroup获取）
    /// </summary>
    [MaxLength(50)]
    public string? ProcessName { get; set; }

    /// <summary>
    /// 执行序号（不传则服务端从ProcessGroup获取）
    /// </summary>
    public int? SequenceNumber { get; set; }
}

/// <summary>
/// 更新成检到料请求（内联编辑用）
/// </summary>
public class UpdateMaterialReceiveCheckRequest
{
    [Required(ErrorMessage = "到料日期不能为空")]
    public DateTime ReceiveDate { get; set; }

    public ShiftType? Shift { get; set; }

    [MaxLength(50)]
    public string? Checker { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }

    /// <summary>强制完成</summary>
    public bool? IsForceCompleted { get; set; }
}
