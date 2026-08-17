namespace MES.Data.Entities.Configuration;

/// <summary>
/// 质量证明书打印配置表：页眉企业信息/标题/页脚说明/字体字号键值对（Key 唯一），
/// 数据库全局共享（仿 ProcessCardStyleDefinition 模式）。
/// </summary>
public class CertificatePrintSetting : BaseEntity
{
    /// <summary>配置键：CertificatePrintKeys 之一</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>配置值（公司名/地址/联系方式/Logo路径/标题/说明文字/字体族名或字号数字）</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>显示名（可改中文）</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>说明</summary>
    public string? Remark { get; set; }
}
