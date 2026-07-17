using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Quality;

/// <summary>
/// 质保书详情 DTO（含子项）
/// </summary>
public class CertificateDetailDto
{
    public int Id { get; set; }
    public string CertificateNo { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public string? CustomerName { get; set; }
    public string? ProductStandard { get; set; }
    public string? ProductName { get; set; }
    public DeliveryState? DeliveryStatus { get; set; }
    public string? DeliveryStatusDisplay => DeliveryStatus.HasValue ? EnumHelper.GetDisplayName(DeliveryStatus.Value) : null;
    public string? Remark { get; set; }
    public List<CertificateItemDto> Items { get; set; } = new();
}

/// <summary>
/// 质保书子项 DTO
/// </summary>
public class CertificateItemDto
{
    public int Id { get; set; }
    public int SeqNo { get; set; }

    // 第1类：仓库信息
    public string? InventoryBatchNo { get; set; }
    public string? ProductionBatchNo { get; set; }
    public string? HeatNo { get; set; }
    public string? SteelGrade { get; set; }
    public string? Specification { get; set; }
    public string? LengthDesc { get; set; }
    public int? Quantity { get; set; }
    public decimal? Meters { get; set; }
    public decimal? Weight { get; set; }

    // 第2类：化学成分
    public decimal? ChemC { get; set; }
    public decimal? ChemSi { get; set; }
    public decimal? ChemMn { get; set; }
    public decimal? ChemP { get; set; }
    public decimal? ChemS { get; set; }
    public decimal? ChemNi { get; set; }
    public decimal? ChemCr { get; set; }
    public decimal? ChemMo { get; set; }
    public decimal? ChemCu { get; set; }
    public decimal? ChemN { get; set; }
    public decimal? ChemNb { get; set; }
    public decimal? ChemTi { get; set; }
    public decimal? ChemFe { get; set; }
    public decimal? ChemAl { get; set; }
    public decimal? ChemW { get; set; }
    public decimal? ChemPREN { get; set; }

    // 第3类：成品检验
    public string? InspPMI { get; set; }
    public string? InspVisual { get; set; }
    public string? InspDimension { get; set; }
    public string? InspEndoscopy { get; set; }
    public string? InspHydro { get; set; }
    public string? InspUnderwaterPneumatic { get; set; }
    public string? InspEddyCurrent { get; set; }
    public string? InspUltrasonic { get; set; }
    public string? InspPortDye { get; set; }

    // 第4类：理化检测
    public decimal? TensileStrength_1 { get; set; }
    public decimal? TensileStrength_2 { get; set; }
    public decimal? YieldRp02_1 { get; set; }
    public decimal? YieldRp02_2 { get; set; }
    public decimal? YieldRp10_1 { get; set; }
    public decimal? YieldRp10_2 { get; set; }
    public decimal? Elongation_1 { get; set; }
    public decimal? Elongation_2 { get; set; }
    public string? Hardness_1 { get; set; }
    public string? Hardness_2 { get; set; }
    public string? GrainSize_1 { get; set; }
    public string? GrainSize_2 { get; set; }
    public decimal? FerriteContent_1 { get; set; }
    public decimal? FerriteContent_2 { get; set; }
    public string? FlaringResult { get; set; }
    public string? FlatteningResult { get; set; }
    public string? IntergranularResult { get; set; }
    public string? PittingResult { get; set; }
}
