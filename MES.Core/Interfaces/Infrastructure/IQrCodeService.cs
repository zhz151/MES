namespace MES.Core.Interfaces.Infrastructure;

/// <summary>
/// 二维码生成服务（批量，供打印二维码标签使用）
/// </summary>
public interface IQrCodeService
{
    /// <summary>
    /// 批量生成二维码 PNG（Base64），返回顺序与输入 codes 一致
    /// </summary>
    /// <param name="codes">二维码内容列表</param>
    /// <returns>Base64 PNG 字符串列表</returns>
    List<string> GenerateQrPngBase64(IReadOnlyList<string> codes);
}
