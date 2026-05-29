using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProcessPlanFromMaterialPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessPlan",
                table: "RoundBarPiercingPlan");

            migrationBuilder.DropColumn(
                name: "ProcessPlan",
                table: "PurchaseSemiPlan");

            migrationBuilder.DropColumn(
                name: "ProcessPlan",
                table: "InventoryPlan");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProcessPlan",
                table: "RoundBarPiercingPlan",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessPlan",
                table: "PurchaseSemiPlan",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessPlan",
                table: "InventoryPlan",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
