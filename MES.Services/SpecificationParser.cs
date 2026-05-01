namespace MES.Services;

/// <summary>
/// 规格字符串解析工具类（从 "外径*壁厚" 格式中提取数值）
/// </summary>
public static class SpecificationParser
{
    /// <summary>
    /// 从规格字符串解析外径
    /// </summary>
    public static decimal ParseOuterDiameter(string specification)
    {
        if (string.IsNullOrEmpty(specification))
            return 0;

        var parts = specification.Split('*');
        if (parts.Length > 0 && decimal.TryParse(parts[0], out var od))
            return od;

        return 0;
    }

    /// <summary>
    /// 从规格字符串解析壁厚
    /// </summary>
    public static decimal ParseWallThickness(string specification)
    {
        if (string.IsNullOrEmpty(specification))
            return 0;

        var parts = specification.Split('*');
        if (parts.Length > 1 && decimal.TryParse(parts[1], out var wt))
            return wt;

        return 0;
    }
}
