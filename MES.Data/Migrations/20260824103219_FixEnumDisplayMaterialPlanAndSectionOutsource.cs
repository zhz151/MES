using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixEnumDisplayMaterialPlanAndSectionOutsource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 枚举表值级修正（对齐 EnumHelper.Register 当前注册）：
            // 1) MaterialPlanStatus：2026-08-17 数据迁移已把 TheoreticalSatisfied 并入 Satisfied（status>2?status-1:status），
            //    但枚举显示配置表残留 5 行、Register 仅 4 值——删除残留值并顺移 Satisfied/Excess 的 DisplayOrder 与种子一致（1未计划/2部分/3满足/4超量）。
            // 2) SectionOutsourceStatus：Register 有 4 值（含 Virtual「略」，厂内行用），真库缺 Virtual——补齐 DisplayOrder=4。
            migrationBuilder.Sql("""
                -- 1) MaterialPlanStatus：删除并入后残留的 TheoreticalSatisfied，顺移后续档位
                IF EXISTS (SELECT 1 FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'MaterialPlanStatus' AND [Value] = N'TheoreticalSatisfied')
                    DELETE FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'MaterialPlanStatus' AND [Value] = N'TheoreticalSatisfied';
                UPDATE [EnumDisplayDefinitions] SET [DisplayOrder] = 3 WHERE [EnumKey] = N'MaterialPlanStatus' AND [Value] = N'Satisfied' AND [DisplayOrder] <> 3;
                UPDATE [EnumDisplayDefinitions] SET [DisplayOrder] = 4 WHERE [EnumKey] = N'MaterialPlanStatus' AND [Value] = N'Excess' AND [DisplayOrder] <> 4;
                -- 2) SectionOutsourceStatus：补齐厂内「略」档
                IF NOT EXISTS (SELECT 1 FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'SectionOutsourceStatus' AND [Value] = N'Virtual')
                    INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'SectionOutsourceStatus', N'Virtual', N'略', 4, N'厂内行（不参与回收）', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 撤销：重建 TheoreticalSatisfied（3）并还原 Satisfied/Excess 档位（4/5）；删除本迁移补齐的 Virtual
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'MaterialPlanStatus' AND [Value] = N'TheoreticalSatisfied')
                    INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'MaterialPlanStatus', N'TheoreticalSatisfied', N'理论满足', 3, NULL, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                UPDATE [EnumDisplayDefinitions] SET [DisplayOrder] = 4 WHERE [EnumKey] = N'MaterialPlanStatus' AND [Value] = N'Satisfied' AND [DisplayOrder] <> 4;
                UPDATE [EnumDisplayDefinitions] SET [DisplayOrder] = 5 WHERE [EnumKey] = N'MaterialPlanStatus' AND [Value] = N'Excess' AND [DisplayOrder] <> 5;
                DELETE FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'SectionOutsourceStatus' AND [Value] = N'Virtual' AND [CreatedBy] = N'System';
                """);
        }
    }
}
