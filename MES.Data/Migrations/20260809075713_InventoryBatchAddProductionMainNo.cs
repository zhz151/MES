using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class InventoryBatchAddProductionMainNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductionMainNo",
                table: "InventoryBatch",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // 存量回填：生产批号优先（ProductionBatch），工单号兜底（WorkOrder），与 ResolveAssociationAsync 优先级一致
            migrationBuilder.Sql(@"
UPDATE ib
SET ib.ProductionMainNo = COALESCE(pb.ProductionMainNo, wo.ProductionMainNo)
FROM InventoryBatch ib
LEFT JOIN ProductionBatch pb ON pb.BatchNo = ib.ProductionBatchNo
LEFT JOIN WorkOrder wo ON wo.WorkOrderNo = ib.WorkOrderNo
WHERE ib.ProductionMainNo IS NULL
  AND (pb.ProductionMainNo IS NOT NULL OR wo.ProductionMainNo IS NOT NULL);");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatch_ProductionMainNo",
                table: "InventoryBatch",
                column: "ProductionMainNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryBatch_ProductionMainNo",
                table: "InventoryBatch");

            migrationBuilder.DropColumn(
                name: "ProductionMainNo",
                table: "InventoryBatch");
        }
    }
}
