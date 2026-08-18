using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedCertificatePrintSettingsActualValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 质量证明书打印配置：按真库（zhou/MESMN）已设定的实际数据填入种子（用户要求）。
            // 幂等 upsert 实际值策略（用户确认）：
            //  ① 缺失键（本迁移新增的 3 个英文键：CompanyNameEn/CompanyAddressEn/HeaderTitleEn）→ IF NOT EXISTS INSERT；
            //  ② 已存在键且从未被用户修改（CreatedBy=System AND UpdatedBy=System）→ 更新为真库实际值；
            //  ③ 已被用户修改（CreatedBy≠System 或 UpdatedBy≠System）→ 不覆盖。
            // 真库执行效果：所有行值不变（System/System 行值已=实际值，用户行不覆盖），幂等安全。

            // === 页眉：企业信息 ===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'CompanyName')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'CompanyName', N'XXX市XXX不锈钢管有限公司', N'公司名称', N'页眉左侧企业名称', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'XXX市XXX不锈钢管有限公司', [DisplayName] = N'公司名称', [Remark] = N'页眉左侧企业名称', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'CompanyName' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'CompanyNameEn')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'CompanyNameEn', N'XXX XXX Stainless Steel Pipe Co.,Ltd', N'公司名称（英文）', N'页眉左侧企业英文名称（中文下方第二行）', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'XXX XXX Stainless Steel Pipe Co.,Ltd', [DisplayName] = N'公司名称（英文）', [Remark] = N'页眉左侧企业英文名称（中文下方第二行）', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'CompanyNameEn' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'CompanyAddress')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'CompanyAddress', N'地址：XXX市XXXXXX路 12 号', N'公司地址', N'页眉右侧地址', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'地址：XXX市XXXXXX路 12 号', [DisplayName] = N'公司地址', [Remark] = N'页眉右侧地址', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'CompanyAddress' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'CompanyAddressEn')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'CompanyAddressEn', N'Add：XXXXXXXX', N'公司地址（英文）', N'页眉右侧地址英文（中文下方第二行）', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'Add：XXXXXXXX', [DisplayName] = N'公司地址（英文）', [Remark] = N'页眉右侧地址英文（中文下方第二行）', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'CompanyAddressEn' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'CompanyContact')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'CompanyContact', N'电话/Tel: 86-111-1111111111', N'联系方式', N'页眉右侧电话/邮箱', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'电话/Tel: 86-111-1111111111', [DisplayName] = N'联系方式', [Remark] = N'页眉右侧电话/邮箱', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'CompanyContact' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'CompanyLogoPath')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'CompanyLogoPath', N'images/certificate-logo.png', N'Logo图片路径', N'相对后端 wwwroot', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'images/certificate-logo.png', [DisplayName] = N'Logo图片路径', [Remark] = N'相对后端 wwwroot', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'CompanyLogoPath' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                """);

            // === 页眉：标题 ===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderTitle')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderTitle', N'产品质量证明书', N'页眉标题', N'页眉中部标题', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'产品质量证明书', [DisplayName] = N'页眉标题', [Remark] = N'页眉中部标题', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'HeaderTitle' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderTitleEn')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderTitleEn', N'PRODUCT QUALITY CERTIFICATE', N'页眉标题（英文）', N'页眉中部标题英文（中文下方第二行）', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'PRODUCT QUALITY CERTIFICATE', [DisplayName] = N'页眉标题（英文）', [Remark] = N'页眉中部标题英文（中文下方第二行）', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'HeaderTitleEn' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                """);

            // === 页脚 ===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'FooterStatement')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'FooterStatement', N'1. 特此证明，我所生产的材料已按照规范要求和产品标准等级要求进行了检验和试验，结果合格。We hereby certify that the materials produced have been inspected and tested in accordance with the association''s regulations and standard grade requirements, and the results are satisfactory.  2. 本"产品质量证明书"须盖有"产品质量专用章"有效，除非有本公司的书面批准，否则证书不允许复制。This "Product Quality Certificate" shall be issued with a "Product Quality Seal" valid, the certificate is not allowed to be reproduced unless approved in writing by the Company.', N'页脚说明文字', N'页脚第1行', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'1. 特此证明，我所生产的材料已按照规范要求和产品标准等级要求进行了检验和试验，结果合格。We hereby certify that the materials produced have been inspected and tested in accordance with the association''s regulations and standard grade requirements, and the results are satisfactory.  2. 本"产品质量证明书"须盖有"产品质量专用章"有效，除非有本公司的书面批准，否则证书不允许复制。This "Product Quality Certificate" shall be issued with a "Product Quality Seal" valid, the certificate is not allowed to be reproduced unless approved in writing by the Company.', [DisplayName] = N'页脚说明文字', [Remark] = N'页脚第1行', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'FooterStatement' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'FooterRemark')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'FooterRemark', N'备注Note：', N'页脚备注', N'页脚第2行左', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'备注Note：', [DisplayName] = N'页脚备注', [Remark] = N'页脚第2行左', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'FooterRemark' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'SealText')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'SealText', N'质量检验专用章', N'盖章文字', N'页脚第2行中', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'质量检验专用章', [DisplayName] = N'盖章文字', [Remark] = N'页脚第2行中', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'SealText' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'SealTextEn')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'SealTextEn', N'QUALITY INSPECTION SEAL', N'盖章文字（英文）', N'页脚第2行中第2行', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'QUALITY INSPECTION SEAL', [DisplayName] = N'盖章文字（英文）', [Remark] = N'页脚第2行中第2行', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'SealTextEn' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'SignerText')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'SignerText', N'工程师：', N'签发工程师', N'页脚第2行右第2行', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'工程师：', [DisplayName] = N'签发工程师', [Remark] = N'页脚第2行右第2行', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'SignerText' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'InspectorText')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'InspectorText', N'检验员：________________', N'检验员签字', N'页脚第2行右第1行', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'检验员：________________', [DisplayName] = N'检验员签字', [Remark] = N'页脚第2行右第1行', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'InspectorText' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                """);

            // === 字体/字号 ===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'PageFontFamily')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'PageFontFamily', N'SimSun', N'正文字体', N'页面默认字体族', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'SimSun', [DisplayName] = N'正文字体', [Remark] = N'页面默认字体族', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'PageFontFamily' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'PageFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'PageFontSize', N'12', N'正文字号', N'页面默认字号', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'12', [DisplayName] = N'正文字号', [Remark] = N'页面默认字号', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'PageFontSize' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderFontFamily')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderFontFamily', N'SimSun', N'标题字体', N'页眉标题字体族', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'SimSun', [DisplayName] = N'标题字体', [Remark] = N'页眉标题字体族', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'HeaderFontFamily' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderFontSize', N'18', N'标题字号', N'页眉标题字号', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'18', [DisplayName] = N'标题字号', [Remark] = N'页眉标题字号', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'HeaderFontSize' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                """);

            // === 页眉：细分子号 ===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderCompanyNameFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderCompanyNameFontSize', N'14', N'公司名称字号', N'页眉左侧企业名称', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'14', [DisplayName] = N'公司名称字号', [Remark] = N'页眉左侧企业名称', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'HeaderCompanyNameFontSize' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderCompanyNameEnFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderCompanyNameEnFontSize', N'9', N'公司名称英文字号', N'页眉企业英文名称', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'9', [DisplayName] = N'公司名称英文字号', [Remark] = N'页眉企业英文名称', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'HeaderCompanyNameEnFontSize' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderAddressFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderAddressFontSize', N'8', N'公司地址字号', N'页眉右侧地址', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'8', [DisplayName] = N'公司地址字号', [Remark] = N'页眉右侧地址', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'HeaderAddressFontSize' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderAddressEnFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderAddressEnFontSize', N'8', N'公司地址英文字号', N'页眉右侧地址英文', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'8', [DisplayName] = N'公司地址英文字号', [Remark] = N'页眉右侧地址英文', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'HeaderAddressEnFontSize' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderContactFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderContactFontSize', N'8', N'联系方式字号', N'页眉右侧电话/邮箱', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'8', [DisplayName] = N'联系方式字号', [Remark] = N'页眉右侧电话/邮箱', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'HeaderContactFontSize' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'HeaderTitleEnFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderTitleEnFontSize', N'13', N'页眉英文标题字号', N'中文标题下方英文', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'13', [DisplayName] = N'页眉英文标题字号', [Remark] = N'中文标题下方英文', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'HeaderTitleEnFontSize' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                """);

            // === 内容：细分子号 ===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'BasicInfoLabelFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BasicInfoLabelFontSize', N'9', N'基本信息标签字号', N'标签小字在值上方', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'9', [DisplayName] = N'基本信息标签字号', [Remark] = N'标签小字在值上方', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'BasicInfoLabelFontSize' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'BasicInfoValueFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BasicInfoValueFontSize', N'9', N'基本信息值字号', N'客户/标准/名称/交货状态等值', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'9', [DisplayName] = N'基本信息值字号', [Remark] = N'客户/标准/名称/交货状态等值', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'BasicInfoValueFontSize' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'SectionTitleFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'SectionTitleFontSize', N'9', N'区块标题字号', N'基本信息+三张明细表标题条', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'9', [DisplayName] = N'区块标题字号', [Remark] = N'基本信息+三张明细表标题条', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'SectionTitleFontSize' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'MaterialTableFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'MaterialTableFontSize', N'9', N'物料信息表字号', N'物料信息表内容', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'9', [DisplayName] = N'物料信息表字号', [Remark] = N'物料信息表内容', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'MaterialTableFontSize' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'ChemistryTableFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ChemistryTableFontSize', N'9', N'化学成分表字号', N'16列宽表内容', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'9', [DisplayName] = N'化学成分表字号', [Remark] = N'16列宽表内容', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'ChemistryTableFontSize' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'InspectionTableFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'InspectionTableFontSize', N'9', N'检验检测表字号', N'21列宽表内容', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'9', [DisplayName] = N'检验检测表字号', [Remark] = N'21列宽表内容', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'InspectionTableFontSize' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'TableHeaderFontSizeDelta')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'TableHeaderFontSizeDelta', N'0.5', N'表头字号增量', N'表头=表内容字号+此增量', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'0.5', [DisplayName] = N'表头字号增量', [Remark] = N'表头=表内容字号+此增量', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'TableHeaderFontSizeDelta' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'SectionSpacing')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'SectionSpacing', N'5', N'区块间距', N'基本信息与三张明细表之间的顶部间距', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'5', [DisplayName] = N'区块间距', [Remark] = N'基本信息与三张明细表之间的顶部间距', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'SectionSpacing' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                """);

            // === 页脚：细分子号 ===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'FooterStatementFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'FooterStatementFontSize', N'8', N'页脚说明字号', N'页脚第1行', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'8', [DisplayName] = N'页脚说明字号', [Remark] = N'页脚第1行', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'FooterStatementFontSize' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'FooterTextFontSize')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'FooterTextFontSize', N'8', N'页脚三栏字号', N'备注/盖章/签发人', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                ELSE
                    UPDATE [CertificatePrintSettings]
                    SET [Value] = N'8', [DisplayName] = N'页脚三栏字号', [Remark] = N'备注/盖章/签发人', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                    WHERE [Key] = N'FooterTextFontSize' AND [CreatedBy] = N'System' AND [UpdatedBy] = N'System';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回退：删除本迁移新增的 3 个英文键（仅 CreatedBy=System 行，即本迁移所插，用户新增行保留）；
            // 已更新的 System/System 行为写入真库实际值，不还原（避免回退到旧种子默认值）。
            migrationBuilder.Sql("""
                DELETE FROM [CertificatePrintSettings]
                WHERE [CreatedBy] = N'System' AND [UpdatedBy] = N'System'
                  AND [Key] IN (N'CompanyNameEn', N'CompanyAddressEn', N'HeaderTitleEn');
                """);
        }
    }
}
