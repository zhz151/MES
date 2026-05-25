using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddG7BatchCountsAndG12ScheduleStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FlowIncompleteBatchCount",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FlowTotalBatchCount",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ScheduleStage",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlowIncompleteBatchCount",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "FlowTotalBatchCount",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ScheduleStage",
                table: "WorkOrderExecutionSummary");
        }
    }
}
