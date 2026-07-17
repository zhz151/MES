using MES.Core.DTOs.Shared;
using MES.Blazor.Services;

namespace MES.Blazor.Helpers;

/// <summary>
/// ColumnDef 扩展方法
/// </summary>
public static class ColumnDefExtensions
{
    /// <summary>
    /// 将列定义转换为打印列定义，自动解析 Width（"80px" → 80）
    /// </summary>
    public static PrintColumnDef ToPrintColumnDef(this ColumnDef c)
    {
        int? width = null;
        if (!string.IsNullOrEmpty(c.Width))
        {
            var cleaned = c.Width.TrimEnd('p', 'x'); // "80px" → "80"
            if (int.TryParse(cleaned, out var w))
                width = w;
        }
        return new PrintColumnDef
        {
            Key = c.Key,
            Label = c.Label,
            Width = width
        };
    }
}
