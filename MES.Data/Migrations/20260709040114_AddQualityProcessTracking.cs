using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityProcessTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "SalesOrder",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EndCustomer",
                table: "SalesOrder",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Salesman",
                table: "SalesOrder",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "QualityProcessTracking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialReceiveCheckId = table.Column<int>(type: "int", nullable: false),
                    ProductionBatchId = table.Column<int>(type: "int", nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ManufacturingItem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TagNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WorkOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SalesOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SourceUnit = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FurnaceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PlantGrade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProductionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LengthStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProductionWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    ReceiveDate = table.Column<DateTime>(type: "date", nullable: false),
                    Shift = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Checker = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Salesman = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DeliveryState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsForceCompleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PbBatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PmiDate = table.Column<DateTime>(type: "date", nullable: true),
                    VisualDate = table.Column<DateTime>(type: "date", nullable: true),
                    DimensionDate = table.Column<DateTime>(type: "date", nullable: true),
                    EndoscopyDate = table.Column<DateTime>(type: "date", nullable: true),
                    HydroDate = table.Column<DateTime>(type: "date", nullable: true),
                    UnderwaterPneumaticDate = table.Column<DateTime>(type: "date", nullable: true),
                    EddyCurrentDate = table.Column<DateTime>(type: "date", nullable: true),
                    UltrasonicDate = table.Column<DateTime>(type: "date", nullable: true),
                    PortColoringDate = table.Column<DateTime>(type: "date", nullable: true),
                    InspectionCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ProductionCutQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    QualifiedQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DefectReworkQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DefectWarehouseQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DefectScrapQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MaxInspectionDate = table.Column<DateTime>(type: "date", nullable: true),
                    InboundQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    InboundWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    InboundDate = table.Column<DateTime>(type: "date", nullable: true),
                    QualityStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "待检验"),
                    LastRefreshTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityProcessTracking", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QPT_BatchNo",
                table: "QualityProcessTracking",
                column: "BatchNo");

            migrationBuilder.CreateIndex(
                name: "IX_QPT_ProductionBatchId",
                table: "QualityProcessTracking",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_QPT_QualityStatus",
                table: "QualityProcessTracking",
                column: "QualityStatus");

            migrationBuilder.CreateIndex(
                name: "IX_QPT_ReceiveDate",
                table: "QualityProcessTracking",
                column: "ReceiveDate");

            migrationBuilder.CreateIndex(
                name: "IX_QPT_SalesOrderNo",
                table: "QualityProcessTracking",
                column: "SalesOrderNo");

            migrationBuilder.CreateIndex(
                name: "IX_QPT_WorkOrderNo",
                table: "QualityProcessTracking",
                column: "WorkOrderNo");

            migrationBuilder.CreateIndex(
                name: "UK_QPT_MaterialReceiveCheckId",
                table: "QualityProcessTracking",
                column: "MaterialReceiveCheckId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QualityProcessTracking");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "SalesOrder");

            migrationBuilder.DropColumn(
                name: "EndCustomer",
                table: "SalesOrder");

            migrationBuilder.DropColumn(
                name: "Salesman",
                table: "SalesOrder");
        }
    }
}
