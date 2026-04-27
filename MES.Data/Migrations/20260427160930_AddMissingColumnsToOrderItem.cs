using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingColumnsToOrderItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OuterDiameterMinus",
                table: "OrderItem",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OuterDiameterPlus",
                table: "OrderItem",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WallThicknessMinus",
                table: "OrderItem",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WallThicknessPlus",
                table: "OrderItem",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OuterDiameterMinus",
                table: "OrderItem");

            migrationBuilder.DropColumn(
                name: "OuterDiameterPlus",
                table: "OrderItem");

            migrationBuilder.DropColumn(
                name: "WallThicknessMinus",
                table: "OrderItem");

            migrationBuilder.DropColumn(
                name: "WallThicknessPlus",
                table: "OrderItem");
        }
    }
}
