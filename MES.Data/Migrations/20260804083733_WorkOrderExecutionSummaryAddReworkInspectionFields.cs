using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrderExecutionSummaryAddReworkInspectionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FinalInspectionReworkWeight",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessInspectionReworkWeight",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReworkInputConsistency",
                table: "WorkOrderExecutionSummary",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReworkTheoreticalProduceQty",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalInspectionReworkWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ProcessInspectionReworkWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ReworkInputConsistency",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ReworkTheoreticalProduceQty",
                table: "WorkOrderExecutionSummary");
        }
    }
}
