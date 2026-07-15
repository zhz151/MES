namespace MES.Data.Entities.Quality;

/// <summary>
/// 质量证明书附表（子项）— 类似"订单+项次"模式的项次
/// </summary>
public class CertificateItem : BaseEntity
{
    /// <summary>所属质量证明书ID</summary>
    public int CertificateId { get; set; }

    /// <summary>序号，从1开始</summary>
    public int SeqNo { get; set; }

    // ========== 第1类：仓库信息 ==========

    /// <summary>库存批次（仓库批次号）</summary>
    public string? InventoryBatchNo { get; set; }
    /// <summary>生产批次</summary>
    public string? ProductionBatchNo { get; set; }
    /// <summary>炉号</summary>
    public string? HeatNo { get; set; }
    /// <summary>钢牌号</summary>
    public string? SteelGrade { get; set; }
    /// <summary>规格</summary>
    public string? Specification { get; set; }
    /// <summary>长度描述（如"8000-9000"，字符串兼容范围值）</summary>
    public string? LengthDesc { get; set; }
    /// <summary>支数</summary>
    public int? Quantity { get; set; }
    /// <summary>米数</summary>
    public decimal? Meters { get; set; }
    /// <summary>重量(kg)</summary>
    public decimal? Weight { get; set; }

    // ========== 第2类：化学成分（仅存实测值） ==========

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

    // ========== 第3类：成品检验结果 ==========

    /// <summary>PMI检测</summary>
    public string? InspPMI { get; set; }
    /// <summary>表检</summary>
    public string? InspVisual { get; set; }
    /// <summary>尺寸</summary>
    public string? InspDimension { get; set; }
    /// <summary>内窥</summary>
    public string? InspEndoscopy { get; set; }
    /// <summary>水压</summary>
    public string? InspHydro { get; set; }
    /// <summary>水下气压</summary>
    public string? InspUnderwaterPneumatic { get; set; }
    /// <summary>涡流</summary>
    public string? InspEddyCurrent { get; set; }
    /// <summary>超声波</summary>
    public string? InspUltrasonic { get; set; }
    /// <summary>端口着色</summary>
    public string? InspPortDye { get; set; }

    // ========== 第4类：理化检测结果 ==========

    // -- 室温拉伸（4字段 × 2样） --
    public decimal? TensileStrength_1 { get; set; }
    public decimal? TensileStrength_2 { get; set; }
    public decimal? YieldRp02_1 { get; set; }
    public decimal? YieldRp02_2 { get; set; }
    public decimal? YieldRp10_1 { get; set; }
    public decimal? YieldRp10_2 { get; set; }
    public decimal? Elongation_1 { get; set; }
    public decimal? Elongation_2 { get; set; }

    // -- 硬度（1字段 × 2样） --
    public string? Hardness_1 { get; set; }
    public string? Hardness_2 { get; set; }

    // -- 晶粒度（1字段 × 2样） --
    public string? GrainSize_1 { get; set; }
    public string? GrainSize_2 { get; set; }

    // -- 金相（1字段 × 2样） --
    public decimal? FerriteContent_1 { get; set; }
    public decimal? FerriteContent_2 { get; set; }

    // -- 扩口/压扁/晶间腐蚀/点腐蚀（1字段 × 1样） --
    public string? FlaringResult { get; set; }
    public string? FlatteningResult { get; set; }
    public string? IntergranularResult { get; set; }
    public string? PittingResult { get; set; }
}
