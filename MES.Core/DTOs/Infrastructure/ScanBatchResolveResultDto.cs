using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Infrastructure;

/// <summary>
/// 按批次号扫码解析结果（含该批次下的所有工序组选项）
/// </summary>
public class ScanBatchResolveResultDto
{
    /// <summary>批次号</summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>批次状态</summary>
    public BatchStatus Status { get; set; }

    /// <summary>批次状态中文显示</summary>
    public string StatusDisplay => EnumHelper.GetDisplayName(Status);

    /// <summary>工厂牌号（钢种）</summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>规格</summary>
    public string Specification { get; set; } = null!;

    /// <summary>挂牌号</summary>
    public string? TagNo { get; set; }

    /// <summary>单支重量（总重量/总支数，Round 4），用于扫码自动算重</summary>
    public decimal? UnitWeight { get; set; }

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

    /// <summary>该工序组包含的工段名称列表（用于按工位工段过滤）</summary>
    public List<string> SectionNames { get; set; } = new();

    /// <summary>是否检验工序组（ProcessGroup.Inspection 有值）：成检到料可选组/自动匹配以此为准</summary>
    public bool IsInspectionGroup { get; set; }
}
