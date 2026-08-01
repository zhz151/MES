using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductionBatchCutDoubtTypeEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CutDoubt",
                table: "ProductionBatch",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            // 存量数据转换：旧 bit 值 1/0 → 枚举字符串
            // true(1) = 旧逻辑"疑问-数量"（有断切记录但 |成切支数−理论|>5%）；false(0) = "正常"
            migrationBuilder.Sql("UPDATE [ProductionBatch] SET [CutDoubt] = N'QuantityMismatch' WHERE [CutDoubt] = N'1'");
            migrationBuilder.Sql("UPDATE [ProductionBatch] SET [CutDoubt] = N'Normal' WHERE [CutDoubt] = N'0'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "CutDoubt",
                table: "ProductionBatch",
                type: "bit",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }
    }
}
