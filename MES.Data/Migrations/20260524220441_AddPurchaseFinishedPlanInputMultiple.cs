using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseFinishedPlanInputMultiple : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InputMultiple",
                table: "PurchaseOrder",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InputMultiple",
                table: "PurchaseFinishedPlan",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InputMultiple",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "InputMultiple",
                table: "PurchaseFinishedPlan");
        }
    }
}
