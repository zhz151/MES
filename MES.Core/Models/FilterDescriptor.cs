namespace MES.Core.Models;

/// <summary>
/// 筛选描述符 — 用于通用列表筛选（每列独立筛选）
/// </summary>
public class FilterDescriptor
{
    /// <summary>实体属性名，如 "BatchNo"、"Status"</summary>
    public string Field { get; set; } = "";

    /// <summary>
    /// 操作符：
    ///   contains / equals / startsWith — string 字段
    ///   gt / lt / gte / lte — 数字/日期
    ///   range — 日期/数字范围（配合 From/To）
    ///   in — 多选（配合 Values）
    /// </summary>
    public string Operator { get; set; } = "contains";

    /// <summary>单值（contains/equals/startsWith/gt/lt/gte/lte）</summary>
    public string? Value { get; set; }

    /// <summary>多值（in 操作符）</summary>
    public List<string>? Values { get; set; }

    /// <summary>范围开始（range 操作符，适用于 DateTime 或 decimal）</summary>
    public object? From { get; set; }

    /// <summary>范围结束（range 操作符，适用于 DateTime 或 decimal）</summary>
    public object? To { get; set; }

    /// <summary>是否同时匹配空值（条件后加 OR field IS NULL）</summary>
    public bool IncludeNull { get; set; }
}
