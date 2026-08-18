using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderBusinessCompletionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BusinessCompleted",
                table: "OrderListSummary",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "FinishedInboundWeight",
                table: "OrderListSummary",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FinishedOutboundWeight",
                table: "OrderListSummary",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FinishedStockWeight",
                table: "OrderListSummary",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessCompleted",
                table: "OrderListSummary");

            migrationBuilder.DropColumn(
                name: "FinishedInboundWeight",
                table: "OrderListSummary");

            migrationBuilder.DropColumn(
                name: "FinishedOutboundWeight",
                table: "OrderListSummary");

            migrationBuilder.DropColumn(
                name: "FinishedStockWeight",
                table: "OrderListSummary");
        }
    }
}
