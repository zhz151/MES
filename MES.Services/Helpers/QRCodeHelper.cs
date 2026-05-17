using QRCoder;

namespace MES.Services.Helpers;

/// <summary>
/// 二维码生成帮助类
/// </summary>
public static class QRCodeHelper
{
    private const int DefaultPixelsPerModule = 6;

    /// <summary>
    /// 生成二维码 PNG 字节数组
    /// </summary>
    /// <param name="content">二维码内容</param>
    /// <param name="pixelsPerModule">每个模块的像素数（越大图片越清晰）</param>
    public static byte[] GeneratePng(string content, int pixelsPerModule = DefaultPixelsPerModule)
    {
        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrData);
        return qrCode.GetGraphic(pixelsPerModule);
    }
}
