using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceOrderItemUniqueIndexWithFiltered : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_OrderItem_Sequence",
                table: "OrderItem");

            migrationBuilder.CreateIndex(
                name: "UK_OrderItem_Sequence_Active",
                table: "OrderItem",
                columns: new[] { "SalesOrderId", "Sequence" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_OrderItem_Sequence_Active",
                table: "OrderItem");

            migrationBuilder.CreateIndex(
                name: "UK_OrderItem_Sequence",
                table: "OrderItem",
                columns: new[] { "SalesOrderId", "Sequence" },
                unique: true);
        }
    }
}
