namespace MES.Services;

/// <summary>
/// 规格字符串解析工具类（从 "外径*壁厚" 格式中提取数值）
/// 解析失败返回 null 而非 0，避免下游静默除零
/// </summary>
public static class SpecificationParser
{
    /// <summary>
    /// 从规格字符串解析外径
    /// </summary>
    public static decimal? ParseOuterDiameter(string specification)
    {
        if (string.IsNullOrEmpty(specification))
            return null;

        var parts = specification.Split('*');
        if (parts.Length > 0 && decimal.TryParse(parts[0], out var od))
            return od;

        return null;
    }

    /// <summary>
    /// 从规格字符串解析壁厚
    /// </summary>
    public static decimal? ParseWallThickness(string specification)
    {
        if (string.IsNullOrEmpty(specification))
            return null;

        var parts = specification.Split('*');
        if (parts.Length > 1 && decimal.TryParse(parts[1], out var wt))
            return wt;

        return null;
    }
}
