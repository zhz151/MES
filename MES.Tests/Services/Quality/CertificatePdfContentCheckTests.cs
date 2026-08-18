using System.IO.Compression;
using System.Text.RegularExpressions;
using FluentAssertions;
using MES.Data.Entities.Quality;
using MES.Services.Printing;
using MES.Tests.Tests;
using QuestPDF.Infrastructure;

namespace MES.Tests.Services.Quality;

/// <summary>
/// 质量证明书多页 PDF 回归守卫：验证「基本信息组每页均重复」。
/// QuestPDF 对所有文本（含 ASCII）使用字形索引（非 Unicode），无法直接匹配文本内容，
/// 故用「按页统计文本对象 Tj 数量」代理验证——若基本信息组每页重复，两页内容结构对称
/// （差异仅数据行数：第 1 页 4 行 / 第 2 页 1 行+3 空行），第 2 页 Tj 应与第 1 页相近。
/// 实测依据（2026-08-18 对照验证）：旧版「仅第一页有基本信息」第 2 页 Tj=331 / 第 1 页 534（0.620）；
/// 新版「每页有基本信息」第 2 页 Tj=357 / 第 1 页 534（0.669），差 26 ≈ 基本信息组文本对象数。
/// </summary>
public class CertificatePdfContentCheckTests : TestBase
{
    static CertificatePdfContentCheckTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static int Find(byte[] data, byte[] pattern, int from)
    {
        for (int i = from; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    private static string? TryInflate(byte[] block)
    {
        try
        {
            using var ms = new MemoryStream(block);
            using var zlib = new ZLibStream(ms, CompressionMode.Decompress);
            using var reader = new StreamReader(zlib);
            return reader.ReadToEnd();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>按 /Contents 引用定位每个物理页的内容流并解压（返回 页号 → 内容文本）</summary>
    private static List<string> ExtractPageContentStreams(byte[] pdfBytes)
    {
        var ascii = System.Text.Encoding.ASCII.GetString(pdfBytes);
        var streamStart = System.Text.Encoding.ASCII.GetBytes("stream");
        var streamEnd = System.Text.Encoding.ASCII.GetBytes("endstream");

        var refs = Regex.Matches(ascii, @"/Contents (\d+) 0 R")
            .Select(m => int.Parse(m.Groups[1].Value))
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var pages = new List<string>();
        foreach (var r in refs)
        {
            var marker = System.Text.Encoding.ASCII.GetBytes($"{r} 0 obj");
            int objStart = Find(pdfBytes, marker, 0);
            if (objStart < 0) continue;
            int s = Find(pdfBytes, streamStart, objStart);
            if (s < 0) continue;
            int contentStart = s + streamStart.Length;
            if (pdfBytes[contentStart] == '\r') contentStart++;
            if (contentStart < pdfBytes.Length && pdfBytes[contentStart] == '\n') contentStart++;
            int e = Find(pdfBytes, streamEnd, contentStart);
            if (e < 0) continue;
            var content = TryInflate(pdfBytes[contentStart..e]);
            if (content != null) pages.Add(content);
        }
        return pages;
    }

    private static Certificate BuildCert(int itemCount)
    {
        var cert = new Certificate
        {
            CertificateNo = "SO20240714001-REG",
            IssueDate = DateTime.UtcNow,
            CustomerName = "某某石化装备有限公司",
            ProductStandard = "GB/T 14976-2012",
            ProductName = "不锈钢无缝钢管",
            DeliveryStatus = "SolutionAnnealedAndPickled",
            Remark = "多页回归：基本信息组每页均重复",
            Items = Enumerable.Range(1, itemCount).Select(i => new CertificateItem
            {
                SeqNo = i,
                ProductionBatchNo = $"BATCH-{i}",
                HeatNo = $"HEAT-{i}",
                SteelGrade = "304",
                Specification = "219×8",
                Quantity = 10 + i,
                Meters = 120m + i,
                Weight = 500m + i,
                InspPMI = "合格",
                InspVisual = "合格",
                InspDimension = "合格",
                InspEndoscopy = "合格",
                InspHydro = "合格",
                InspUnderwaterPneumatic = "合格",
                InspEddyCurrent = "合格",
                InspUltrasonic = "合格",
                InspPortDye = "合格"
            }).ToList()
        };
        return cert;
    }

    [Fact]
    public void MultipagePdfBasicInfoRepeatsOnEveryPage()
    {
        // 5 子项 → 每页 4 行 → 2 页；每页均应含基本信息组（两页 Tj 数相近）
        var pdfBytes = CertificatePrintHelper.GeneratePdf(
            new List<Certificate> { BuildCert(5) },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var pages = ExtractPageContentStreams(pdfBytes);
        pages.Count.Should().Be(2, "5 子项按每页 4 行应拆为 2 页");

        var tjCounts = pages.Select(p => Regex.Matches(p, @"Tj|TJ").Count).ToList();
        // 旧版（仅第一页有基本信息）：第 2 页 / 第 1 页 = 331/534 ≈ 0.620；
        // 新版（每页有基本信息）：357/534 ≈ 0.669 → 阈值取 0.64，低于则说明第二页缺少基本信息组文本
        tjCounts[0].Should().BeGreaterThan(0);
        (tjCounts[1] / (double)tjCounts[0]).Should().BeGreaterThan(0.64,
            $"基本信息组每页重复 → 第二页 Tj 应与第一页相近；实际两页 Tj 数 {string.Join(" / ", tjCounts)}");
    }
}
