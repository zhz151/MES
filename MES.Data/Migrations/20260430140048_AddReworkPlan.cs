using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReworkPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProcessPlan",
                table: "InventoryPlan",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReworkType",
                table: "InventoryPlan",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessPlan",
                table: "InventoryPlan");

            migrationBuilder.DropColumn(
                name: "ReworkType",
                table: "InventoryPlan");
        }
    }
}
