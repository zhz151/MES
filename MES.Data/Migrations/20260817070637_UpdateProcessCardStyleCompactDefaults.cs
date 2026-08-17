using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProcessCardStyleCompactDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 工艺卡「整体紧凑化」：字号默认值微降（与 ProcessCardPrintHelper 代码默认值对齐），
            // 配合页边距/页眉/行高压缩，使 12 行工序组可打印在一页。
            // 幂等：值已为目标则无变化；覆盖种子值及此前面板保存的较大字号（表头/数据 10 在 33 窄列下折行会触发布局冲突）。
            migrationBuilder.Sql("""
                UPDATE [ProcessCardStyleDefinitions] SET [Value] = N'9' WHERE [Key] = N'PageFontSize' AND [Value] <> N'9';
                UPDATE [ProcessCardStyleDefinitions] SET [Value] = N'10' WHERE [Key] = N'BlockTitleFontSize' AND [Value] <> N'10';
                UPDATE [ProcessCardStyleDefinitions] SET [Value] = N'8.5' WHERE [Key] = N'TableHeaderFontSize' AND [Value] <> N'8.5';
                UPDATE [ProcessCardStyleDefinitions] SET [Value] = N'8.5' WHERE [Key] = N'CellFontSize' AND [Value] <> N'8.5';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回退：仅恢复值为紧凑化新值的行
            migrationBuilder.Sql("""
                UPDATE [ProcessCardStyleDefinitions] SET [Value] = N'10' WHERE [Key] = N'PageFontSize' AND [Value] = N'9';
                UPDATE [ProcessCardStyleDefinitions] SET [Value] = N'11' WHERE [Key] = N'BlockTitleFontSize' AND [Value] = N'10';
                UPDATE [ProcessCardStyleDefinitions] SET [Value] = N'9' WHERE [Key] = N'TableHeaderFontSize' AND [Value] = N'8.5';
                UPDATE [ProcessCardStyleDefinitions] SET [Value] = N'9' WHERE [Key] = N'CellFontSize' AND [Value] = N'8.5';
                """);
        }
    }
}
