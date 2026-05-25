using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveG4DeadFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MainNoValidInputOutputRatio",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "MainNoValidInputStatus",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ValidInputOutputRatio",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ValidInputStatus",
                table: "WorkOrderExecutionSummary");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MainNoValidInputOutputRatio",
                table: "WorkOrderExecutionSummary",
                type: "decimal(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MainNoValidInputStatus",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ValidInputOutputRatio",
                table: "WorkOrderExecutionSummary",
                type: "decimal(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ValidInputStatus",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
