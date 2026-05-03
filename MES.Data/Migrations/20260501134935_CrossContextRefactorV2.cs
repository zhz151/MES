using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrossContextRefactorV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryPlan_InventoryBatchId",
                table: "InventoryPlan");

            migrationBuilder.DropColumn(
                name: "InventoryBatchId",
                table: "InventoryPlan");

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
                name: "InventoryBatchNo",
                table: "InventoryPlan",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PurchaseOrderNo",
                table: "InventoryBatch",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubcontractOrderNo",
                table: "InventoryBatch",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPlan_InventoryBatchNo",
                table: "InventoryPlan",
                column: "InventoryBatchNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryPlan_InventoryBatchNo",
                table: "InventoryPlan");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderNo",
                table: "PurchaseSemiPlan");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderNo",
                table: "PurchaseFinishedPlan");

            migrationBuilder.DropColumn(
                name: "InventoryBatchNo",
                table: "InventoryPlan");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderNo",
                table: "InventoryBatch");

            migrationBuilder.DropColumn(
                name: "SubcontractOrderNo",
                table: "InventoryBatch");

            migrationBuilder.AddColumn<int>(
                name: "InventoryBatchId",
                table: "InventoryPlan",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPlan_InventoryBatchId",
                table: "InventoryPlan",
                column: "InventoryBatchId");
        }
    }
}
