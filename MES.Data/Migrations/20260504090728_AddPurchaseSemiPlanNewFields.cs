using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseSemiPlanNewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "OutboundRecord");

            migrationBuilder.AddColumn<string>(
                name: "PlantGrade",
                table: "PurchaseSemiPlan",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RequiredUnitWeight",
                table: "PurchaseSemiPlan",
                type: "decimal(18,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlantGrade",
                table: "PurchaseSemiPlan");

            migrationBuilder.DropColumn(
                name: "RequiredUnitWeight",
                table: "PurchaseSemiPlan");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "OutboundRecord",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
