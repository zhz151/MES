using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Quality;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Configuration;
using MES.Data;
using MES.Data.Entities.Quality;
using MES.Services.Quality;
using MES.Tests.Tests;
using Moq;
using QuestPDF.Infrastructure;

namespace MES.Tests.Services.Quality;

/// <summary>
/// 质量证明书服务打印测试：按 Id 集合生成质量证明书 PDF（含子项），空/不存在 Id 抛业务异常。
/// </summary>
public class CertificateServiceTests : TestBase
{
    static CertificateServiceTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private CertificateService CreateService(AppDbContext ctx, Dictionary<string, string>? settingMap = null,
        Dictionary<string, CertificatePrintColumnDefinitionDto>? columnConfigMap = null)
    {
        var loggerMock = new Mock<ILogger<CertificateService>>();

        var settingMock = new Mock<ICertificatePrintSettingService>();
        settingMock.Setup(x => x.GetSettingMapAsync())
            .ReturnsAsync(settingMap ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var columnMock = new Mock<ICertificatePrintColumnDefinitionService>();
        columnMock.Setup(x => x.GetConfigMapAsync())
            .ReturnsAsync(columnConfigMap ?? new Dictionary<string, CertificatePrintColumnDefinitionDto>(StringComparer.OrdinalIgnoreCase));

        // WebRootPath 为空 → PrintFileAsync 跳过 Logo 读取，优雅降级为无 Logo
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.WebRootPath).Returns("");

        return new CertificateService(ctx, loggerMock.Object, settingMock.Object, columnMock.Object, envMock.Object);
    }

    private static async Task<Certificate> SeedCertificateAsync(AppDbContext ctx, string certificateNo, int itemCount = 1)
    {
        var cert = new Certificate
        {
            CertificateNo = certificateNo,
            IssueDate = DateTime.UtcNow,
            CustomerName = "测试客户",
            ProductStandard = "GB/T 14976-2012",
            ProductName = "无缝钢管",
            DeliveryStatus = "SolutionAnnealedAndPickled",
            Remark = "测试备注",
            Items = Enumerable.Range(1, itemCount).Select(i => new CertificateItem
            {
                SeqNo = i,
                InventoryBatchNo = $"INV-{i}",
                ProductionBatchNo = $"BATCH-{i}",
                HeatNo = $"HEAT-{i}",
                SteelGrade = "304",
                Specification = "219×8",
                Quantity = 10,
                Meters = 120m,
                Weight = 500m
            }).ToList()
        };
        ctx.Certificates.Add(cert);
        await ctx.SaveChangesAsync();
        return cert;
    }

    // ========== PrintFileAsync ==========

    [Fact]
    public async Task PrintFileAsync_按Id生成PDF_含子项()
    {
        var ctx = CreateDbContext();
        var cert = await SeedCertificateAsync(ctx, "SO20240714001-01", itemCount: 2);
        var svc = CreateService(ctx);

        var pdfBytes = await svc.PrintFileAsync(new CertificatePrintRequest { Ids = new[] { cert.Id } });

        pdfBytes.Should().NotBeNull();
        pdfBytes.Should().NotBeEmpty();
        pdfBytes[0].Should().Be((byte)'%');
        pdfBytes[1].Should().Be((byte)'P');
        pdfBytes[2].Should().Be((byte)'D');
        pdfBytes[3].Should().Be((byte)'F');
    }

    [Fact]
    public async Task PrintFileAsync_多证书_生成PDF()
    {
        var ctx = CreateDbContext();
        var cert1 = await SeedCertificateAsync(ctx, "SO20240714001-01");
        var cert2 = await SeedCertificateAsync(ctx, "SO20240714001-02");
        var svc = CreateService(ctx);

        var pdfBytes = await svc.PrintFileAsync(new CertificatePrintRequest { Ids = new[] { cert1.Id, cert2.Id } });

        pdfBytes.Should().NotBeNull();
        pdfBytes.Should().NotBeEmpty();
        pdfBytes[0].Should().Be((byte)'%');
    }

    [Fact]
    public async Task PrintFileAsync_配置映射提供_Logo缺失降级生成PDF()
    {
        var ctx = CreateDbContext();
        var cert = await SeedCertificateAsync(ctx, "SO20240714001-01");
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CompanyName"] = "某某钢管有限公司",
            ["HeaderTitle"] = "产品质量证明书",
            ["CompanyLogoPath"] = "images/not-exist.png"
        };
        var svc = CreateService(ctx, settings);

        // Logo 文件不存在：优雅降级（WebRootPath 空），PDF 正常生成
        var pdfBytes = await svc.PrintFileAsync(new CertificatePrintRequest { Ids = new[] { cert.Id } });

        pdfBytes.Should().NotBeNull();
        pdfBytes.Should().NotBeEmpty();
        pdfBytes[0].Should().Be((byte)'%');
    }

    [Fact]
    public async Task PrintFileAsync_Ids为空_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await FluentActions.Invoking(() => svc.PrintFileAsync(new CertificatePrintRequest { Ids = Array.Empty<int>() }))
            .Should().ThrowAsync<BusinessException>()
            .WithMessage("*请选择要打印的质量证明书*");
    }

    [Fact]
    public async Task PrintFileAsync_Ids不存在_抛业务异常()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        await FluentActions.Invoking(() => svc.PrintFileAsync(new CertificatePrintRequest { Ids = new[] { 99999 } }))
            .Should().ThrowAsync<BusinessException>()
            .WithMessage("*未找到选中的质量证明书*");
    }

    [Fact]
    public async Task PrintFileAsync_列配置覆盖_隐藏列生成PDF()
    {
        var ctx = CreateDbContext();
        var cert = await SeedCertificateAsync(ctx, "SO20240714001-01", itemCount: 2);

        // 配置覆盖：物料信息「支数」列隐藏 + 「炉号」重命名，打印按配置渲染不崩溃
        var configMap = new Dictionary<string, CertificatePrintColumnDefinitionDto>(StringComparer.OrdinalIgnoreCase)
        {
            ["Material|Quantity"] = new CertificatePrintColumnDefinitionDto
            {
                BlockKey = "Material", FieldKey = "Quantity", Label = "支数",
                Visible = false, ColumnIndex = 6, ColumnWeight = 2
            },
            ["Material|HeatNo"] = new CertificatePrintColumnDefinitionDto
            {
                BlockKey = "Material", FieldKey = "HeatNo", Label = "熔炼炉号",
                Visible = true, ColumnIndex = 2, ColumnWeight = 4
            }
        };
        var svc = CreateService(ctx, columnConfigMap: configMap);

        var pdfBytes = await svc.PrintFileAsync(new CertificatePrintRequest { Ids = new[] { cert.Id } });

        pdfBytes.Should().NotBeNull();
        pdfBytes.Should().NotBeEmpty();
        pdfBytes[0].Should().Be((byte)'%');
    }
}
