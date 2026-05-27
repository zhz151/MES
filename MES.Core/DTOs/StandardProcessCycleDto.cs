// 文件路径: MES.Core/DTOs/StandardProcessCycleDto.cs
namespace MES.Core.DTOs;

public class StandardProcessCycleDto
{
    public int Id { get; set; }
    public string PlantGrade { get; set; } = string.Empty;
    public string RawMaterialType { get; set; } = string.Empty;
    public string RawSpec { get; set; } = string.Empty;
    public string ProductSpec { get; set; } = string.Empty;
    public string DeliveryState { get; set; } = string.Empty;
    public int StandardCycleDays { get; set; }
}
