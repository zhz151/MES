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
    public string? ProductionMainNo { get; set; }
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

    /// <summary>
    /// 是否正式成检（成检类型==FormalInspection；null/其他/预成检均视为非正式成检）
    /// 仅正式成检时「制造状态/是否交付态」才有效，否则统一显示 "-"
    /// </summary>
    public bool IsFormalInspection =>
        string.Equals(InspectionType, nameof(MES.Core.Enums.InspectionType.FormalInspection), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 实时校验状态（列表加载时按当前工艺卡比对；null=正常）。
    /// 「成检类型过期」=存储成检类型与当前最深检验节点判定不一致；「工序组非检验」=关联工序组已不存在或不再是检验工序组。
    /// </summary>
    public string? HealthIssue { get; set; }

    /// <summary>
    /// 批次原始交货状态（始终填充，用于交付态计算，不受"仅最后工序组有效"影响）
    /// </summary>
    public string? RawDeliveryState { get; set; }

    /// <summary>
    /// 是否交付态（批次制造状态==交货状态为"是"，否则"否"；纯计算派生，随批次当前状态）
    /// 仅正式成检时有效，非正式成检返回 null（前端显示 "-"）
    /// </summary>
    public string? IsDeliveryStatus =>
        !IsFormalInspection
        ? null
        : !string.IsNullOrEmpty(ManufacturingStatus) && !string.IsNullOrEmpty(RawDeliveryState)
          && string.Equals(ManufacturingStatus, RawDeliveryState, StringComparison.OrdinalIgnoreCase) ? "是" : "否";

    // ========== 批次冗余字段 ==========
    public LengthStatus? LengthStatus { get; set; }
    public string? LengthStatusDisplay => LengthStatus.HasValue ? EnumHelper.GetDisplayName(LengthStatus.Value) : null;

    // ========== 工单冗余字段 ==========
    public string? Salesman { get; set; }
    public DeliveryState? DeliveryState { get; set; }
    public string? DeliveryStateDisplay => DeliveryState.HasValue ? EnumHelper.GetDisplayName(DeliveryState.Value) : null;

    /// <summary>制造状态（批次执行的实际制造状态，与交货状态同枚举）</summary>
    public string? ManufacturingStatus { get; set; }
    public string? ManufacturingStatusDisplay => !IsFormalInspection
        ? "-"
        : !string.IsNullOrEmpty(ManufacturingStatus) && EnumHelper.TryParse<DeliveryState>(ManufacturingStatus) is { } ms ? EnumHelper.GetDisplayName(ms) : "-";

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedTime { get; set; }
}

/// <summary>
/// 成检到料实时健康汇总（按当前筛选条件全量统计）
/// </summary>
public class MaterialCheckHealthSummaryDto
{
    /// <summary>筛选结果总数</summary>
    public int TotalCount { get; set; }

    /// <summary>成检类型疑问的生产编号（存储成检类型与当前工艺卡判定不一致）</summary>
    public List<string> InspectionTypeExpiredBatchNos { get; set; } = new();

    /// <summary>非成检批次的生产编号（关联工序组已不存在或不再是检验工序组）</summary>
    public List<string> ProcessGroupNotInspectionBatchNos { get; set; } = new();

    /// <summary>成检类型疑问数</summary>
    public int InspectionTypeExpiredCount => InspectionTypeExpiredBatchNos.Count;

    /// <summary>非成检批次数</summary>
    public int ProcessGroupNotInspectionCount => ProcessGroupNotInspectionBatchNos.Count;

    /// <summary>正常数</summary>
    public int NormalCount => TotalCount - InspectionTypeExpiredCount - ProcessGroupNotInspectionCount;

    /// <summary>异常总数</summary>
    public int IssueCount => InspectionTypeExpiredCount + ProcessGroupNotInspectionCount;
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

    /// <summary>
    /// 重选工序组ID（可选；提交后服务端校验归属该批次并联动重算工序名称/执行序/成检类型）
    /// </summary>
    public int? ProcessGroupId { get; set; }
}
