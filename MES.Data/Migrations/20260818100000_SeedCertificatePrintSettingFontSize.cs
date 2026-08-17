using MES.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260818100000_SeedCertificatePrintSettingFontSize")]
    public partial class SeedCertificatePrintSettingFontSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 质量证明书打印配置：补充 15 个细分子号键（页眉 6 / 内容 7 / 页脚 2），
            // 默认值 = 原 CertificatePrintHelper 模板写死/派生值（消除死编码，全部由用户配置决定）。
            // 幂等 IF NOT EXISTS 按 Key 锚点插入，新库/存量库统一生效，用户修改过的行不覆盖。

            // === 页眉：细分子号 ===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderCompanyNameFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderCompanyNameFontSize', N'14', N'公司名称字号', N'页眉左侧企业名称', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderCompanyNameEnFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderCompanyNameEnFontSize', N'9', N'公司名称英文字号', N'页眉企业英文名称', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderAddressFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderAddressFontSize', N'8', N'公司地址字号', N'页眉右侧地址', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderAddressEnFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderAddressEnFontSize', N'8', N'公司地址英文字号', N'页眉右侧地址英文', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderContactFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderContactFontSize', N'8', N'联系方式字号', N'页眉右侧电话/邮箱', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderTitleEnFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderTitleEnFontSize', N'13', N'页眉英文标题字号', N'中文标题下方英文', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);

            // === 内容：细分子号 ===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'BasicInfoLabelFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BasicInfoLabelFontSize', N'7', N'基本信息标签字号', N'标签小字在值上方', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'BasicInfoValueFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BasicInfoValueFontSize', N'9', N'基本信息值字号', N'客户/标准/名称/交货状态等值', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'SectionTitleFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'SectionTitleFontSize', N'10', N'区块标题字号', N'基本信息+三张明细表标题条', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'MaterialTableFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'MaterialTableFontSize', N'8.5', N'物料信息表字号', N'物料信息表内容', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'ChemistryTableFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ChemistryTableFontSize', N'6.5', N'化学成分表字号', N'16列宽表内容', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'InspectionTableFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'InspectionTableFontSize', N'5.5', N'检验检测表字号', N'21列宽表内容', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'TableHeaderFontSizeDelta')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'TableHeaderFontSizeDelta', N'0.5', N'表头字号增量', N'表头=表内容字号+此增量', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);

            // === 页脚：细分子号 ===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'FooterStatementFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'FooterStatementFontSize', N'8', N'页脚说明字号', N'页脚第1行', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'FooterTextFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'FooterTextFontSize', N'8', N'页脚三栏字号', N'备注/盖章/签发人', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回退：仅删除本迁移种子写入的 15 个字号键（CreatedBy=System 且 Key 在本迁移范围内），用户修改的行保留
            migrationBuilder.Sql("""
                DELETE FROM [CertificatePrintSettings]
                WHERE [CreatedBy] = N'System' AND [Key] IN (
                    N'HeaderCompanyNameFontSize', N'HeaderCompanyNameEnFontSize',
                    N'HeaderAddressFontSize', N'HeaderAddressEnFontSize', N'HeaderContactFontSize',
                    N'HeaderTitleEnFontSize',
                    N'BasicInfoLabelFontSize', N'BasicInfoValueFontSize', N'SectionTitleFontSize',
                    N'MaterialTableFontSize', N'ChemistryTableFontSize', N'InspectionTableFontSize',
                    N'TableHeaderFontSizeDelta',
                    N'FooterStatementFontSize', N'FooterTextFontSize'
                )
                """);
        }
    }
}
