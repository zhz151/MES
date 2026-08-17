namespace MES.Core.DTOs.Configuration;

/// <summary>
/// 质量证明书打印列布局配置 DTO（「字段布局」面板批量保存/加载用）。
/// BlockKey+FieldKey 为唯一锚点；ColumnIndex 区块内排序键，ColumnWeight 列宽相对权重（>0）。
/// # 序号列为固定列，不参与本配置。
/// </summary>
public class CertificatePrintColumnDefinitionDto
{
    public int Id { get; set; }

    /// <summary>区块：Material / Chemistry / Inspection</summary>
    public string BlockKey { get; set; } = string.Empty;

    /// <summary>字段 Key</summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>显示名（可改中文）</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>英文显示名（表头第二行；可空，为空则不显示英文）</summary>
    public string? LabelEn { get; set; }

    /// <summary>是否启用</summary>
    public bool Visible { get; set; } = true;

    /// <summary>区块内列顺序（1 起）</summary>
    public int ColumnIndex { get; set; }

    /// <summary>列宽权重（相对比例，>0）</summary>
    public int ColumnWeight { get; set; } = 3;
}
