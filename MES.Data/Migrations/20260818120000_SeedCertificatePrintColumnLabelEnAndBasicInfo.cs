using MES.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260818120000_SeedCertificatePrintColumnLabelEnAndBasicInfo")]
    public partial class SeedCertificatePrintColumnLabelEnAndBasicInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 质量证明书打印列布局配置扩展：
            // ① 为既有 3 区块 44 行补英文列名 LabelEn（仅 LabelEn 为空的存量/新种子行，不覆盖用户已填值）；
            // ② 新增「基本信息（BasicInfo）」区块 4 字段（客户名称/产品标准/产品名称/交货状态），
            //    与 CertificatePrintHelper.GetDefaultColumnDefs 默认列一致。幂等，新库/存量库统一生效。

            // === ① 既有 44 行补 LabelEn ===
            migrationBuilder.Sql("""
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Batch No.' WHERE [BlockKey] = N'Material' AND [FieldKey] = N'ProductionBatchNo' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Heat No.' WHERE [BlockKey] = N'Material' AND [FieldKey] = N'HeatNo' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Steel Grade' WHERE [BlockKey] = N'Material' AND [FieldKey] = N'SteelGrade' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Specification' WHERE [BlockKey] = N'Material' AND [FieldKey] = N'Specification' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Length' WHERE [BlockKey] = N'Material' AND [FieldKey] = N'LengthDesc' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Qty' WHERE [BlockKey] = N'Material' AND [FieldKey] = N'Quantity' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Meters' WHERE [BlockKey] = N'Material' AND [FieldKey] = N'Meters' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Weight (kg)' WHERE [BlockKey] = N'Material' AND [FieldKey] = N'Weight' AND [LabelEn] IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Element' WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Element' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Carbon' WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'C' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Silicon' WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Si' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Manganese' WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Mn' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Phosphorus' WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'P' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Sulfur' WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'S' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Nickel' WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Ni' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Chromium' WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Cr' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Molybdenum' WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Mo' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Copper' WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Cu' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Nitrogen' WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'N' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Niobium' WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Nb' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Titanium' WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Ti' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Iron' WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Fe' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Aluminum' WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Al' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Tungsten' WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'W' AND [LabelEn] IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'PMI' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Pmi' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Visual Inspection' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Visual' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Dimension' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Dimension' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Endoscopy' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Endoscopy' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Hydrostatic Test' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Hydro' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Underwater Pressure' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'UnderwaterPneumatic' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Eddy Current' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'EddyCurrent' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Ultrasonic Test' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Ultrasonic' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Port Coloring' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'PortDye' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Tensile Strength' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'TensileStrength' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Yield Rp0.2' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'YieldRp02' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Yield Rp1.0' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'YieldRp10' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Elongation' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Elongation' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Hardness' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Hardness' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Grain Size' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'GrainSize' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Ferrite Content' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Ferrite' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Expanding Test' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Expanding' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Flattening Test' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Flattening' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Intergranular Corrosion' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Intergranular' AND [LabelEn] IS NULL;
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = N'Pitting' WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Pitting' AND [LabelEn] IS NULL;
                """);

            // === ② 新增「基本信息（BasicInfo）」区块 4 字段 ===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'BasicInfo' AND [FieldKey] = N'CustomerName')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [LabelEn], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BasicInfo', N'CustomerName', N'客户名称', N'Customer Name', 1, 1, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'BasicInfo' AND [FieldKey] = N'ProductStandard')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [LabelEn], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BasicInfo', N'ProductStandard', N'产品标准', N'Product Standard', 1, 2, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'BasicInfo' AND [FieldKey] = N'ProductName')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [LabelEn], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BasicInfo', N'ProductName', N'产品名称', N'Product Name', 1, 3, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'BasicInfo' AND [FieldKey] = N'DeliveryStatus')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [LabelEn], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BasicInfo', N'DeliveryStatus', N'交货状态', N'Delivery Status', 1, 4, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 撤销：删除基本信息区块 4 行 + 清空既有 44 行的 LabelEn（CreatedBy=System 种子行）
            migrationBuilder.Sql("""
                DELETE FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'BasicInfo' AND [FieldKey] IN (N'CustomerName', N'ProductStandard', N'ProductName', N'DeliveryStatus') AND [CreatedBy] = N'System';
                UPDATE [CertificatePrintColumnDefinitions] SET [LabelEn] = NULL WHERE [CreatedBy] = N'System' AND [BlockKey] IN (N'Material', N'Chemistry', N'Inspection');
                """);
        }
    }
}
