using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseFinishedPlanWorkOrderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryState",
                table: "PurchaseFinishedPlan",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LengthStatus",
                table: "PurchaseFinishedPlan",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "MaxLength",
                table: "PurchaseFinishedPlan",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinLength",
                table: "PurchaseFinishedPlan",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OuterDiameterNegative",
                table: "PurchaseFinishedPlan",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OuterDiameterPositive",
                table: "PurchaseFinishedPlan",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PlantGrade",
                table: "PurchaseFinishedPlan",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Specification",
                table: "PurchaseFinishedPlan",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "WallThicknessNegative",
                table: "PurchaseFinishedPlan",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WallThicknessPositive",
                table: "PurchaseFinishedPlan",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryState",
                table: "PurchaseFinishedPlan");

            migrationBuilder.DropColumn(
                name: "LengthStatus",
                table: "PurchaseFinishedPlan");

            migrationBuilder.DropColumn(
                name: "MaxLength",
                table: "PurchaseFinishedPlan");

            migrationBuilder.DropColumn(
                name: "MinLength",
                table: "PurchaseFinishedPlan");

            migrationBuilder.DropColumn(
                name: "OuterDiameterNegative",
                table: "PurchaseFinishedPlan");

            migrationBuilder.DropColumn(
                name: "OuterDiameterPositive",
                table: "PurchaseFinishedPlan");

            migrationBuilder.DropColumn(
                name: "PlantGrade",
                table: "PurchaseFinishedPlan");

            migrationBuilder.DropColumn(
                name: "Specification",
                table: "PurchaseFinishedPlan");

            migrationBuilder.DropColumn(
                name: "WallThicknessNegative",
                table: "PurchaseFinishedPlan");

            migrationBuilder.DropColumn(
                name: "WallThicknessPositive",
                table: "PurchaseFinishedPlan");
        }
    }
}
