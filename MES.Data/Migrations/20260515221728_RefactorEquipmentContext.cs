using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorEquipmentContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Equipment_InspectionStatus",
                table: "Equipment");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_MaintStatus",
                table: "Equipment");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_RunningStatus",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "InspectionStatus",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "MaintStatus",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "RunningStatus",
                table: "Equipment");

            migrationBuilder.RenameColumn(
                name: "NextMaintDate",
                table: "Equipment",
                newName: "CurrentMaintStartDate");

            migrationBuilder.RenameColumn(
                name: "NextInspectionDate",
                table: "Equipment",
                newName: "CurrentInspectionStartDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CurrentMaintStartDate",
                table: "Equipment",
                newName: "NextMaintDate");

            migrationBuilder.RenameColumn(
                name: "CurrentInspectionStartDate",
                table: "Equipment",
                newName: "NextInspectionDate");

            migrationBuilder.AddColumn<string>(
                name: "InspectionStatus",
                table: "Equipment",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<string>(
                name: "MaintStatus",
                table: "Equipment",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<string>(
                name: "RunningStatus",
                table: "Equipment",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_InspectionStatus",
                table: "Equipment",
                column: "InspectionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_MaintStatus",
                table: "Equipment",
                column: "MaintStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_RunningStatus",
                table: "Equipment",
                column: "RunningStatus");
        }
    }
}
