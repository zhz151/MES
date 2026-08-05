using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class QualityProcessTrackingKeyDropIsDeliveryStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_QPT_ProductionBatchTypeDelivery",
                table: "QualityProcessTracking");

            migrationBuilder.CreateIndex(
                name: "UK_QPT_ProductionBatchType",
                table: "QualityProcessTracking",
                columns: new[] { "ProductionBatchId", "InspectionType" },
                unique: true,
                filter: "[InspectionType] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_QPT_ProductionBatchType",
                table: "QualityProcessTracking");

            migrationBuilder.CreateIndex(
                name: "UK_QPT_ProductionBatchTypeDelivery",
                table: "QualityProcessTracking",
                columns: new[] { "ProductionBatchId", "InspectionType", "IsDeliveryStatus" },
                unique: true,
                filter: "[InspectionType] IS NOT NULL AND [IsDeliveryStatus] IS NOT NULL");
        }
    }
}
