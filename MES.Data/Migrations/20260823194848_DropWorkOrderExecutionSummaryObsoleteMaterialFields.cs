using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropWorkOrderExecutionSummaryObsoleteMaterialFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingOutsourceFinishQty",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "PendingOutsourceFinishWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "PendingRoughTubeQty",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "PendingRoughTubeWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "TheoreticalFinishQty",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "TheoreticalFinishWeight",
                table: "WorkOrderExecutionSummary");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PendingOutsourceFinishQty",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingOutsourceFinishWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PendingRoughTubeQty",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingRoughTubeWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TheoreticalFinishQty",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TheoreticalFinishWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
