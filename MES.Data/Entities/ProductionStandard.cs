namespace MES.Data.Entities;

public class ProductionStandard : BaseEntity
{
    public string StandardCode { get; set; } = null!;

    public string StandardName { get; set; } = null!;

    public string? Remark { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

}