namespace MES.Core.DTOs.Configuration;

/// <summary>
/// 质量证明书打印配置 DTO（列表页「打印设置」对话框批量保存/加载用）。
/// Key 为唯一锚点（CertificatePrintKeys 之一），Value 为配置值字符串。
/// </summary>
public class CertificatePrintSettingDto
{
    public int Id { get; set; }

    /// <summary>配置键：CertificatePrintKeys 之一</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>配置值（公司名/地址/联系方式/Logo路径/标题/说明文字/字体族名或字号数字）</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>显示名（可改中文）</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>说明</summary>
    public string? Remark { get; set; }
}
