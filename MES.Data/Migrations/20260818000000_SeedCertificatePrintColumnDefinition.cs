using MES.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260818000000_SeedCertificatePrintColumnDefinition")]
    public partial class SeedCertificatePrintColumnDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 质量证明书打印列布局配置：为 3 个区块（Material 物料信息 / Chemistry 化学成分 / Inspection 检验检测）种子默认列定义，
            // 默认值 = 用户定稿的列宽权重方案（ColumnIndex 区块内排序键 / ColumnWeight 列宽权重，Visible 全启用）。
            // 新库走 DbInitializer 种子，存量库（已存在任何配置行）不触发，故此处补数据迁移（幂等 IF NOT EXISTS）。

            // === 物料信息（8）===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Material' AND [FieldKey] = N'ProductionBatchNo')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Material', N'ProductionBatchNo', N'生产批号', 1, 1, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Material' AND [FieldKey] = N'HeatNo')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Material', N'HeatNo', N'炉号', 1, 2, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Material' AND [FieldKey] = N'SteelGrade')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Material', N'SteelGrade', N'牌号', 1, 3, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Material' AND [FieldKey] = N'Specification')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Material', N'Specification', N'规格', 1, 4, 4, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Material' AND [FieldKey] = N'LengthDesc')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Material', N'LengthDesc', N'长度', 1, 5, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Material' AND [FieldKey] = N'Quantity')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Material', N'Quantity', N'支数', 1, 6, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Material' AND [FieldKey] = N'Meters')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Material', N'Meters', N'米数', 1, 7, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Material' AND [FieldKey] = N'Weight')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Material', N'Weight', N'重量(kg)', 1, 8, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);

            // === 化学成分（16 = 元素 + C~W 15 元素）===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Element')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Chemistry', N'Element', N'元素', 1, 1, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'C')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Chemistry', N'C', N'C', 1, 2, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Si')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Chemistry', N'Si', N'Si', 1, 3, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Mn')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Chemistry', N'Mn', N'Mn', 1, 4, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'P')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Chemistry', N'P', N'P', 1, 5, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'S')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Chemistry', N'S', N'S', 1, 6, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Ni')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Chemistry', N'Ni', N'Ni', 1, 7, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Cr')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Chemistry', N'Cr', N'Cr', 1, 8, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Mo')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Chemistry', N'Mo', N'Mo', 1, 9, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Cu')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Chemistry', N'Cu', N'Cu', 1, 10, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'N')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Chemistry', N'N', N'N', 1, 11, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Nb')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Chemistry', N'Nb', N'Nb', 1, 12, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Ti')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Chemistry', N'Ti', N'Ti', 1, 13, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Fe')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Chemistry', N'Fe', N'Fe', 1, 14, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'Al')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Chemistry', N'Al', N'Al', 1, 15, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Chemistry' AND [FieldKey] = N'W')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Chemistry', N'W', N'W', 1, 16, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);

            // === 检验检测（20 = 成品检验 9 + 理化检测 11）===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Pmi')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'Pmi', N'PMI', 1, 1, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Visual')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'Visual', N'表检', 1, 2, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Dimension')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'Dimension', N'尺寸', 1, 3, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Endoscopy')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'Endoscopy', N'内窥', 1, 4, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Hydro')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'Hydro', N'水压', 1, 5, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'UnderwaterPneumatic')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'UnderwaterPneumatic', N'水下气压', 1, 6, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'EddyCurrent')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'EddyCurrent', N'涡流', 1, 7, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Ultrasonic')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'Ultrasonic', N'超声波', 1, 8, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'PortDye')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'PortDye', N'端口着色', 1, 9, 2, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'TensileStrength')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'TensileStrength', N'抗拉强度', 1, 10, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'YieldRp02')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'YieldRp02', N'屈服Rp0.2', 1, 11, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'YieldRp10')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'YieldRp10', N'屈服Rp1.0', 1, 12, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Elongation')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'Elongation', N'伸长率', 1, 13, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Hardness')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'Hardness', N'硬度', 1, 14, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'GrainSize')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'GrainSize', N'晶粒度', 1, 15, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Ferrite')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'Ferrite', N'铁素体', 1, 16, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Expanding')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'Expanding', N'扩口', 1, 17, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Flattening')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'Flattening', N'压扁', 1, 18, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Intergranular')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'Intergranular', N'晶间腐蚀', 1, 19, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintColumnDefinitions] WHERE [BlockKey] = N'Inspection' AND [FieldKey] = N'Pitting')
                    INSERT INTO [CertificatePrintColumnDefinitions] ([BlockKey], [FieldKey], [Label], [Visible], [ColumnIndex], [ColumnWeight], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'Inspection', N'Pitting', N'点蚀', 1, 20, 3, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 撤销：删除本迁移种子的质量证明书打印列布局配置（仅限 CreatedBy=System 的种子行）
            migrationBuilder.Sql("""
                DELETE FROM [CertificatePrintColumnDefinitions] WHERE [CreatedBy] = N'System';
                """);
        }
    }
}
