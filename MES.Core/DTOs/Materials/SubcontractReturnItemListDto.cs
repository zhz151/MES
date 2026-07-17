using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Materials;

/// <summary>
/// 委外子项执行查询 — 列表 DTO
/// </summary>
public class SubcontractReturnItemListDto
{
    public int Id { get; set; }
    public int SubcontractOrderId { get; set; }
    public string? OrderNo { get; set; }
    public string? SupplierName { get; set; }
    public string? SourceWorkOrderNo { get; set; }
    public string? PlantGrade { get; set; }
    public string ProcessSpecification { get; set; } = null!;
    public decimal? UnitWeight { get; set; }
    public int? RequiredQuantity { get; set; }
    public decimal? RequiredWeight { get; set; }
    public DateTime? ReturnDeadline { get; set; }
    public int ReturnedQuantity { get; set; }
    public decimal ReturnedWeight { get; set; }
    public string ProcessStatus { get; set; } = null!;

    public string ProcessStatusDisplay
    {
        get
        {
            var parsed = EnumHelper.TryParse<SubcontractOrderStatus>(ProcessStatus);
            return parsed.HasValue ? EnumHelper.GetDisplayName(parsed.Value) : ProcessStatus;
        }
    }
}
