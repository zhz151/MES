namespace MES.Core.DTOs;

/// <summary>
/// 扫码解析结果
/// </summary>
public class ScanResolveResultDto
{
    /// <summary>批次号</summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>批次状态</summary>
    public string Status { get; set; } = null!;

    /// <summary>工厂牌号（钢种）</summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>规格</summary>
    public string Specification { get; set; } = null!;

    /// <summary>挂牌号</summary>
    public string? TagNo { get; set; }

    /// <summary>生产类型</summary>
    public string? ProductionType { get; set; }

    /// <summary>工序组ID</summary>
    public int ProcessGroupId { get; set; }

    /// <summary>工序名称</summary>
    public string ProcessName { get; set; } = null!;

    /// <summary>制造规格</summary>
    public string? ManufacturingSpec { get; set; }

    /// <summary>可用工段列表（ProcessGroup 非 null 字段，排除"检验"）</summary>
    public List<SectionOption> AvailableSections { get; set; } = new();
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
