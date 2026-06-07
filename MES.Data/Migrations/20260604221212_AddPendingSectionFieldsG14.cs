using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingSectionFieldsG14 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DeformedProcessCompleted",
                table: "WorkOrderExecutionSummary",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingSection20Roll",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingSection30Roll",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingSection50Roll",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingSection60Roll",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingSectionDrawBench",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingSectionRoughTube",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingSectionThreeRoll",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingSectionWarehouseFix",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionAttentionProcess",
                table: "WorkOrderExecutionSummary",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeformedProcessCompleted",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "PendingSection20Roll",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "PendingSection30Roll",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "PendingSection50Roll",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "PendingSection60Roll",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "PendingSectionDrawBench",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "PendingSectionRoughTube",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "PendingSectionThreeRoll",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "PendingSectionWarehouseFix",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ProductionAttentionProcess",
                table: "WorkOrderExecutionSummary");
        }
    }
}
