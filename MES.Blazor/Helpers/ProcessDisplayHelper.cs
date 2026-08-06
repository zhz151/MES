using MES.Core.Constants;

namespace MES.Blazor.Helpers;

/// <summary>
/// 工序组显示辅助：把存储层英文 Key 归一为中文显示。
/// 优先使用配置表加载的 OverrideMap（ProcessDefinition.ProcessName，可随配置改名），
/// 否则回退 ProcessNames 规范中文；未知/空值原样返回（不崩）。
/// MainLayout 启动时经 ProcessDefinitionService.GetProcessNameMapAsync() 填充 OverrideMap。
/// </summary>
public static class ProcessDisplayHelper
{
    /// <summary>Key → 显示中文（由配置表加载，全局共享）</summary>
    public static Dictionary<string, string>? OverrideMap { get; set; }

    /// <summary>工序 Key/中文 → 显示中文</summary>
    public static string GetProcessNameText(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        if (OverrideMap != null && OverrideMap.TryGetValue(value, out var cn))
            return cn;
        return ProcessKeys.ToChinese(value) ?? value;
    }

    /// <summary>工序中文/别名 → 稳定 Key（提交层用）；未知返回 null</summary>
    public static string? GetProcessKey(string? nameOrKey) => ProcessKeys.ToKey(nameOrKey);
}
