using FluentAssertions;
using MES.Services.Helpers;

namespace MES.Tests.Services.Helpers;

/// <summary>
/// 二维码生成帮助类 QRCodeHelper 冒烟测试：
/// 输出非空且为合法 PNG（签名头）、同内容确定性、像素密度参数、中文与长内容可生成。
/// </summary>
public class QRCodeHelperTests
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static void AssertPng(byte[] bytes)
    {
        bytes.Should().NotBeNullOrEmpty();
        bytes.Length.Should().BeGreaterThan(PngSignature.Length);
        bytes.Take(PngSignature.Length).Should().Equal(PngSignature);
    }

    [Fact]
    public void GeneratePng_返回非空PNG字节()
    {
        var bytes = QRCodeHelper.GeneratePng("http://localhost:5001/scan?batch=TEST-001");

        AssertPng(bytes);
    }

    [Fact]
    public void GeneratePng_相同内容_输出确定()
    {
        var a = QRCodeHelper.GeneratePng("BATCH-001");
        var b = QRCodeHelper.GeneratePng("BATCH-001");

        a.Should().Equal(b);
    }

    [Fact]
    public void GeneratePng_像素密度参数_生成更大图仍为PNG()
    {
        var bytes = QRCodeHelper.GeneratePng("BATCH-001", pixelsPerModule: 20);

        AssertPng(bytes);
    }

    [Fact]
    public void GeneratePng_中文与超长内容_可生成()
    {
        var bytes = QRCodeHelper.GeneratePng("批次号：SC20260904001；牌号：304；规格：Φ57×3 不锈钢无缝管");

        AssertPng(bytes);
    }
}
