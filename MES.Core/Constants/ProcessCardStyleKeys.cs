namespace MES.Core.Constants;

/// <summary>
/// 工艺卡打印版式配置键（ProcessCardStyleDefinition 配置表 Key），
/// 默认值与 ProcessCardPrintHelper 打印模板硬编码值保持一致。
/// </summary>
public static class ProcessCardStyleKeys
{
    /// <summary>正文字体族（页面默认字体）</summary>
    public const string PageFontFamily = "PageFontFamily";

    /// <summary>正文字号（页面默认字号）</summary>
    public const string PageFontSize = "PageFontSize";

    /// <summary>主标题字体族（工艺流转卡标题）</summary>
    public const string HeaderFontFamily = "HeaderFontFamily";

    /// <summary>主标题字号</summary>
    public const string HeaderFontSize = "HeaderFontSize";

    /// <summary>生产编号字号（页眉副标题）</summary>
    public const string BatchNoFontSize = "BatchNoFontSize";

    /// <summary>区块标题字号（批次基本信息/质量要求/投料信息/工单信息/工序组标题）</summary>
    public const string BlockTitleFontSize = "BlockTitleFontSize";

    /// <summary>表格表头字号</summary>
    public const string TableHeaderFontSize = "TableHeaderFontSize";

    /// <summary>数据单元格字号</summary>
    public const string CellFontSize = "CellFontSize";
}
