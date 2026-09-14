using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalInspectionInProcessWarehouseTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FinalInspectionInProcessWarehouseWeight",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefectInProcessWarehouseQuantity",
                table: "QualityProcessTracking",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefectInProcessWarehouseQuantity",
                table: "FinalInspection",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefectInProcessWarehouseWeight",
                table: "FinalInspection",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalInspectionInProcessWarehouseWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "DefectInProcessWarehouseQuantity",
                table: "QualityProcessTracking");

            migrationBuilder.DropColumn(
                name: "DefectInProcessWarehouseQuantity",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "DefectInProcessWarehouseWeight",
                table: "FinalInspection");
        }
    }
}
