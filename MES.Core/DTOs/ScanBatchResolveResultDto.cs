namespace MES.Core.DTOs;

/// <summary>
/// 按批次号扫码解析结果（含该批次下的所有工序组选项）
/// </summary>
public class ScanBatchResolveResultDto
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

    /// <summary>该批次下的所有工序组选项</summary>
    public List<ProcessGroupOption> ProcessGroups { get; set; } = new();
}

/// <summary>
/// 工序组选项
/// </summary>
public class ProcessGroupOption
{
    /// <summary>工序组ID（数据库主键）</summary>
    public int Id { get; set; }

    /// <summary>序号</summary>
    public int SequenceNumber { get; set; }

    /// <summary>工序名称</summary>
    public string ProcessName { get; set; } = null!;

    /// <summary>制造规格</summary>
    public string? ManufacturingSpec { get; set; }
}
