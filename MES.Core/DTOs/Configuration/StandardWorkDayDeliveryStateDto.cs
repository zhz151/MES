using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Configuration;

/// <summary>
/// 交货状态附加天数 DTO
/// </summary>
public class StandardWorkDayDeliveryStateDto
{
    public int Id { get; set; }
    public DeliveryState? DeliveryState { get; set; }
    public double ExtraDays { get; set; }
    public string? PlantGradePrefix { get; set; }
    public string? Remark { get; set; }
}
