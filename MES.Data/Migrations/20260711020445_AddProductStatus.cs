using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. 新增 ProductStatus 列
            migrationBuilder.AddColumn<string>(
                name: "ProductStatus",
                table: "ProductionRecord",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // 2. 回填现有数据：IsFinished=true → 成品, false+荒管处理 → 荒管, false+其他 → 在制
            migrationBuilder.Sql(@"
                UPDATE [ProductionRecord]
                SET [ProductStatus] = N'成品'
                WHERE [IsFinished] = 1
            ");
            migrationBuilder.Sql(@"
                UPDATE [ProductionRecord]
                SET [ProductStatus] = N'荒管'
                WHERE [IsFinished] = 0 AND [ProcessName] = N'荒管处理'
            ");
            migrationBuilder.Sql(@"
                UPDATE [ProductionRecord]
                SET [ProductStatus] = N'在制'
                WHERE [ProductStatus] IS NULL
            ");

            // 3. 删除旧的 IsFinished 列
            migrationBuilder.DropColumn(
                name: "IsFinished",
                table: "ProductionRecord");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductStatus",
                table: "ProductionRecord");

            migrationBuilder.AddColumn<bool>(
                name: "IsFinished",
                table: "ProductionRecord",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
