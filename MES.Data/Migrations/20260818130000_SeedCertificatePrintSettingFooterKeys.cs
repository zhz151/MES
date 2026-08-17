using MES.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260818130000_SeedCertificatePrintSettingFooterKeys")]
    public partial class SeedCertificatePrintSettingFooterKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 质量证明书打印配置：页脚三块（备注/盖章中英/签发人两行）配套键补充。
            // ① 新增盖章英文 SealTextEn、检验员 InspectorText（幂等 IF NOT EXISTS）；
            // ② 签发工程师 SignerText 默认值由「签发人：」改为「工程师：」——仅当仍为原种子默认值
            //    （CreatedBy=System 且 Value=原值）才更新，用户已改不覆盖。新库/存量库统一生效。

            // === 盖章英文 + 检验员 ===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'SealTextEn')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'SealTextEn', N'QUALITY INSPECTION SEAL', N'盖章文字（英文）', N'页脚第2行中第2行', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'InspectorText')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'InspectorText', N'检验员：________________', N'检验员签字', N'页脚第2行右第1行', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);

            // === 签发工程师默认值更新（仅原种子默认值）===
            migrationBuilder.Sql("""
                UPDATE [CertificatePrintSettings]
                SET [Value] = N'工程师：________________', [DisplayName] = N'签发工程师', [Remark] = N'页脚第2行右第2行', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                WHERE [Key] = N'SignerText' AND [CreatedBy] = N'System' AND [Value] = N'签发人：________________';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回退：删除本迁移种子的 2 个键（CreatedBy=System），恢复 SignerText 原种子默认值
            migrationBuilder.Sql("""
                DELETE FROM [CertificatePrintSettings]
                WHERE [CreatedBy] = N'System' AND [Key] IN (N'SealTextEn', N'InspectorText');
                UPDATE [CertificatePrintSettings]
                SET [Value] = N'签发人：________________', [DisplayName] = N'签发人', [Remark] = N'页脚第2行右', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                WHERE [Key] = N'SignerText' AND [CreatedBy] = N'System' AND [Value] = N'工程师：________________';
                """);
        }
    }
}
