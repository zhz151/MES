using System;
using System.Linq;
using MES.Core.Enums;

namespace MES.Core.Helpers;

/// <summary>
/// 数据工具（DataExchange）「string 属性但存英文 Key/枚举名」字段的集中双向转换。
/// 导出：ToDisplay（英文 Key → 中文）；导入：ToKey（中文 → 英文 Key）。
/// 与 DataExchangeRegistry 中已标 isEnum 的列（走 EnumHelper）互补，覆盖无法用枚举表达的值域。
/// 未识别的属性/值返回 null，调用方原样兜底（不崩）。
/// </summary>
public static class DataExchangeValueHelper
{
    /// <summary>英文 Key/枚举名 → 中文（数据工具导出用）；未识别返回 null</summary>
    public static string? ToDisplay(string propertyName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return propertyName switch
        {
            "DataSource" => value is "SCAN" or "MANUAL" ? StringEnumDisplayHelper.GetDataSourceText(value) : null,
            "UsageMode" => value switch { "All" => "全部", "Partial" => "部分", _ => null },
            "ProcessType" => value switch { "Piercing" => "穿孔", _ => null },
            "Module" => value switch { "Order" => "订单", "Batch" => "批次", "WorkOrder" => "工单", _ => null },
            "InspectionItems" => ConvertInspectionItems(value, toKey: false),
            "SourceInspectionItem" => ConvertInspectionItems(value, toKey: false),
            _ => null
        };
    }

    /// <summary>中文 → 英文 Key/枚举名（数据工具导入用）；未识别返回 null</summary>
    public static string? ToKey(string propertyName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return propertyName switch
        {
            "DataSource" => value switch { "扫码" => "SCAN", "手动" => "MANUAL", _ => null },
            "UsageMode" => value switch { "全部" => "All", "部分" => "Partial", _ => null },
            "ProcessType" => value switch { "穿孔" => "Piercing", _ => null },
            "Module" => value switch { "订单" => "Order", "批次" => "Batch", "工单" => "WorkOrder", _ => null },
            "InspectionItems" => ConvertInspectionItems(value, toKey: true),
            "SourceInspectionItem" => ConvertInspectionItems(value, toKey: true),
            _ => null
        };
    }

    /// <summary>逗号分隔的 InspectionItem 枚举名串 ↔ 中文串；无法识别的单项原样保留</summary>
    private static string? ConvertInspectionItems(string value, bool toKey)
    {
        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return null;

        var converted = parts
            .Select(p => EnumHelper.TryParse<InspectionItem>(p) is { } e
                ? (toKey ? e.ToString() : EnumHelper.GetDisplayName(e))
                : p)
            .ToList();
        return string.Join(",", converted);
    }
}
