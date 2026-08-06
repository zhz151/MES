using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 修正 Workstations.SectionName 存量：8 个成检工位(SC031-SC038)将"检验项目"(PMI/表检/尺寸/内窥/水压/水下气压/涡流/超声波)
    /// 误填入"工段"字段 → 统一归位为 Inspection（检验工段）；SC030"成检到料"(MaterialReceiveCheck) → Inspection；
    /// 删除 2 条测试残留工位 TC14-WS / TC14-WS2（无 FK 引用，Name 为 null）。
    /// 依据 2026-08-06 审计：扫码成检路径不依赖 SectionName（靠 ReportType==FinalInspection，检验项目由用户扫码页选择）。
    /// </summary>
    public partial class FixWorkstationSectionNameLegacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) 成检工位(SC031-SC038) + 成检到料(SC030) SectionName 归位为检验工段
            migrationBuilder.Sql("""
                UPDATE [Workstations] SET [SectionName] = N'Inspection'
                WHERE [Code] IN (N'SC030', N'SC031', N'SC032', N'SC033', N'SC034', N'SC035', N'SC036', N'SC037', N'SC038')
                """);

            // 2) 删除测试残留工位（无 FK 引用）
            migrationBuilder.Sql("""
                DELETE FROM [Workstations] WHERE [Code] IN (N'TC14-WS', N'TC14-WS2')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 反向恢复 9 个工位的原始 SectionName
            migrationBuilder.Sql("""
                UPDATE [Workstations] SET [SectionName] = N'成检到料' WHERE [Code] = N'SC030';
                UPDATE [Workstations] SET [SectionName] = N'PMI'      WHERE [Code] = N'SC031';
                UPDATE [Workstations] SET [SectionName] = N'表检'     WHERE [Code] = N'SC032';
                UPDATE [Workstations] SET [SectionName] = N'尺寸'     WHERE [Code] = N'SC033';
                UPDATE [Workstations] SET [SectionName] = N'内窥'     WHERE [Code] = N'SC034';
                UPDATE [Workstations] SET [SectionName] = N'水压'     WHERE [Code] = N'SC035';
                UPDATE [Workstations] SET [SectionName] = N'水下气压' WHERE [Code] = N'SC036';
                UPDATE [Workstations] SET [SectionName] = N'涡流'     WHERE [Code] = N'SC037';
                UPDATE [Workstations] SET [SectionName] = N'超声波'   WHERE [Code] = N'SC038';
                """);

            // 回补 2 条测试残留工位（审计字段以当前时间/空值回填）
            migrationBuilder.Sql("""
                INSERT INTO [Workstations] ([Code], [Name], [EquipmentName], [SectionName], [ReportType], [IsActive], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                VALUES (N'TC14-WS', NULL, NULL, N'TC14-工段', N'ProductionRecord', 1, SYSUTCDATETIME(), N'', SYSUTCDATETIME(), N'');
                INSERT INTO [Workstations] ([Code], [Name], [EquipmentName], [SectionName], [ReportType], [IsActive], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                VALUES (N'TC14-WS2', NULL, NULL, N'TC14-工段', N'ProductionRecord', 1, SYSUTCDATETIME(), N'', SYSUTCDATETIME(), N'');
                """);
        }
    }
}
