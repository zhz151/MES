using System.ComponentModel.DataAnnotations;

using MES.Core.DTOs.Shared;
using MES.Core.Enums;
using MES.Core.Helpers;
namespace MES.Core.DTOs.Batch;

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
    public ShiftType? Shift { get; set; }
    public int? Quantity { get; set; }
    public decimal? Weight { get; set; }

    /// <summary>固溶温度(℃)，仅固溶工段使用</summary>
    public decimal? SolutionTemperature { get; set; }

    /// <summary>保温时间(min)，仅固溶工段使用</summary>
    public int? SoakTime { get; set; }

    /// <summary>产类（荒管/在制/成品），由系统自动计算</summary>
    public string? ProductStatus { get; set; }

    /// <summary>预成切（虽然是成品切割，但不是正式成品切割；不计入成品切割统计支数）</summary>
    public bool? IsPreCut { get; set; }

    /// <summary>长度状态（定尺/范围尺/非定尺），断切成品时自动填充</summary>
    public LengthStatus? LengthStatus { get; set; }
    public decimal? CuttingMultiple { get; set; }
    public decimal? FinishedCutLength { get; set; }

    /// <summary>定尺切割长度匹配标识（完全匹配/主号匹配/不适用），仅成品切割+定尺+非预成切时计算</summary>
    public CutLengthMatchType? CutLengthMatchType { get; set; }

    /// <summary>定尺切割长度匹配标识中文（完全匹配/主号匹配/空=不适用）</summary>
    public string? CutLengthMatchTypeDisplay => CutLengthMatchHelper.GetText(CutLengthMatchType);

    public int? PostCutQuantity { get; set; }

    /// <summary>平头数，仅荒管切割使用：1=一端，2=两端</summary>
    public int? FaceCutCount { get; set; }

    public string? TagNo { get; set; }
    public string? PlantGrade { get; set; }
    public string? Remark { get; set; }

    /// <summary>数据来源（SCAN=扫码报工，MANUAL=手动录入）</summary>
    public string? DataSource { get; set; }

    /// <summary>批次号（冗余，用于跨批次列表展示）</summary>
    public string? BatchNo { get; set; }

    /// <summary>工单号（从批次导航属性投影）</summary>
    public string? WorkOrderNo { get; set; }

    /// <summary>订单号（从批次导航属性投影）</summary>
    public string? SalesOrderNo { get; set; }

    /// <summary>主号（从批次导航属性投影）</summary>
    public string? ProductionMainNo { get; set; }

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

    public ShiftType? Shift { get; set; }

    public int? Quantity { get; set; }
    public decimal? Weight { get; set; }

    /// <summary>固溶温度(℃)，仅固溶工段使用</summary>
    public decimal? SolutionTemperature { get; set; }

    /// <summary>保温时间(min)，仅固溶工段使用</summary>
    public int? SoakTime { get; set; }

    /// <summary>预成切（虽然是成品切割，但不是正式成品切割；不计入成品切割统计支数）</summary>
    public bool? IsPreCut { get; set; }

    public decimal? CuttingMultiple { get; set; }
    public decimal? FinishedCutLength { get; set; }
    public int? PostCutQuantity { get; set; }

    /// <summary>平头数，仅荒管切割使用：1=一端，2=两端</summary>
    public int? FaceCutCount { get; set; }

    [MaxLength(50)]
    public string? TagNo { get; set; }

    [Required(ErrorMessage = "工厂牌号不能为空")]
    [MaxLength(50)]
    public string PlantGrade { get; set; } = string.Empty;

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

    public ShiftType? Shift { get; set; }

    public int? Quantity { get; set; }
    public decimal? Weight { get; set; }

    /// <summary>固溶温度(℃)，仅固溶工段使用</summary>
    public decimal? SolutionTemperature { get; set; }

    /// <summary>保温时间(min)，仅固溶工段使用</summary>
    public int? SoakTime { get; set; }

    /// <summary>预成切（虽然是成品切割，但不是正式成品切割；不计入成品切割统计支数）</summary>
    public bool? IsPreCut { get; set; }

    public decimal? CuttingMultiple { get; set; }
    public decimal? FinishedCutLength { get; set; }
    public int? PostCutQuantity { get; set; }

    /// <summary>平头数，仅荒管切割使用：1=一端，2=两端</summary>
    public int? FaceCutCount { get; set; }

    [MaxLength(50)]
    public string? TagNo { get; set; }

    [Required(ErrorMessage = "工厂牌号不能为空")]
    [MaxLength(50)]
    public string PlantGrade { get; set; } = string.Empty;

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

