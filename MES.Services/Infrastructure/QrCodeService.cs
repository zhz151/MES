using MES.Core.Interfaces.Infrastructure;
using MES.Services.Helpers;

namespace MES.Services.Infrastructure;

/// <summary>
/// 二维码生成服务实现：基于 QRCodeHelper（QRCoder）本地生成 PNG，替代前端外部在线二维码服务
/// </summary>
public class QrCodeService : IQrCodeService
{
    /// <summary>
    /// 批量生成二维码 PNG（Base64），空项返回空串保持顺序对齐
    /// </summary>
    public List<string> GenerateQrPngBase64(IReadOnlyList<string> codes)
    {
        var results = new List<string>(codes.Count);
        foreach (var code in codes)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                results.Add(string.Empty);
                continue;
            }
            results.Add(Convert.ToBase64String(QRCodeHelper.GeneratePng(code)));
        }
        return results;
    }
}
