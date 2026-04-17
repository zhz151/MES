using MES.Core.Enums;

namespace MES.Data.Entities;

public class WorkOrder : BaseEntity
{
    public string OrderNo { get; set; } = null!;

    public int OrderItemId { get; set; }

    public WorkOrderStatus Status { get; set; }

    public int Priority { get; set; }

    public DateTime? PlannedStartDate { get; set; }

    public DateTime? PlannedEndDate { get; set; }

    public DateTime? ActualStartDate { get; set; }

    public DateTime? ActualEndDate { get; set; }

    public string? Remark { get; set; }

    public OrderItem OrderItem { get; set; } = null!;

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}