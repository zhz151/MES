using System.ComponentModel.DataAnnotations;

using MES.Core.DTOs.Shared;
using MES.Core.Enums;
using MES.Core.Helpers;
namespace MES.Core.DTOs.Batch;

// ========== 入缸记录（PicklingInRecord）==========

/// <summary>
/// 去油/酸洗入缸记录DTO（用于列表展示）
/// </summary>
public class PicklingInRecordDto
{
    public int Id { get; set; }
    public int ProductionBatchId { get; set; }
    public int ProcessGroupId { get; set; }

    /// <summary>批次号（冗余自 ProductionBatch）</summary>
    public string BatchNo { get; set; } = null!;
    /// <summary>工序名称</summary>
    public string ProcessName { get; set; } = null!;
    /// <summary>制造规格</summary>
    public string? ManufacturingSpec { get; set; }
    /// <summary>工段名称（去油/酸洗）</summary>
    public string SectionName { get; set; } = null!;
    /// <summary>执行序号</summary>
    public int SequenceNumber { get; set; }

    /// <summary>入缸日期</summary>
    public DateTime InDate { get; set; }
    /// <summary>状态（Soaking=浸泡中, Completed=已完工）</summary>
    public PicklingStatus Status { get; set; }
    public string StatusDisplay => EnumHelper.GetDisplayName(Status);

    /// <summary>设备名称</summary>
    public string? EquipmentName { get; set; }
    /// <summary>操作人</summary>
    public string? Operator { get; set; }
    /// <summary>班次</summary>
    public ShiftType? Shift { get; set; }
    public string? ShiftDisplay => Shift.HasValue ? EnumHelper.GetDisplayName(Shift.Value) : null;
    /// <summary>加工数量（支数）</summary>
    public int? Quantity { get; set; }
    /// <summary>加工重量(kg)</summary>
    public decimal? Weight { get; set; }
    /// <summary>制造状态（荒管/在制/成品）</summary>
    public string? ProductStatus { get; set; }

    public string? TagNo { get; set; }
    public string? PlantGrade { get; set; }
    public string? Remark { get; set; }
    public string? DataSource { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }

    // ========== 完工汇总 ==========
    /// <summary>关联完工记录ID（为空则未完工）</summary>
    public int? PicklingOutRecordId { get; set; }
    /// <summary>完工日期</summary>
    public DateTime? CompleteDate { get; set; }
    /// <summary>完工班次（出缸时登记的，区别于入缸班次）</summary>
    public ShiftType? CompleteShift { get; set; }
    /// <summary>完工操作人（出缸时登记的，区别于入缸操作人）</summary>
    public string? CompleteOperator { get; set; }
}

/// <summary>
/// 创建入缸记录请求（使用 BatchNo 替代 ProductionBatchId）
/// </summary>
public class CreatePicklingInRecordRequest
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

    [Required(ErrorMessage = "入缸日期不能为空")]
    public DateTime InDate { get; set; }

    /// <summary>设备名称</summary>
    [MaxLength(100)]
    public string? EquipmentName { get; set; }
    /// <summary>操作人</summary>
    [MaxLength(50)]
    public string? Operator { get; set; }
    /// <summary>班次</summary>
    public ShiftType? Shift { get; set; }
    /// <summary>加工数量（支数）</summary>
    public int? Quantity { get; set; }
    /// <summary>加工重量(kg)</summary>
    public decimal? Weight { get; set; }

    [MaxLength(50)]
    public string? TagNo { get; set; }

    [Required(ErrorMessage = "工厂牌号不能为空")]
    [MaxLength(50)]
    public string PlantGrade { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Remark { get; set; }

    [MaxLength(10)]
    public string? DataSource { get; set; }
}

/// <summary>
/// 更新入缸记录请求（内联编辑）
/// </summary>
public class UpdatePicklingInRecordRequest
{
    public DateTime? InDate { get; set; }
    [MaxLength(100)]
    public string? EquipmentName { get; set; }
    [MaxLength(50)]
    public string? Operator { get; set; }
    public ShiftType? Shift { get; set; }
    public int? Quantity { get; set; }
    public decimal? Weight { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }
}

// ========== 完工记录（PicklingOutRecord）==========

/// <summary>
/// 去油/酸洗完工记录DTO
/// </summary>
public class PicklingOutRecordDto
{
    public int Id { get; set; }
    public int PicklingInRecordId { get; set; }
    public DateTime CompleteDate { get; set; }
    public string? Remark { get; set; }
    public string? DataSource { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }

    // ========== 冗余字段（与实体字段对齐）==========
    public int ProductionBatchId { get; set; }
    public string? BatchNo { get; set; }
    public string? ProcessName { get; set; }
    public string? ManufacturingSpec { get; set; }
    public string SectionName { get; set; } = null!;
    public string? TagNo { get; set; }
    public string? PlantGrade { get; set; }
    public string? EquipmentName { get; set; }
    public string? Operator { get; set; }
    public ShiftType? Shift { get; set; }
    public string? ShiftDisplay => Shift.HasValue ? EnumHelper.GetDisplayName(Shift.Value) : null;
    public int? Quantity { get; set; }
    public decimal? Weight { get; set; }
    public string? ProductStatus { get; set; }
}

/// <summary>
/// 创建完工记录请求
/// </summary>
public class CreatePicklingOutRecordRequest
{
    [Required(ErrorMessage = "关联入缸记录ID不能为空")]
    public int PicklingInRecordId { get; set; }

    [Required(ErrorMessage = "完工日期不能为空")]
    public DateTime CompleteDate { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }

    [MaxLength(10)]
    public string? DataSource { get; set; }

    /// <summary>完工班次（出缸时登记，区别于入缸班次）</summary>
    public ShiftType? Shift { get; set; }

    /// <summary>完工操作人（出缸时登记，区别于入缸操作人）</summary>
    [MaxLength(50)]
    public string? Operator { get; set; }
}

/// <summary>
/// 更新完工记录请求
/// </summary>
public class UpdatePicklingOutRecordRequest
{
    public DateTime? CompleteDate { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }

    /// <summary>完工班次</summary>
    public ShiftType? Shift { get; set; }

    /// <summary>完工操作人</summary>
    [MaxLength(50)]
    public string? Operator { get; set; }
}

// ========== 打印相关 ==========

// ========== 入缸记录打印 ==========

/// <summary>
/// 入缸记录打印已选请求
/// </summary>
public class PicklingInRecordPrintBatchRequest
{
    [Required(ErrorMessage = "请选择要打印的记录")]
    public int[] Ids { get; set; } = Array.Empty<int>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 入缸记录打印全部请求
/// </summary>
public class PicklingInRecordPrintAllRequest
{
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; } = true;
    public DateTime? InDateFrom { get; set; }
    public DateTime? InDateTo { get; set; }
    public DateTime? CompleteDateFrom { get; set; }
    public DateTime? CompleteDateTo { get; set; }
    public List<PrintColumnDef> Columns { get; set; } = new();
}

// ========== 完工记录打印 ==========

/// <summary>
/// 完工记录打印已选请求
/// </summary>
public class PicklingOutRecordPrintBatchRequest
{
    [Required(ErrorMessage = "请选择要打印的记录")]
    public int[] Ids { get; set; } = Array.Empty<int>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 完工记录打印全部请求
/// </summary>
public class PicklingOutRecordPrintAllRequest
{
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; } = true;
    public DateTime? CompleteDateFrom { get; set; }
    public DateTime? CompleteDateTo { get; set; }
    public List<PrintColumnDef> Columns { get; set; } = new();
}
