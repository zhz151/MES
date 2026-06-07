using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddG14FieldsToWorkOrderSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DeformedProcessCompleted",
                table: "WorkOrderSchedules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingSection20Roll",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingSection30Roll",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingSection50Roll",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingSection60Roll",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingSectionDrawBench",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingSectionRoughTube",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingSectionThreeRoll",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingSectionWarehouseFix",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionAttentionProcess",
                table: "WorkOrderSchedules",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeformedProcessCompleted",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "PendingSection20Roll",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "PendingSection30Roll",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "PendingSection50Roll",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "PendingSection60Roll",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "PendingSectionDrawBench",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "PendingSectionRoughTube",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "PendingSectionThreeRoll",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "PendingSectionWarehouseFix",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "ProductionAttentionProcess",
                table: "WorkOrderSchedules");
        }
    }
}
