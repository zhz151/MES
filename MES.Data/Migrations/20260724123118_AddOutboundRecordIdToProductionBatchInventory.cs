using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboundRecordIdToProductionBatchInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_PBI_ProductionBatch_InventoryBatch",
                table: "ProductionBatchInventory");

            migrationBuilder.AddColumn<long>(
                name: "OutboundRecordId",
                table: "ProductionBatchInventory",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PBI_OutboundRecordId",
                table: "ProductionBatchInventory",
                column: "OutboundRecordId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionBatchInventory_OutboundRecord_OutboundRecordId",
                table: "ProductionBatchInventory",
                column: "OutboundRecordId",
                principalTable: "OutboundRecord",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionBatchInventory_OutboundRecord_OutboundRecordId",
                table: "ProductionBatchInventory");

            migrationBuilder.DropIndex(
                name: "IX_PBI_OutboundRecordId",
                table: "ProductionBatchInventory");

            migrationBuilder.DropColumn(
                name: "OutboundRecordId",
                table: "ProductionBatchInventory");

            migrationBuilder.CreateIndex(
                name: "UK_PBI_ProductionBatch_InventoryBatch",
                table: "ProductionBatchInventory",
                columns: new[] { "ProductionBatchId", "InventoryBatchId" },
                unique: true);
        }
    }
}
