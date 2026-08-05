namespace MES.Core.Interfaces.Configuration;

/// <summary>
/// 工段显示名解析服务：Key（英文，存储）↔ 中文（显示）双向转换。
/// 存储层与后端匹配使用英文 Key；显示层使用中文（优先配置表 StandardWorkDays.SectionName，
/// 兜底 SectionDefs 规范中文）。
/// </summary>
public interface ISectionNameDisplayService
{
    /// <summary>获取 Key → 显示中文 映射（配置表优先，叠合 SectionDefs 兜底 26 键）</summary>
    Task<IReadOnlyDictionary<string, string>> GetSectionNameMapAsync();

    /// <summary>归一为显示中文：Key → 中文；已是中文（迁移前存量）原样返回；未知返回 null</summary>
    Task<string?> ToDisplayAsync(string? keyOrName);

    /// <summary>归一为稳定 Key：已是 Key 原样返回；中文/别名反查；未知返回 null</summary>
    Task<string?> ToKeyAsync(string? nameOrKey);
}
