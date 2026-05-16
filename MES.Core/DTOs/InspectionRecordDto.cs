namespace MES.Core.DTOs;

/// <summary>
/// 点检记录列表 DTO
/// </summary>
public class InspectionRecordListDto
{
    public int Id { get; set; }
    public string RecordNo { get; set; } = null!;
    public int EquipmentId { get; set; }
    public string EquipmentName { get; set; } = null!;
    public string? EquipmentCode { get; set; }
    public string? Location { get; set; }
    public DateTime? ActualDate { get; set; }
    public string? Inspector { get; set; }
    public string? ExecutionSummary { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 创建点检记录请求
/// </summary>
public class CreateInspectionRecordRequest
{
    public int EquipmentId { get; set; }
    public DateTime? ActualDate { get; set; }
    public string? Inspector { get; set; }
    public string? ExecutionSummary { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新点检记录请求
/// </summary>
public class UpdateInspectionRequest
{
    public DateTime? ActualDate { get; set; }
    public string? Inspector { get; set; }
    public string? ExecutionSummary { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 点检记录打印请求（批量）
/// </summary>
public class InspectionRecordPrintBatchRequest
{
    public int[] Ids { get; set; } = Array.Empty<int>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 点检记录打印请求（全部）
/// </summary>
public class InspectionRecordPrintAllRequest
{
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; }
    public int? EquipmentId { get; set; }
    public List<PrintColumnDef> Columns { get; set; } = new();
}
