namespace MES.Core.Constants;

/// <summary>
/// 段落类别类型 — 段落日产配置的基础 3 类（配置驱动自动生成）。
/// </summary>
public static class ParagraphCategoryTypes
{
    /// <summary>冷轧拔：按冷轧机台组配置的组显示名（ParagraphKey=机台组 GroupKey）</summary>
    public const string Cold = "Cold";

    /// <summary>普通工段：按 StandardWorkDays 启用工段（ParagraphKey=SectionKey）</summary>
    public const string Section = "Section";

    /// <summary>固定检验：荒管检/在制检（ParagraphKey=固定中文）</summary>
    public const string Fixed = "Fixed";
}
