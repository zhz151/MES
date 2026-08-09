using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveManufacturingMultipleFromPlanProcessGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManufacturingMultiple",
                table: "SemiPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "ManufacturingMultiple",
                table: "PiercingPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "ManufacturingMultiple",
                table: "InventoryPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "ManufacturingMultiple",
                table: "InProcessReworkPlanProcessGroup");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ManufacturingMultiple",
                table: "SemiPlanProcessGroup",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ManufacturingMultiple",
                table: "PiercingPlanProcessGroup",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ManufacturingMultiple",
                table: "InventoryPlanProcessGroup",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ManufacturingMultiple",
                table: "InProcessReworkPlanProcessGroup",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
