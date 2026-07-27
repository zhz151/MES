using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionBatchInventoryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductionBatchInventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionBatchId = table.Column<int>(type: "int", nullable: false),
                    InventoryBatchId = table.Column<int>(type: "int", nullable: false),
                    InputQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    InputWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionBatchInventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionBatchInventory_InventoryBatch_InventoryBatchId",
                        column: x => x.InventoryBatchId,
                        principalTable: "InventoryBatch",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductionBatchInventory_ProductionBatch_ProductionBatchId",
                        column: x => x.ProductionBatchId,
                        principalTable: "ProductionBatch",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PBI_InventoryBatchId",
                table: "ProductionBatchInventory",
                column: "InventoryBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PBI_ProductionBatchId",
                table: "ProductionBatchInventory",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "UK_PBI_ProductionBatch_InventoryBatch",
                table: "ProductionBatchInventory",
                columns: new[] { "ProductionBatchId", "InventoryBatchId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionBatchInventory");
        }
    }
}
