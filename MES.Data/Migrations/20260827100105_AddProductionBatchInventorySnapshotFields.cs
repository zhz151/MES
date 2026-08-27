using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionBatchInventorySnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SnapshotBatchNo",
                table: "ProductionBatchInventory",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotHeatNo",
                table: "ProductionBatchInventory",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotMaterialType",
                table: "ProductionBatchInventory",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotPlantGrade",
                table: "ProductionBatchInventory",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotSourceName",
                table: "ProductionBatchInventory",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotSpecification",
                table: "ProductionBatchInventory",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotWarehouseName",
                table: "ProductionBatchInventory",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // 存量数据回填：从关联库存批次（含仓库）复制快照，使合并投料明细脱离实时 JOIN
            migrationBuilder.Sql("""
                UPDATE pbi
                SET
                    SnapshotBatchNo = ib.BatchNo,
                    SnapshotHeatNo = ib.HeatNo,
                    SnapshotPlantGrade = ib.PlantGrade,
                    SnapshotSpecification = ib.Specification,
                    SnapshotMaterialType = ib.MaterialType,
                    SnapshotSourceName = ib.SourceName,
                    SnapshotWarehouseName = w.Name
                FROM ProductionBatchInventory pbi
                INNER JOIN InventoryBatch ib ON ib.Id = pbi.InventoryBatchId
                LEFT JOIN Warehouse w ON w.Id = ib.WarehouseId
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SnapshotBatchNo",
                table: "ProductionBatchInventory");

            migrationBuilder.DropColumn(
                name: "SnapshotHeatNo",
                table: "ProductionBatchInventory");

            migrationBuilder.DropColumn(
                name: "SnapshotMaterialType",
                table: "ProductionBatchInventory");

            migrationBuilder.DropColumn(
                name: "SnapshotPlantGrade",
                table: "ProductionBatchInventory");

            migrationBuilder.DropColumn(
                name: "SnapshotSourceName",
                table: "ProductionBatchInventory");

            migrationBuilder.DropColumn(
                name: "SnapshotSpecification",
                table: "ProductionBatchInventory");

            migrationBuilder.DropColumn(
                name: "SnapshotWarehouseName",
                table: "ProductionBatchInventory");
        }
    }
}
