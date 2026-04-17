namespace MES.Core.DTOs;

public class OrderItemDto
{
    public int Id { get; set; }
    public int Sequence { get; set; }
    public DateTime DeliveryDate { get; set; }
    public bool DelayPenalty { get; set; }
    public string SettlementMethod { get; set; } = null!;
    public string MaterialName { get; set; } = null!;
    public string ProductionStandardCode { get; set; } = null!;
    public string DeliveryState { get; set; } = null!;
    public string StandardGrade { get; set; } = null!;
    public string PlantGrade { get; set; } = null!;
    public decimal Density { get; set; }
    public decimal OuterDiameter { get; set; }
    public decimal WallThickness { get; set; }
    public string Specification { get; set; } = null!;
    public decimal OuterDiameterNegative { get; set; }
    public decimal OuterDiameterPositive { get; set; }
    public decimal WallThicknessNegative { get; set; }
    public decimal WallThicknessPositive { get; set; }
    public string LengthStatus { get; set; } = null!;
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }
    public int? Quantity { get; set; }
    public decimal? Meters { get; set; }
    public decimal ContractWeight { get; set; }
    public decimal TheoreticalWeight { get; set; }
    public string? Remark { get; set; }
}