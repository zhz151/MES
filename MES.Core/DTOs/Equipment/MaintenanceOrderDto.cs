using MES.Core.DTOs.Shared;
namespace MES.Core.DTOs.Equipment;

/// <summary>
/// 保养记录列表 DTO
/// </summary>
public class MaintenanceOrderListDto
{
    public int Id { get; set; }
    public string MaintOrderNo { get; set; } = null!;
    public int EquipmentId { get; set; }
    public string EquipmentName { get; set; } = null!;
    public string? EquipmentCode { get; set; }
    public string? Location { get; set; }
    public DateTime? ActualDate { get; set; }
    public string? Executor { get; set; }
    public string? ExecutionSummary { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 创建保养工单请求
/// </summary>
public class CreateMaintenanceOrderRequest
{
    public int EquipmentId { get; set; }
    public DateTime? ActualDate { get; set; }
    public string? Executor { get; set; }
    public string? ExecutionSummary { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新保养工单请求
/// </summary>
public class UpdateMaintenanceRequest
{
    public DateTime? ActualDate { get; set; }
    public string? Executor { get; set; }
    public string? ExecutionSummary { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 保养工单打印请求（批量）
/// </summary>
public class MaintenanceOrderPrintBatchRequest
{
    public int[] Ids { get; set; } = Array.Empty<int>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 保养工单打印请求（全部）
/// </summary>
public class MaintenanceOrderPrintAllRequest
{
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; }
    public int? EquipmentId { get; set; }
    public List<PrintColumnDef> Columns { get; set; } = new();
}
