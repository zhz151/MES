using MES.Core.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace MES.Data.Entities;

public class OrderItem : BaseEntity
{
    public int SalesOrderId { get; set; }
    public int Sequence { get; set; }

    [NotMapped]
    public int? WorkOrderId { get; set; }
    public DateTime DeliveryDate { get; set; }

    public bool DelayPenalty { get; set; }

    public SettlementMethod SettlementMethod { get; set; }

    public MaterialName MaterialName { get; set; }

    public int ProductionStandardId { get; set; }

    public DeliveryState DeliveryState { get; set; }

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

    public LengthStatus LengthStatus { get; set; }
    public decimal? MinLength { get; set; }

    public decimal? MaxLength { get; set; }

    public int? Quantity { get; set; }

    public decimal? Meters { get; set; }

    public decimal ContractWeight { get; set; }

    public decimal TheoreticalWeight { get; set; }

    public string? Remark { get; set; }

    public SalesOrder SalesOrder { get; set; } = null!;

    public ProductionStandard ProductionStandard { get; set; } = null!;

    public StandardGradeMapping? GradeMapping { get; set; }

    [NotMapped]
    public WorkOrder? WorkOrder { get; set; }
    public ProductRequirement? ProductRequirement { get; set; }
}