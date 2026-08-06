namespace MES.Core.Helpers;

/// <summary>
/// 字符串存储的非 C# 枚举显示统一出口（前后端共享，替代各打印 Helper/前端散落 switch）。
/// 目前承载 DataSource（SCAN→扫码 / MANUAL→手动）。空值返回空串，未知值原样返回（不崩）。
/// </summary>
public static class StringEnumDisplayHelper
{
    /// <summary>
    /// 数据来源类型中文显示：SCAN→扫码, MANUAL→手动，其余原样返回。
    /// </summary>
    public static string GetDataSourceText(string? dataSource) => dataSource switch
    {
        "SCAN" => "扫码",
        "MANUAL" => "手动",
        _ => dataSource ?? ""
    };
}
