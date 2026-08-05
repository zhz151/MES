using MES.Core.Constants;

namespace MES.Blazor.Helpers;

/// <summary>
/// 工段显示辅助：把存储层英文 Key 归一为中文显示。
/// 优先使用配置表加载的 OverrideMap（StandardWorkDays.SectionName，可随配置改名），
/// 否则回退 SectionDefs 规范中文；未知/空值原样返回（不崩）。
/// MainLayout 启动时经 StandardWorkDayService.GetSectionNameMapAsync() 填充 OverrideMap。
/// </summary>
public static class SectionDisplayHelper
{
    /// <summary>Key → 显示中文（由配置表加载，全局共享）</summary>
    public static Dictionary<string, string>? OverrideMap { get; set; }

    /// <summary>工段 Key/中文 → 显示中文</summary>
    public static string GetSectionNameText(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        if (OverrideMap != null && OverrideMap.TryGetValue(value, out var cn))
            return cn;
        return SectionKeys.ToChinese(value) ?? value;
    }

    /// <summary>工段中文/别名 → 稳定 Key（提交层用）；未知返回 null</summary>
    public static string? GetSectionKey(string? nameOrKey) => SectionKeys.ToKey(nameOrKey);
}
