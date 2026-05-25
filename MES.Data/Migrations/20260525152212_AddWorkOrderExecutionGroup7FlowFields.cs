using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderExecutionGroup7FlowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FlowOutputRatio",
                table: "WorkOrderExecutionSummary",
                type: "decimal(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "FlowStatus",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MainNoFlowOutputRatio",
                table: "WorkOrderExecutionSummary",
                type: "decimal(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MainNoFlowStatus",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlowOutputRatio",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "FlowStatus",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "MainNoFlowOutputRatio",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "MainNoFlowStatus",
                table: "WorkOrderExecutionSummary");
        }
    }
}
