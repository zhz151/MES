namespace MES.Core.DTOs.Infrastructure;

/// <summary>
/// 扫码解析结果
/// </summary>
public class ScanResolveResultDto
{
    /// <summary>批次号</summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>工厂牌号（钢种）</summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>工序组ID</summary>
    public int ProcessGroupId { get; set; }

    /// <summary>工序名称</summary>
    public string ProcessName { get; set; } = null!;

    /// <summary>制造规格</summary>
    public string? ManufacturingSpec { get; set; }

    /// <summary>可用工段列表（ProcessGroup 非 null 字段，排除"检验"）</summary>
    public List<SectionOption> AvailableSections { get; set; } = new();

    /// <summary>单支重量(kg)，用于扫码报工自动计算总重量 = 支数 × UnitWeight</summary>
    public decimal? UnitWeight { get; set; }

    /// <summary>产类预判（英文 Key：Finished=成品 / RoughTube=荒管 / InProgress=在制），扫码断切按此分流显示参数</summary>
    public string? ProductStatus { get; set; }
}

/// <summary>
/// 工段选项
/// </summary>
public class SectionOption
{
    /// <summary>工段名称</summary>
    public string SectionName { get; set; } = null!;

    /// <summary>执行序号</summary>
    public int SequenceNumber { get; set; }
}
