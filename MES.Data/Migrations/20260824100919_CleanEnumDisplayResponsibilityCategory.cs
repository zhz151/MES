using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class CleanEnumDisplayResponsibilityCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NCR 责任类别已由固定枚举 ResponsibilityCategory 改为可扩展字典（DictKey=NcrResponsibilityKey），
            // 枚举定义 97eee04 已删除，EnumDisplayDefinitions 配置表残留 5 行已无效，清理掉。
            migrationBuilder.Sql("""
                DELETE FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'ResponsibilityCategory';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 撤销：重建被删除的 ResponsibilityCategory 枚举显示配置 5 行（沿用原枚举名/中文显示/排序）
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'ResponsibilityCategory' AND [Value] = N'ProductionInternal')
                    INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ResponsibilityCategory', N'ProductionInternal', N'生产-厂内', 1, NULL, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'ResponsibilityCategory' AND [Value] = N'ProductionOutsource')
                    INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ResponsibilityCategory', N'ProductionOutsource', N'生产-外协', 2, NULL, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'ResponsibilityCategory' AND [Value] = N'MaterialTubeBlank')
                    INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ResponsibilityCategory', N'MaterialTubeBlank', N'原料-荒管', 3, NULL, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'ResponsibilityCategory' AND [Value] = N'MaterialPurchased')
                    INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ResponsibilityCategory', N'MaterialPurchased', N'原料-外购成品', 4, NULL, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'ResponsibilityCategory' AND [Value] = N'MaterialSurplus')
                    INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'ResponsibilityCategory', N'MaterialSurplus', N'原料-余库料', 5, NULL, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);
        }
    }
}
