namespace MES.Core.DTOs.Quality;

/// <summary>
/// 质量证明书打印列定义（打印链路用）：BlockKey=区块标识, Key=字段名, Label=显示名,
/// Visible=是否启用, ColumnIndex=区块内列顺序, ColumnWeight=列宽权重。
/// # 序号列为固定列，由打印模板固定置于表头首位。
/// </summary>
public class CertificatePrintColumnDef
{
    /// <summary>区块标识：Material / Chemistry / Inspection</summary>
    public string BlockKey { get; set; } = "";

    /// <summary>字段 Key（如 ProductionBatchNo / C / TensileStrength）</summary>
    public string Key { get; set; } = "";

    /// <summary>显示名（已按配置表覆盖后的最终列头文字）</summary>
    public string Label { get; set; } = "";

    /// <summary>英文显示名（表头第二行；可空，为空则不显示英文）</summary>
    public string? LabelEn { get; set; }

    /// <summary>是否启用（隐藏则不打印该列）</summary>
    public bool Visible { get; set; } = true;

    /// <summary>区块内列顺序（1 起）</summary>
    public int ColumnIndex { get; set; }

    /// <summary>列宽权重（相对比例，>0）</summary>
    public int ColumnWeight { get; set; } = 3;
}
