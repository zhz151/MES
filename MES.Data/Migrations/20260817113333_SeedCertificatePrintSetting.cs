using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedCertificatePrintSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 质量证明书打印配置：13 个键值对（Key→Value），默认值 = CertificatePrintHelper 打印模板硬编码值。
            // 幂等 IF NOT EXISTS 按 Key 锚点插入，新库/存量库统一生效，用户修改过的行不覆盖。
            // 注：SQL Server 排序规则 case-insensitive，Key 唯一索引即锚点。

            // === 页眉：企业信息 ===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'CompanyName')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'CompanyName', N'', N'公司名称', N'页眉左侧企业名称', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'CompanyAddress')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'CompanyAddress', N'', N'公司地址', N'页眉右侧地址', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'CompanyContact')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'CompanyContact', N'', N'联系方式', N'页眉右侧电话/邮箱', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'CompanyLogoPath')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'CompanyLogoPath', N'images/certificate-logo.png', N'Logo图片路径', N'相对后端 wwwroot', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);

            // === 页眉：标题 ===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderTitle')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderTitle', N'产品质量证明书', N'页眉标题', N'页眉中部标题', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);

            // === 页脚 ===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'FooterStatement')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'FooterStatement', N'本质量证明书仅对所列产品批次有效，检测数据真实有效。', N'页脚说明文字', N'页脚第1行', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'FooterRemark')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'FooterRemark', N'备注：', N'页脚备注', N'页脚第2行左', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'SealText')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'SealText', N'质量检验专用章', N'盖章文字', N'页脚第2行中', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'SignerText')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'SignerText', N'签发人：________________', N'签发人', N'页脚第2行右', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);

            // === 字体/字号 ===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'PageFontFamily')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'PageFontFamily', N'SimSun', N'正文字体', N'页面默认字体族', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'PageFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'PageFontSize', N'9', N'正文字号', N'页面默认字号', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderFontFamily')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderFontFamily', N'SimSun', N'标题字体', N'页眉标题字体族', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderFontSize', N'18', N'标题字号', N'页眉标题字号', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 种子数据回退：仅删除由种子写入（CreatedBy=System）的配置行，用户自行修改的行保留
            migrationBuilder.Sql("DELETE FROM [CertificatePrintSettings] WHERE [CreatedBy] = N'System'");
        }
    }
}
