using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedProcessCardStyleDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 工艺卡打印版式配置：8 个字体/字号键值对（Key→Value），默认值 = ProcessCardPrintHelper 打印模板硬编码值。
            // 新库走 DbInitializer 种子，存量库（已存在任何配置行或表已有数据）不触发，故此处补数据迁移（幂等 IF NOT EXISTS）。
            // 注：SQL Server 排序规则 case-insensitive，Key 唯一索引即锚点。

            // === 字体族 ===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardStyleDefinitions] WHERE [Key] = N'PageFontFamily')
                    INSERT INTO [ProcessCardStyleDefinitions] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'PageFontFamily', N'华文仿宋', N'正文字体', N'页面默认字体族', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardStyleDefinitions] WHERE [Key] = N'HeaderFontFamily')
                    INSERT INTO [ProcessCardStyleDefinitions] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderFontFamily', N'SimSun', N'主标题字体', N'主标题字体族', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);

            // === 字号 ===
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardStyleDefinitions] WHERE [Key] = N'PageFontSize')
                    INSERT INTO [ProcessCardStyleDefinitions] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'PageFontSize', N'10', N'正文字号', N'页面默认字号', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardStyleDefinitions] WHERE [Key] = N'HeaderFontSize')
                    INSERT INTO [ProcessCardStyleDefinitions] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'HeaderFontSize', N'20', N'主标题字号', N'工艺流转卡标题', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardStyleDefinitions] WHERE [Key] = N'BatchNoFontSize')
                    INSERT INTO [ProcessCardStyleDefinitions] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BatchNoFontSize', N'12', N'生产编号字号', N'页眉生产编号', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardStyleDefinitions] WHERE [Key] = N'BlockTitleFontSize')
                    INSERT INTO [ProcessCardStyleDefinitions] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'BlockTitleFontSize', N'11', N'区块标题字号', N'区块标题', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardStyleDefinitions] WHERE [Key] = N'TableHeaderFontSize')
                    INSERT INTO [ProcessCardStyleDefinitions] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'TableHeaderFontSize', N'9', N'表头字号', N'表格表头', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                IF NOT EXISTS (SELECT 1 FROM [ProcessCardStyleDefinitions] WHERE [Key] = N'CellFontSize')
                    INSERT INTO [ProcessCardStyleDefinitions] ([Key], [Value], [DisplayName], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'CellFontSize', N'9', N'数据字号', N'数据单元格', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 种子数据回退：仅删除由种子写入（CreatedBy=System）的配置行，用户自行修改的行保留
            migrationBuilder.Sql("DELETE FROM [ProcessCardStyleDefinitions] WHERE [CreatedBy] = N'System'");
        }
    }
}
