using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceProductionNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 迁移旧枚举值为新值
            migrationBuilder.Sql("UPDATE ProductionBatch SET InputType = 'SplitFromNumber' WHERE InputType = 'Normal'");
            migrationBuilder.Sql("UPDATE ProductionBatch SET InputType = 'Other' WHERE InputType = 'FromOtherWorkOrder'");
            // 有仓库来源的旧批次，InputType 修正为 Warehouse
            migrationBuilder.Sql(@"UPDATE pb SET InputType = 'Warehouse'
                FROM ProductionBatch pb
                WHERE pb.InputType = 'SplitFromNumber'
                AND (
                    pb.SourceBatchNo IS NOT NULL
                    OR EXISTS (SELECT 1 FROM ProductionBatchInventory pbi WHERE pbi.ProductionBatchId = pb.Id)
                )");

            migrationBuilder.AlterColumn<string>(
                name: "InputType",
                table: "ProductionBatch",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "SplitFromNumber",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Normal");

            migrationBuilder.AddColumn<string>(
                name: "SourceProductionNo",
                table: "ProductionBatch",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceProductionNo",
                table: "ProductionBatch");

            migrationBuilder.AlterColumn<string>(
                name: "InputType",
                table: "ProductionBatch",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "SplitFromNumber");
        }
    }
}
