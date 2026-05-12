using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameManualStatusToIsForceCompleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualStatus",
                table: "SubcontractOrder");

            migrationBuilder.DropColumn(
                name: "ManualStatus",
                table: "PurchaseOrder");

            migrationBuilder.AddColumn<bool>(
                name: "IsForceCompleted",
                table: "SubcontractOrder",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsForceCompleted",
                table: "PurchaseOrder",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsForceCompleted",
                table: "SubcontractOrder");

            migrationBuilder.DropColumn(
                name: "IsForceCompleted",
                table: "PurchaseOrder");

            migrationBuilder.AddColumn<string>(
                name: "ManualStatus",
                table: "SubcontractOrder",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualStatus",
                table: "PurchaseOrder",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }
    }
}
