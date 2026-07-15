namespace MES.Core.DTOs.Quality;

/// <summary>
/// 创建质保书请求
/// </summary>
public class CertificateCreateRequest
{
    /// <summary>订单号（用于生成 CertificateNo）</summary>
    public string OrderNo { get; set; } = null!;

    /// <summary>客户名称</summary>
    public string? CustomerName { get; set; }

    /// <summary>产品标准</summary>
    public string? ProductStandard { get; set; }

    /// <summary>产品名称</summary>
    public string? ProductName { get; set; }

    /// <summary>交货状态</summary>
    public string? DeliveryStatus { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>选中的库存批次号列表（用于生成 CertificateItem）</summary>
    public List<string> InventoryBatchNos { get; set; } = new();

    /// <summary>子项完整数据（含检查数据），一次性创建</summary>
    public List<CertificateItemUpdateDto> Items { get; set; } = new();
}

/// <summary>
/// 更新质保书请求
/// </summary>
public class CertificateUpdateRequest
{
    public string? CustomerName { get; set; }
    public string? ProductStandard { get; set; }
    public string? ProductName { get; set; }
    public string? DeliveryStatus { get; set; }
    public string? Remark { get; set; }
    public List<CertificateItemUpdateDto> Items { get; set; } = new();
}

/// <summary>
/// 更新子项 DTO
/// </summary>
public class CertificateItemUpdateDto
{
    public int? Id { get; set; } // null = 新增
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
