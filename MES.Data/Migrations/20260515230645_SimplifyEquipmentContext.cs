using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyEquipmentContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MaintenanceOrder_ScheduledDate",
                table: "MaintenanceOrder");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceOrder_Status",
                table: "MaintenanceOrder");

            migrationBuilder.DropIndex(
                name: "IX_InspectionRecord_ScheduledDate",
                table: "InspectionRecord");

            migrationBuilder.DropIndex(
                name: "IX_InspectionRecord_Status",
                table: "InspectionRecord");

            migrationBuilder.DropColumn(
                name: "DowntimeHours",
                table: "RepairOrder");

            migrationBuilder.DropColumn(
                name: "VerifyComment",
                table: "RepairOrder");

            migrationBuilder.DropColumn(
                name: "VerifyPerson",
                table: "RepairOrder");

            migrationBuilder.DropColumn(
                name: "VerifyTime",
                table: "RepairOrder");

            migrationBuilder.DropColumn(
                name: "ChecklistResults",
                table: "MaintenanceOrder");

            migrationBuilder.DropColumn(
                name: "MaintType",
                table: "MaintenanceOrder");

            migrationBuilder.DropColumn(
                name: "ScheduledDate",
                table: "MaintenanceOrder");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "MaintenanceOrder");

            migrationBuilder.DropColumn(
                name: "ChecklistResults",
                table: "InspectionRecord");

            migrationBuilder.DropColumn(
                name: "ScheduledDate",
                table: "InspectionRecord");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "InspectionRecord");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRepairDate",
                table: "Equipment",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRepairDate",
                table: "Equipment");

            migrationBuilder.AddColumn<decimal>(
                name: "DowntimeHours",
                table: "RepairOrder",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifyComment",
                table: "RepairOrder",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifyPerson",
                table: "RepairOrder",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifyTime",
                table: "RepairOrder",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChecklistResults",
                table: "MaintenanceOrder",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaintType",
                table: "MaintenanceOrder",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Monthly");

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledDate",
                table: "MaintenanceOrder",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "MaintenanceOrder",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "ChecklistResults",
                table: "InspectionRecord",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledDate",
                table: "InspectionRecord",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "InspectionRecord",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrder_ScheduledDate",
                table: "MaintenanceOrder",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrder_Status",
                table: "MaintenanceOrder",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecord_ScheduledDate",
                table: "InspectionRecord",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecord_Status",
                table: "InspectionRecord",
                column: "Status");
        }
    }
}
