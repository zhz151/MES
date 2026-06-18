namespace MES.Core.DTOs;

/// <summary>
/// 维修记录列表 DTO
/// </summary>
public class RepairOrderListDto
{
    public int Id { get; set; }
    public string RepairOrderNo { get; set; } = null!;
    public int EquipmentId { get; set; }
    public string EquipmentName { get; set; } = null!;
    public string? EquipmentCode { get; set; }
    public string? EquipmentLocation { get; set; }
    public string FaultDescription { get; set; } = null!;
    public string? FaultType { get; set; }
    public string Priority { get; set; } = null!;
    public string RepairStatus { get; set; } = null!; // 动态计算
    public string ReportPerson { get; set; } = null!;
    public DateTime ReportTime { get; set; }
    public string? RepairPerson { get; set; }
    public string? RepairCategory { get; set; }
    public DateTime? RepairStartTime { get; set; }
    public DateTime? RepairEndTime { get; set; }
    public string? RepairContent { get; set; }
    public string? SparePartUsed { get; set; }
    public string? OtherRepairPersons { get; set; }
}

/// <summary>
/// 创建维修工单请求
/// </summary>
public class CreateRepairOrderRequest
{
    public int EquipmentId { get; set; }
    public string FaultDescription { get; set; } = null!;
    public string? FaultType { get; set; }
    public string Priority { get; set; } = nameof(MES.Core.Enums.RepairPriority.Normal);
    public string ReportPerson { get; set; } = null!;
    public DateTime ReportTime { get; set; }
    public string? RepairPerson { get; set; }
    public string? RepairCategory { get; set; }
    public DateTime? RepairStartTime { get; set; }
    public DateTime? RepairEndTime { get; set; }
    public string? RepairContent { get; set; }
    public string? SparePartUsed { get; set; }
    public string? OtherRepairPersons { get; set; }
}

/// <summary>
/// 更新维修工单请求
/// </summary>
public class UpdateRepairOrderRequest
{
    public string? FaultDescription { get; set; }
    public string? FaultType { get; set; }
    public string? Priority { get; set; }
    public string? ReportPerson { get; set; }
    public DateTime? ReportTime { get; set; }
    public string? RepairPerson { get; set; }
    public string? RepairCategory { get; set; }
    public DateTime? RepairStartTime { get; set; }
    public DateTime? RepairEndTime { get; set; }
    public string? RepairContent { get; set; }
    public string? SparePartUsed { get; set; }
    public string? OtherRepairPersons { get; set; }
}

/// <summary>
/// 开始维修请求（仅设置维修人和开始时间）
/// </summary>
public class StartRepairRequest
{
    /// <summary>维修人（当前扫码人）</summary>
    public string RepairPerson { get; set; } = null!;
}

/// <summary>
/// 完成维修请求
/// </summary>
public class CompleteRepairRequest
{
    /// <summary>维修类别</summary>
    public string? RepairCategory { get; set; }

    /// <summary>维修内容</summary>
    public string RepairContent { get; set; } = null!;

    /// <summary>使用备件</summary>
    public string? SparePartUsed { get; set; }

    /// <summary>其它维修人（多人协作时补充）</summary>
    public List<string>? OtherRepairPersons { get; set; }
}

/// <summary>
/// 维修工单打印请求（批量）
/// </summary>
public class RepairOrderPrintBatchRequest
{
    public int[] Ids { get; set; } = Array.Empty<int>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 维修工单打印请求（全部）
/// </summary>
public class RepairOrderPrintAllRequest
{
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; }
    public int? EquipmentId { get; set; }
    public string? RepairStatus { get; set; }
    public string? Priority { get; set; }
    public DateTime? ReportTimeFrom { get; set; }
    public DateTime? ReportTimeTo { get; set; }
    public List<PrintColumnDef> Columns { get; set; } = new();
}
