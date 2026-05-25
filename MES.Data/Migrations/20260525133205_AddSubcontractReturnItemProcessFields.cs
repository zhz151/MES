using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubcontractReturnItemProcessFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsForceCompleted",
                table: "SubcontractReturnItem",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProcessStatus",
                table: "SubcontractReturnItem",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<int>(
                name: "ReturnedQuantity",
                table: "SubcontractReturnItem",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ReturnedWeight",
                table: "SubcontractReturnItem",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsForceCompleted",
                table: "SubcontractReturnItem");

            migrationBuilder.DropColumn(
                name: "ProcessStatus",
                table: "SubcontractReturnItem");

            migrationBuilder.DropColumn(
                name: "ReturnedQuantity",
                table: "SubcontractReturnItem");

            migrationBuilder.DropColumn(
                name: "ReturnedWeight",
                table: "SubcontractReturnItem");
        }
    }
}
