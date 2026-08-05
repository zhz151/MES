using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductionBatchAddProcessInspectionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ProcessInspectionNeedAdjust",
                table: "ProductionBatch",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessInspectionQualifiedQty",
                table: "ProductionBatch",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProcessInspectionQualifiedWeight",
                table: "ProductionBatch",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessInspectionReworkWeight",
                table: "ProductionBatch",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessInspectionScrapWeight",
                table: "ProductionBatch",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessInspectionTheoreticalQty",
                table: "ProductionBatch",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessInspectionNeedAdjust",
                table: "ProductionBatch");

            migrationBuilder.DropColumn(
                name: "ProcessInspectionQualifiedQty",
                table: "ProductionBatch");

            migrationBuilder.DropColumn(
                name: "ProcessInspectionQualifiedWeight",
                table: "ProductionBatch");

            migrationBuilder.DropColumn(
                name: "ProcessInspectionReworkWeight",
                table: "ProductionBatch");

            migrationBuilder.DropColumn(
                name: "ProcessInspectionScrapWeight",
                table: "ProductionBatch");

            migrationBuilder.DropColumn(
                name: "ProcessInspectionTheoreticalQty",
                table: "ProductionBatch");
        }
    }
}
