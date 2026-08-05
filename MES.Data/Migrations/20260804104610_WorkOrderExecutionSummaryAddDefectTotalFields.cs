using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrderExecutionSummaryAddDefectTotalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FinalInspectionDefectQty",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FinalInspectionDefectWeight",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FinalInspectionScrapWeight",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FinalInspectionWarehouseWeight",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessInspectionDefectWeight",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessInspectionScrapWeight",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessInspectionWarehouseWeight",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalInspectionDefectQty",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "FinalInspectionDefectWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "FinalInspectionScrapWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "FinalInspectionWarehouseWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ProcessInspectionDefectWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ProcessInspectionScrapWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ProcessInspectionWarehouseWeight",
                table: "WorkOrderExecutionSummary");
        }
    }
}
