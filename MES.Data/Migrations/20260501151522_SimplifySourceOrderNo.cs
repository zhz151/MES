using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class SimplifySourceOrderNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PurchaseOrderNo",
                table: "PurchaseSemiPlan");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderNo",
                table: "PurchaseFinishedPlan");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderNo",
                table: "InventoryBatch");

            migrationBuilder.RenameColumn(
                name: "SubcontractOrderNo",
                table: "InventoryBatch",
                newName: "SourceOrderNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SourceOrderNo",
                table: "InventoryBatch",
                newName: "SubcontractOrderNo");

            migrationBuilder.AddColumn<string>(
                name: "PurchaseOrderNo",
                table: "PurchaseSemiPlan",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseOrderNo",
                table: "PurchaseFinishedPlan",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseOrderNo",
                table: "InventoryBatch",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
