using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class QualityProcessTrackingKeyAndInventoryBatchRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_QPT_MaterialReceiveCheckId",
                table: "QualityProcessTracking");

            migrationBuilder.DropColumn(
                name: "IsDeliveryStatus",
                table: "MaterialReceiveCheck");

            migrationBuilder.RenameColumn(
                name: "SurfaceCondition",
                table: "InventoryBatch",
                newName: "ManufacturingStatus");

            migrationBuilder.AddColumn<string>(
                name: "InspectionType",
                table: "QualityProcessTracking",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IsDeliveryStatus",
                table: "QualityProcessTracking",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            // 回填存量：成检类型从成检到料头带过来
            migrationBuilder.Sql(
                @"UPDATE qpt
                    SET qpt.InspectionType = rc.InspectionType
                  FROM QualityProcessTracking qpt
                 INNER JOIN MaterialReceiveCheck rc ON rc.Id = qpt.MaterialReceiveCheckId;");

            // 回填存量：是否交付态 = 批次制造状态与交货状态一致（大小写不敏感）
            migrationBuilder.Sql(
                @"UPDATE qpt
                    SET qpt.IsDeliveryStatus = CASE
                          WHEN pb.ManufacturingStatus IS NOT NULL AND pb.DeliveryState IS NOT NULL
                               AND LOWER(pb.ManufacturingStatus) = LOWER(pb.DeliveryState) THEN N'是'
                          ELSE N'否'
                        END
                  FROM QualityProcessTracking qpt
                 INNER JOIN ProductionBatch pb ON pb.Id = qpt.ProductionBatchId;");

            migrationBuilder.CreateIndex(
                name: "IX_QPT_MaterialReceiveCheckId",
                table: "QualityProcessTracking",
                column: "MaterialReceiveCheckId");

            migrationBuilder.CreateIndex(
                name: "UK_QPT_ProductionBatchTypeDelivery",
                table: "QualityProcessTracking",
                columns: new[] { "ProductionBatchId", "InspectionType", "IsDeliveryStatus" },
                unique: true,
                filter: "[InspectionType] IS NOT NULL AND [IsDeliveryStatus] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QPT_MaterialReceiveCheckId",
                table: "QualityProcessTracking");

            migrationBuilder.DropIndex(
                name: "UK_QPT_ProductionBatchTypeDelivery",
                table: "QualityProcessTracking");

            migrationBuilder.DropColumn(
                name: "InspectionType",
                table: "QualityProcessTracking");

            migrationBuilder.DropColumn(
                name: "IsDeliveryStatus",
                table: "QualityProcessTracking");

            migrationBuilder.RenameColumn(
                name: "ManufacturingStatus",
                table: "InventoryBatch",
                newName: "SurfaceCondition");

            migrationBuilder.AddColumn<string>(
                name: "IsDeliveryStatus",
                table: "MaterialReceiveCheck",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UK_QPT_MaterialReceiveCheckId",
                table: "QualityProcessTracking",
                column: "MaterialReceiveCheckId",
                unique: true);
        }
    }
}
