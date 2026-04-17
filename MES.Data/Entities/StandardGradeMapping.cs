namespace MES.Data.Entities;

public class StandardGradeMapping : BaseEntity
{
    public string StandardGrade { get; set; } = null!;
    public string PlantGrade { get; set; } = null!;

    public decimal Density { get; set; }

    public string? HeatTreatment { get; set; }


    public bool SpecialMaterial { get; set; }

    public string? SpecialNote { get; set; }

    public string? Remark { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}