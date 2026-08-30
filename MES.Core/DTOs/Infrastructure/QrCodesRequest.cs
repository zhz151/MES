namespace MES.Core.DTOs.Infrastructure;

/// <summary>
/// 批量生成二维码请求
/// </summary>
public class QrCodesRequest
{
    /// <summary>
    /// 二维码内容列表（与返回的 base64 图片一一对应）
    /// </summary>
    public List<string> Codes { get; set; } = new();
}
