namespace MES.Core.DTOs;

public class SalesOrderDetailDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = null!;
    public DateTime SignDate { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public string Salesman { get; set; } = null!;
    public string Status { get; set; } = null!;
    public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
}