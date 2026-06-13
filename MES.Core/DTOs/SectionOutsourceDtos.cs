using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs;

// ========== 工段委外（SectionOutsource）==========

/// <summary>
/// 工段委外DTO（用于列表展示）
/// </summary>
public class SectionOutsourceDto
{
    /// <summary>记录ID</summary>
    public int Id { get; set; }
    /// <summary>关联生产批次ID</summary>
    public int ProductionBatchId { get; set; }
    /// <summary>关联工序组ID</summary>
    public int ProcessGroupId { get; set; }

    /// <summary>批次号（冗余）</summary>
    public string BatchNo { get; set; } = null!;
    /// <summary>工序名称（从ProcessGroup冗余）</summary>
    public string ProcessName { get; set; } = null!;
    /// <summary>制造规格（从ProcessGroup冗余）</summary>
    public string? ManufacturingSpec { get; set; }
    /// <summary>委外工段名称</summary>
    public string SectionName { get; set; } = null!;
    /// <summary>执行序号（来自工序组中该工段的顺序值）</summary>
    public int SequenceNumber { get; set; }
    /// <summary>委外单位</summary>
    public string OutsourceVendor { get; set; } = null!;
    /// <summary>发出日期</summary>
    public DateTime SendOutDate { get; set; }
    /// <summary>发出数量（支数）</summary>
    public int? SendQuantity { get; set; }
    /// <summary>发出重量(kg)</summary>
    public decimal? SendWeight { get; set; }
    /// <summary>状态（PendingRecovery=待回收, Recovered=已回收）</summary>
    public string Status { get; set; } = null!;
    /// <summary>挂牌号</summary>
    public string? TagNo { get; set; }
    /// <summary>工厂牌号</summary>
    public string? PlantGrade { get; set; }
    /// <summary>委外规格</summary>
    public string? OutsourceSpec { get; set; }
    /// <summary>要求收回日期</summary>
    public DateTime? ExpectedReturnDate { get; set; }
    /// <summary>是否紧急</summary>
    public bool IsUrgent { get; set; }
    /// <summary>备注</summary>
    public string? Remark { get; set; }
    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedTime { get; set; }
    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedTime { get; set; }

    // ========== 回收汇总 ==========
    /// <summary>正常回收总支数</summary>
    public int? TotalRecoveredQuantity { get; set; }
    /// <summary>正常回收总重量</summary>
    public decimal? TotalRecoveredWeight { get; set; }
    /// <summary>非正常回收总支数</summary>
    public int? TotalUnprocessedQuantity { get; set; }
    /// <summary>非正常回收总重量</summary>
    public decimal? TotalUnprocessedWeight { get; set; }

    /// <summary>实际回收日期（回收记录中最大的日期）</summary>
    public DateTime? ActualRecoveryDate { get; set; }

    /// <summary>数据来源（SCAN=扫码报工，MANUAL=手动录入）</summary>
    public string? DataSource { get; set; }
}

/// <summary>
/// 创建工段委外请求（使用 BatchNo 替代 ProductionBatchId）
/// </summary>
public class CreateSectionOutsourceRequest
{
    [Required(ErrorMessage = "批次号不能为空")]
    [MaxLength(50)]
    public string BatchNo { get; set; } = string.Empty;

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

    /// <summary>执行序号（0=由系统自动从工序组解析）</summary>
    public int SequenceNumber { get; set; }

    [Required(ErrorMessage = "委外单位不能为空")]
    [MaxLength(100)]
    public string OutsourceVendor { get; set; } = null!;

    [Required(ErrorMessage = "发出日期不能为空")]
    public DateTime SendOutDate { get; set; }

    public int? SendQuantity { get; set; }
    public decimal? SendWeight { get; set; }

    [MaxLength(50)]
    public string? TagNo { get; set; }

    [MaxLength(50)]
    public string? PlantGrade { get; set; }

    [MaxLength(100)]
    public string? OutsourceSpec { get; set; }

    public DateTime? ExpectedReturnDate { get; set; }
    public bool IsUrgent { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }

    /// <summary>
    /// 数据来源（SCAN=扫码报工，MANUAL=手动录入）
    /// </summary>
    [MaxLength(10)]
    public string? DataSource { get; set; }
}

/// <summary>
/// 更新工段委外请求（内联编辑）
/// </summary>
public class UpdateSectionOutsourceRequest
{
    public int? SendQuantity { get; set; }
    public decimal? SendWeight { get; set; }

    [MaxLength(100)]
    public string? OutsourceVendor { get; set; }

    [MaxLength(100)]
    public string? OutsourceSpec { get; set; }

    public DateTime? ExpectedReturnDate { get; set; }
    public bool? IsUrgent { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }
}

// ========== 委外回收（OutsourceRecovery）==========

/// <summary>
/// 委外回收DTO
/// </summary>
public class OutsourceRecoveryDto
{
    public int Id { get; set; }
    public int SectionOutsourceId { get; set; }
    public DateTime RecoveryDate { get; set; }

    /// <summary>正常回收支数</summary>
    public int? RecoveryQuantity { get; set; }
    /// <summary>正常回收重量</summary>
    public decimal? RecoveryWeight { get; set; }
    /// <summary>非正常回收支数</summary>
    public int? UnprocessedQuantity { get; set; }
    /// <summary>非正常回收重量</summary>
    public decimal? UnprocessedWeight { get; set; }
    public string? Remark { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }

    // ========== 冗余字段 ==========
    /// <summary>批次号</summary>
    public string? BatchNo { get; set; }
    /// <summary>委外单位</summary>
    public string? OutsourceVendor { get; set; }
    /// <summary>工序名称</summary>
    public string? ProcessName { get; set; }
    /// <summary>工段名称</summary>
    public string? SectionName { get; set; }

    /// <summary>委外规格</summary>
    public string? OutsourceSpec { get; set; }
    /// <summary>制造规格</summary>
    public string? ManufacturingSpec { get; set; }
    /// <summary>发出支数</summary>
    public int? SendQuantity { get; set; }
    /// <summary>发出重量</summary>
    public decimal? SendWeight { get; set; }
    /// <summary>挂牌号</summary>
    public string? TagNo { get; set; }
    /// <summary>工厂牌号</summary>
    public string? PlantGrade { get; set; }

    /// <summary>数据来源（SCAN=扫码报工，MANUAL=手动录入）</summary>
    public string? DataSource { get; set; }
}

/// <summary>
/// 创建委外回收请求
/// </summary>
public class CreateOutsourceRecoveryRequest
{
    [Required(ErrorMessage = "关联委外记录ID不能为空")]
    public int SectionOutsourceId { get; set; }

    [Required(ErrorMessage = "回收日期不能为空")]
    public DateTime RecoveryDate { get; set; }

    /// <summary>正常回收支数</summary>
    public int? RecoveryQuantity { get; set; }
    /// <summary>正常回收重量</summary>
    public decimal? RecoveryWeight { get; set; }
    /// <summary>非正常回收支数</summary>
    public int? UnprocessedQuantity { get; set; }
    /// <summary>非正常回收重量</summary>
    public decimal? UnprocessedWeight { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }

    /// <summary>
    /// 数据来源（SCAN=扫码报工，MANUAL=手动录入）
    /// </summary>
    [MaxLength(10)]
    public string? DataSource { get; set; }
}

/// <summary>
/// 更新委外回收请求
/// </summary>
public class UpdateOutsourceRecoveryRequest
{
    public DateTime? RecoveryDate { get; set; }
    public int? RecoveryQuantity { get; set; }
    public decimal? RecoveryWeight { get; set; }
    public int? UnprocessedQuantity { get; set; }
    public decimal? UnprocessedWeight { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }
}

// ========== 打印相关 ==========

/// <summary>
/// 工段委外打印已选请求
/// </summary>
public class SectionOutsourcePrintBatchRequest
{
    [Required(ErrorMessage = "请选择要打印的记录")]
    public int[] Ids { get; set; } = Array.Empty<int>();

    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 工段委外打印全部请求
/// </summary>
public class SectionOutsourcePrintAllRequest
{
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; } = true;
    public DateTime? SendOutDateFrom { get; set; }
    public DateTime? SendOutDateTo { get; set; }
    public DateTime? ActualRecoveryDateFrom { get; set; }
    public DateTime? ActualRecoveryDateTo { get; set; }
    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 委外回收打印已选请求
/// </summary>
public class RecoveryPrintBatchRequest
{
    [Required(ErrorMessage = "请选择要打印的记录")]
    public int[] Ids { get; set; } = Array.Empty<int>();

    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 委外回收打印全部请求
/// </summary>
public class RecoveryPrintAllRequest
{
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; } = true;
    public DateTime? RecoveryDateFrom { get; set; }
    public DateTime? RecoveryDateTo { get; set; }
    public List<PrintColumnDef> Columns { get; set; } = new();
}
