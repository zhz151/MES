using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Quality;
using MES.Core.Interfaces.Configuration;
using MES.Data;
using MES.Data.Entities.Quality;
using MES.Services.Quality;
using MES.Tests.Tests;
using Moq;
using QuestPDF.Infrastructure;

namespace MES.Tests.Services.Quality;

/// <summary>临时预览生成（验证后删除）</summary>
public class CertificatePreviewTempTests : TestBase
{
    static CertificatePreviewTempTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public async Task GeneratePreviewPdf()
    {
        var ctx = CreateDbContext();
        var cert = new Certificate
        {
            CertificateNo = "SO20240714001-01",
            IssueDate = DateTime.UtcNow,
            CustomerName = "某某石化装备有限公司",
            ProductStandard = "GB/T 14976-2012",
            ProductName = "不锈钢无缝钢管",
            DeliveryStatus = "SolutionAnnealedAndPickled",
            Remark = "本批产品经检验合格，准予出厂。",
            Items = Enumerable.Range(1, 2).Select(i => new CertificateItem
            {
                SeqNo = i,
                InventoryBatchNo = $"INV-{i}",
                ProductionBatchNo = $"BATCH-{i}",
                HeatNo = $"HEAT-{i}",
                SteelGrade = "304",
                Specification = "219×8",
                Quantity = 10 + i,
                Meters = 120m + i,
                Weight = 500m + i,
                TensileStrength_1 = 620,
                TensileStrength_2 = 610,
                YieldRp02_1 = 320,
                YieldRp02_2 = 318,
                Elongation_1 = 45,
                Elongation_2 = 44,
                Hardness_1 = "HRB 85",
                Hardness_2 = "HRB 86",
                GrainSize_1 = "5.0",
                GrainSize_2 = "5.5",
                FerriteContent_1 = 0.5m,
                FerriteContent_2 = 0.6m,
                FlaringResult = "合格",
                FlatteningResult = "合格",
                IntergranularResult = "合格",
                PittingResult = "合格",
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
        ctx.Certificates.Add(cert);
        await ctx.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<CertificateService>>();
        var settingMock = new Mock<ICertificatePrintSettingService>();
        settingMock.Setup(x => x.GetSettingMapAsync())
            .ReturnsAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CompanyName"] = "某某钢管有限公司",
                ["CompanyNameEn"] = "XXX STEEL PIPE CO., LTD.",
                ["CompanyAddress"] = "江苏省某市某工业园区",
                ["CompanyContact"] = "电话：0512-88888888",
                ["HeaderTitle"] = "产品质量证明书",
                ["HeaderTitleEn"] = "PRODUCT QUALITY CERTIFICATE"
            });
        var columnMock = new Mock<ICertificatePrintColumnDefinitionService>();
        columnMock.Setup(x => x.GetConfigMapAsync())
            .ReturnsAsync(new Dictionary<string, CertificatePrintColumnDefinitionDto>(StringComparer.OrdinalIgnoreCase)
            {
                ["Material|Quantity"] = new CertificatePrintColumnDefinitionDto
                {
                    BlockKey = "Material", FieldKey = "Quantity", Label = "支数",
                    Visible = false, ColumnIndex = 6, ColumnWeight = 2
                }
            });
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.WebRootPath).Returns("");
        var svc = new CertificateService(ctx, loggerMock.Object, settingMock.Object, columnMock.Object, envMock.Object);

        var pdfBytes = await svc.PrintFileAsync(new CertificatePrintRequest { Ids = new[] { cert.Id } });

        pdfBytes.Should().NotBeNullOrEmpty();
        await File.WriteAllBytesAsync(@"E:\MES项目\MES\certificate-preview-landscape.pdf", pdfBytes);
    }

    [Fact]
    public async Task GenerateMultiPagePreviewPdf()
    {
        var ctx = CreateDbContext();
        var cert = new Certificate
        {
            CertificateNo = "SO20240714001-MP",
            IssueDate = DateTime.UtcNow,
            CustomerName = "某某石化装备有限公司",
            ProductStandard = "GB/T 14976-2012",
            ProductName = "不锈钢无缝钢管",
            DeliveryStatus = "SolutionAnnealedAndPickled",
            Remark = "多页预览：5 子项 → 第 1 页 4 行 + 第 2 页 1 行（+3 占位空行），每页固定 4 行数据",
            Items = Enumerable.Range(1, 5).Select(i => new CertificateItem
            {
                SeqNo = i,
                InventoryBatchNo = $"INV-MP-{i}",
                ProductionBatchNo = $"BATCH-MP-{i}",
                HeatNo = $"HEAT-MP-{i}",
                SteelGrade = "304",
                Specification = "219×8",
                Quantity = 10 + i,
                Meters = 120m + i,
                Weight = 500m + i,
                TensileStrength_1 = 620,
                TensileStrength_2 = 610,
                YieldRp02_1 = 320,
                YieldRp02_2 = 318,
                Elongation_1 = 45,
                Elongation_2 = 44,
                Hardness_1 = "HRB 85",
                Hardness_2 = "HRB 86",
                GrainSize_1 = "5.0",
                GrainSize_2 = "5.5",
                FerriteContent_1 = 0.5m,
                FerriteContent_2 = 0.6m,
                FlaringResult = "合格",
                FlatteningResult = "合格",
                IntergranularResult = "合格",
                PittingResult = "合格",
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
        ctx.Certificates.Add(cert);
        await ctx.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<CertificateService>>();
        var settingMock = new Mock<ICertificatePrintSettingService>();
        settingMock.Setup(x => x.GetSettingMapAsync())
            .ReturnsAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CompanyName"] = "某某钢管有限公司",
                ["CompanyNameEn"] = "XXX STEEL PIPE CO., LTD.",
                ["CompanyAddress"] = "江苏省某市某工业园区",
                ["CompanyContact"] = "电话：0512-88888888",
                ["HeaderTitle"] = "产品质量证明书",
                ["HeaderTitleEn"] = "PRODUCT QUALITY CERTIFICATE"
            });
        var columnMock = new Mock<ICertificatePrintColumnDefinitionService>();
        columnMock.Setup(x => x.GetConfigMapAsync())
            .ReturnsAsync(new Dictionary<string, CertificatePrintColumnDefinitionDto>(StringComparer.OrdinalIgnoreCase));
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.WebRootPath).Returns("");
        var svc = new CertificateService(ctx, loggerMock.Object, settingMock.Object, columnMock.Object, envMock.Object);

        var pdfBytes = await svc.PrintFileAsync(new CertificatePrintRequest { Ids = new[] { cert.Id } });

        pdfBytes.Should().NotBeNullOrEmpty();
        await File.WriteAllBytesAsync(@"E:\MES项目\MES\certificate-preview-multipage.pdf", pdfBytes);
    }

    [Fact]
    public async Task GenerateDensePreviewPdf()
    {
        // 诊断用例：4 项满内容（长批号/规格 + 全「合格」带英文），验证每页固定 4 行时是否溢出为 2 页
        var ctx = CreateDbContext();
        var cert = new Certificate
        {
            CertificateNo = "SO20240714001-DENSE",
            IssueDate = DateTime.UtcNow,
            CustomerName = "某某石化装备有限公司某某分公司",
            ProductStandard = "GB/T 14976-2012",
            ProductName = "不锈钢无缝钢管",
            DeliveryStatus = "SolutionAnnealedAndPickled",
            Remark = "密集内容诊断：4 子项，长字段值，全部检验项「合格」",
            Items = Enumerable.Range(1, 4).Select(i => new CertificateItem
            {
                SeqNo = i,
                InventoryBatchNo = $"INV-DENSE-20260818-{i}",
                ProductionBatchNo = $"BATCH-20260818-0001-{i}",
                HeatNo = $"HEAT-2026-0618-0{i}",
                SteelGrade = "0Cr18Ni9Ti",
                Specification = "Φ219×8×12000",
                Quantity = 120,
                Meters = 12000m,
                Weight = 1288.5m,
                TensileStrength_1 = 620, TensileStrength_2 = 610,
                YieldRp02_1 = 320, YieldRp02_2 = 318,
                YieldRp10_1 = 300, YieldRp10_2 = 298,
                Elongation_1 = 45, Elongation_2 = 44,
                Hardness_1 = "HRB 85", Hardness_2 = "HRB 86",
                GrainSize_1 = "5.0", GrainSize_2 = "5.5",
                FerriteContent_1 = 0.5m, FerriteContent_2 = 0.6m,
                FlaringResult = "合格", FlatteningResult = "合格",
                IntergranularResult = "合格", PittingResult = "合格",
                InspPMI = "合格", InspVisual = "合格", InspDimension = "合格",
                InspEndoscopy = "合格", InspHydro = "合格", InspUnderwaterPneumatic = "合格",
                InspEddyCurrent = "合格", InspUltrasonic = "合格", InspPortDye = "合格",
                ChemC = 0.045m, ChemSi = 0.45m, ChemMn = 1.2m, ChemP = 0.03m, ChemS = 0.02m,
                ChemNi = 8.2m, ChemCr = 18.2m, ChemMo = 0.1m, ChemCu = 0.2m, ChemN = 0.05m,
                ChemNb = 0.01m, ChemTi = 0.02m, ChemFe = 70m, ChemAl = 0.01m, ChemW = 0.01m
            }).ToList()
        };
        ctx.Certificates.Add(cert);
        await ctx.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<CertificateService>>();
        var settingMock = new Mock<ICertificatePrintSettingService>();
        settingMock.Setup(x => x.GetSettingMapAsync())
            .ReturnsAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // 真库 zhou/MESMN 实际字号配置（复现溢出场景）
                ["CompanyName"] = "某某钢管有限公司",
                ["CompanyNameEn"] = "XXX STEEL PIPE CO., LTD.",
                ["CompanyAddress"] = "江苏省某市某工业园区",
                ["CompanyContact"] = "电话：0512-88888888",
                ["HeaderTitle"] = "产品质量证明书",
                ["HeaderTitleEn"] = "PRODUCT QUALITY CERTIFICATE",
                ["PageFontSize"] = "12",
                ["BasicInfoLabelFontSize"] = "9",
                ["BasicInfoValueFontSize"] = "9",
                ["SectionTitleFontSize"] = "9",
                ["TableHeaderFontSizeDelta"] = "0.1",
                ["MaterialTableFontSize"] = "8",
                ["ChemistryTableFontSize"] = "6.5",
                ["InspectionTableFontSize"] = "5.5",
                ["HeaderFontSize"] = "18",
                ["HeaderTitleEnFontSize"] = "12",
                ["HeaderCompanyNameFontSize"] = "12",
                ["HeaderCompanyNameEnFontSize"] = "8",
                ["FooterTextFontSize"] = "9",
                ["FooterStatementFontSize"] = "9"
            });
        var columnMock = new Mock<ICertificatePrintColumnDefinitionService>();
        columnMock.Setup(x => x.GetConfigMapAsync())
            .ReturnsAsync(new Dictionary<string, CertificatePrintColumnDefinitionDto>(StringComparer.OrdinalIgnoreCase));
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.WebRootPath).Returns("");
        var svc = new CertificateService(ctx, loggerMock.Object, settingMock.Object, columnMock.Object, envMock.Object);

        var pdfBytes = await svc.PrintFileAsync(new CertificatePrintRequest { Ids = new[] { cert.Id } });

        pdfBytes.Should().NotBeNullOrEmpty();
        await File.WriteAllBytesAsync(@"E:\MES项目\MES\certificate-preview-dense.pdf", pdfBytes);
    }

    [Fact]
    public async Task ScanFontSizesForPageFit()
    {
        // 扫描诊断：dense 4 项证书在「物料/化学/检验」不同字号组合下的页数（找出能放下 4 行的最大字号）
        var ctx = CreateDbContext();
        var cert = new Certificate
        {
            CertificateNo = "SO20240714001-SCAN",
            IssueDate = DateTime.UtcNow,
            CustomerName = "某某石化装备有限公司某某分公司",
            ProductStandard = "GB/T 14976-2012",
            ProductName = "不锈钢无缝钢管",
            DeliveryStatus = "SolutionAnnealedAndPickled",
            Remark = "字号扫描",
            Items = Enumerable.Range(1, 4).Select(i => new CertificateItem
            {
                SeqNo = i,
                InventoryBatchNo = $"INV-DENSE-{i}",
                ProductionBatchNo = $"BATCH-20260818-0001-{i}",
                HeatNo = $"HEAT-2026-0618-0{i}",
                SteelGrade = "0Cr18Ni9Ti",
                Specification = "Φ219×8×12000",
                Quantity = 120,
                Meters = 12000m,
                Weight = 1288.5m,
                TensileStrength_1 = 620, TensileStrength_2 = 610,
                YieldRp02_1 = 320, YieldRp02_2 = 318,
                Elongation_1 = 45, Elongation_2 = 44,
                Hardness_1 = "HRB 85", Hardness_2 = "HRB 86",
                GrainSize_1 = "5.0", GrainSize_2 = "5.5",
                FerriteContent_1 = 0.5m, FerriteContent_2 = 0.6m,
                FlaringResult = "合格", FlatteningResult = "合格",
                IntergranularResult = "合格", PittingResult = "合格",
                InspPMI = "合格", InspVisual = "合格", InspDimension = "合格",
                InspEndoscopy = "合格", InspHydro = "合格", InspUnderwaterPneumatic = "合格",
                InspEddyCurrent = "合格", InspUltrasonic = "合格", InspPortDye = "合格"
            }).ToList()
        };
        ctx.Certificates.Add(cert);
        await ctx.SaveChangesAsync();

        var combos = new (string m, string c, string i)[]
        {
            ("9", "9", "9"), ("9", "8.5", "8"), ("9", "8", "7"), ("9", "7.5", "6.5"),
            ("8", "8", "7.5"), ("8", "7.5", "6.5"), ("8", "7", "6"), ("8", "6.5", "5.5")
        };

        var loggerMock = new Mock<ILogger<CertificateService>>();
        var columnMock = new Mock<ICertificatePrintColumnDefinitionService>();
        columnMock.Setup(x => x.GetConfigMapAsync())
            .ReturnsAsync(new Dictionary<string, CertificatePrintColumnDefinitionDto>(StringComparer.OrdinalIgnoreCase));
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.WebRootPath).Returns("");

        foreach (var (m, c, i) in combos)
        {
            var settingMock = new Mock<ICertificatePrintSettingService>();
            settingMock.Setup(x => x.GetSettingMapAsync())
                .ReturnsAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["HeaderTitle"] = "产品质量证明书",
                    ["PageFontSize"] = "12",
                    ["BasicInfoLabelFontSize"] = "9",
                    ["BasicInfoValueFontSize"] = "9",
                    ["SectionTitleFontSize"] = "9",
                    ["TableHeaderFontSizeDelta"] = "0.1",
                    ["MaterialTableFontSize"] = m,
                    ["ChemistryTableFontSize"] = c,
                    ["InspectionTableFontSize"] = i,
                    ["FooterStatementFontSize"] = "9"
                });
            var svc = new CertificateService(ctx, loggerMock.Object, settingMock.Object, columnMock.Object, envMock.Object);
            var pdfBytes = await svc.PrintFileAsync(new CertificatePrintRequest { Ids = new[] { cert.Id } });
            await File.WriteAllBytesAsync($@"E:\MES项目\MES\scan-{m}-{c}-{i}.pdf", pdfBytes);
        }
    }
}
