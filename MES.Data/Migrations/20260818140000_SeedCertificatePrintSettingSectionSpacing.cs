using MES.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260818140000_SeedCertificatePrintSettingSectionSpacing")]
    public partial class SeedCertificatePrintSettingSectionSpacing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 质量证明书打印配置：新增「区块间距」键（基本信息与三张明细表之间的顶部间距，三处共用）。
            // 默认 6 = 原 CertificatePrintHelper 模板写死值。幂等 IF NOT EXISTS 按 Key 锚点插入。
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [CertificatePrintSettings] WHERE [Key] = N'SectionSpacing')
                    INSERT INTO [CertificatePrintSettings] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'SectionSpacing', N'6', N'区块间距', N'基本信息与三张明细表之间的顶部间距', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回退：删除本迁移种子的区块间距键（CreatedBy=System），用户修改的行保留
            migrationBuilder.Sql("""
                DELETE FROM [CertificatePrintSettings]
                WHERE [CreatedBy] = N'System' AND [Key] = N'SectionSpacing';
                """);
        }
    }
}
