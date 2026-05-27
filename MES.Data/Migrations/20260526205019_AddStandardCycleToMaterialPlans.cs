using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStandardCycleToMaterialPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StandardCycle",
                table: "RoundBarPiercingPlan",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StandardCycle",
                table: "PurchaseSemiPlan",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StandardCycle",
                table: "PurchaseFinishedPlan",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StandardCycle",
                table: "InventoryPlan",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StandardCycle",
                table: "RoundBarPiercingPlan");

            migrationBuilder.DropColumn(
                name: "StandardCycle",
                table: "PurchaseSemiPlan");

            migrationBuilder.DropColumn(
                name: "StandardCycle",
                table: "PurchaseFinishedPlan");

            migrationBuilder.DropColumn(
                name: "StandardCycle",
                table: "InventoryPlan");
        }
    }
}
