namespace MES.Core.DTOs.Configuration;

/// <summary>
/// 交货状态附加天数 DTO
/// </summary>
public class StandardWorkDayDeliveryStateDto
{
    public int Id { get; set; }
    public string DeliveryState { get; set; } = string.Empty;
    public double ExtraDays { get; set; }
    public string? PlantGradePrefix { get; set; }
    public string? Remark { get; set; }
}
