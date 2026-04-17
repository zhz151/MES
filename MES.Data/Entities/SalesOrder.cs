using MES.Core.Enums;

namespace MES.Data.Entities;

public class SalesOrder : BaseEntity
{
    public string OrderNumber { get; set; } = null!;
    public DateTime SignDate { get; set; }

    public int CustomerId { get; set; }

    public SalesOrderStatus Status { get; set; }

    public byte[] RowVersion { get; set; } = null!;
    public CustomerProfile Customer { get; set; } = null!;

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}