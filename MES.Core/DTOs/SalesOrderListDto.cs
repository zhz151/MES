namespace MES.Core.DTOs;
public class SalesOrderListDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = null!;
    public DateTime SignDate { get; set; }
    public string CustomerName { get; set; } = null!;
    public string Status { get; set; } = null!;
}