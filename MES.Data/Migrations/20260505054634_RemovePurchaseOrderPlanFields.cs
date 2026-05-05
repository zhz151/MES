using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePurchaseOrderPlanFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryState",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "LengthStatus",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "MaxLength",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "MinLength",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "OuterDiameterNegative",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "OuterDiameterPositive",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "PlanType",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "WallThicknessNegative",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "WallThicknessPositive",
                table: "PurchaseOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryState",
                table: "PurchaseOrder",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LengthStatus",
                table: "PurchaseOrder",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxLength",
                table: "PurchaseOrder",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinLength",
                table: "PurchaseOrder",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OuterDiameterNegative",
                table: "PurchaseOrder",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OuterDiameterPositive",
                table: "PurchaseOrder",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PlanType",
                table: "PurchaseOrder",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WallThicknessNegative",
                table: "PurchaseOrder",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WallThicknessPositive",
                table: "PurchaseOrder",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
