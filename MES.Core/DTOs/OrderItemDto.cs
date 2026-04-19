// 文件路径: MES.Core/DTOs/OrderItemDto.cs
using MES.Core.Enums;

namespace MES.Core.DTOs;

/// <summary>
/// 订单项次 DTO
/// </summary>
public class OrderItemDto
{
    public int Id { get; set; }
    public int Sequence { get; set; }
    public DateTime DeliveryDate { get; set; }
    public bool DelayPenalty { get; set; }
    public SettlementMethod SettlementMethod { get; set; }
    public MaterialName MaterialName { get; set; }
    
    /// <summary>
    /// 产品标准编码（用于前端显示）
    /// </summary>
    public string ProductionStandardCode { get; set; } = null!;
    
    /// <summary>
    /// 产品标准编码（别名，与上面相同）
    /// </summary>
    public string StandardCode => ProductionStandardCode;
    
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
    
    /// <summary>
    /// 创建时间（审计字段）
    /// </summary>
    public DateTimeOffset CreatedTime { get; set; }
    
    /// <summary>
    /// 更新时间（审计字段）
    /// </summary>
    public DateTimeOffset UpdatedTime { get; set; }
}