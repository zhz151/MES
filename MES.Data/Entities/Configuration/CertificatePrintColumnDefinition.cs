namespace MES.Data.Entities.Configuration;

/// <summary>
/// 质量证明书打印列布局配置表：明细表（物料信息/化学成分/检验检测）每个打印字段的显示配置
/// （是否启用/列顺序/列宽权重），数据库全局共享（仿 ProcessCardColumnDefinition 模式）。
/// 锚点 = BlockKey + FieldKey；# 序号列为固定列，不参与本配置。
/// </summary>
public class CertificatePrintColumnDefinition : BaseEntity
{
    /// <summary>区块：Material（物料信息）/ Chemistry（化学成分）/ Inspection（检验检测）</summary>
    public string BlockKey { get; set; } = string.Empty;

    /// <summary>字段 Key（如 ProductionBatchNo / C / TensileStrength）</summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>显示名（可改中文）</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>英文显示名（表头第二行；可空，为空则不显示英文）</summary>
    public string? LabelEn { get; set; }

    /// <summary>是否启用（隐藏则不打印该列）</summary>
    public bool Visible { get; set; } = true;

    /// <summary>区块内列顺序（1 起）</summary>
    public int ColumnIndex { get; set; }

    /// <summary>列宽权重（相对比例，>0）</summary>
    public int ColumnWeight { get; set; } = 3;
}
