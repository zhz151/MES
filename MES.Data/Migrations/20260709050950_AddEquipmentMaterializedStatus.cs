using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentMaterializedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InspectionStatus",
                table: "Equipment",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotApplicable");

            migrationBuilder.AddColumn<string>(
                name: "MaintStatus",
                table: "Equipment",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotApplicable");

            migrationBuilder.AddColumn<string>(
                name: "RunningStatus",
                table: "Equipment",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InspectionStatus",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "MaintStatus",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "RunningStatus",
                table: "Equipment");
        }
    }
}
