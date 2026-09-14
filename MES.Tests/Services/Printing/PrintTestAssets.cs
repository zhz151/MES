using System.IO.Compression;
using System.Text;

namespace MES.Tests.Services.Printing;

/// <summary>
/// 打印排版测试的共用素材与断言帮手：
/// 1) <see cref="CreatePng"/> 手工按 PNG 规范构造真实可解码的图片字节（不依赖外部资源文件与第三方图像库）；
/// 2) <see cref="CountPdfPages"/> 从 QuestPDF 输出字节中统计页数，用于「字段页恒单页 / 照片统一第 2 页」的版式回归断言。
/// </summary>
internal static class PrintTestAssets
{
    /// <summary>构造 width×height 的纯色 PNG（真彩色 8bit + zlib 压缩，符合 PNG 规范，可被 Skia 解码）</summary>
    public static byte[] CreatePng(int width = 16, int height = 12)
    {
        const int bytesPerPixel = 3;
        var rowLength = 1 + width * bytesPerPixel;          // 每行首字节为 filter type(0)
        var raw = new byte[height * rowLength];
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * rowLength;
            raw[rowStart] = 0;                              // filter: None
            for (var x = 0; x < width; x++)
            {
                var p = rowStart + 1 + x * bytesPerPixel;
                raw[p] = 180;                               // R
                raw[p + 1] = 40;                            // G
                raw[p + 2] = 40;                            // B
            }
        }

        var ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, width);
        WriteBigEndian(ihdr, 4, height);
        ihdr[8] = 8;                                        // bit depth
        ihdr[9] = 2;                                        // color type: truecolor

        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });   // PNG 签名

        WriteChunk(ms, "IHDR", ihdr);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(raw, 0, raw.Length);
        WriteChunk(ms, "IDAT", compressed.ToArray());

        WriteChunk(ms, "IEND", Array.Empty<byte>());

        return ms.ToArray();
    }

    /// <summary>
    /// 统计 PDF 页数：按页面对象 <c>/Type /Page</c> 计数（兼容 <c>/Type/Page</c> 无空格写法），
    /// 并以「Page 后紧跟 s」排除页树节点 <c>/Type /Pages</c>。
    /// 说明：QuestPDF 输出的页面字典为明文对象（仅内容流 Flate 压缩），故可直接字节扫描。
    /// </summary>
    public static int CountPdfPages(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var count = 0;
        var idx = 0;

        while (true)
        {
            idx = text.IndexOf("/Type", idx, StringComparison.Ordinal);
            if (idx < 0) break;

            // 值本身是 PDF name（带斜杠），如「/Type /Page」；分隔空白可有可无（「/Type/Page」）
            var p = idx + "/Type".Length;
            while (p < text.Length && (text[p] == ' ' || text[p] == '\r' || text[p] == '\n' || text[p] == '\t'))
                p++;
            if (p < text.Length && text[p] == '/') p++;

            if (p + 4 <= text.Length && string.CompareOrdinal(text, p, "Page", 0, 4) == 0)
            {
                var after = p + 4;
                if (after >= text.Length || text[after] != 's')     // 排除 /Pages
                    count++;
            }

            idx += "/Type".Length;
        }

        return count;
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        WriteBigEndian(length, 0, data.Length);
        stream.Write(length);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);

        Span<byte> crc = stackalloc byte[4];
        WriteBigEndian(crc, 0, unchecked((int)ComputeCrc32(typeBytes, data)));
        stream.Write(crc);
    }

    private static void WriteBigEndian(Span<byte> buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint ComputeCrc32(byte[] first, byte[] second)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var chunk in new[] { first, second })
        {
            foreach (var b in chunk)
            {
                crc ^= b;
                for (var i = 0; i < 8; i++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }
        return crc ^ 0xFFFFFFFFu;
    }
}
