using MES.Core.Enums;

namespace MES.Data.Entities;

public class CustomerProfile : BaseEntity
{
    public string CustomerCode { get; set; } = null!;

    public string Salesman { get; set; } = null!;

    public string CustomerUnit { get; set; } = null!;
    public string? EndCustomer { get; set; }

    public string? ContactPerson { get; set; }

    public string? ContactPhone { get; set; }

    public string? Address { get; set; }

    public CustomerStatus Status { get; set; }

    public string? Remark { get; set; }
    public ICollection<SalesOrder> SalesOrders { get; set; } = new List<SalesOrder>();
}