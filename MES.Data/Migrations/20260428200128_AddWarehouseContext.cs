using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryBatch",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    MaterialType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InboundSource = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InboundDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    RelatedNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    HeatNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProductionBatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LengthStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MinLength = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxLength = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InitialQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    InitialWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    UnitWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    Meters = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RemainingQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RemainingWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    ActualSpecification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ActualOuterDiameter = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    ActualWallThickness = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    SurfaceCondition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LocationArea = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LocationRack = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsFrozen = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsMixedPackage = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PackageNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DefectReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LiabilityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OriginalSupplier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TagNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DefectRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsLinkedToWorkOrder = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    WorkOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SalesOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OrderItemIds = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryBatch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryBatchDeleteLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Operator = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DeletedTime = table.Column<DateTime>(type: "datetime", nullable: false),
                    BatchData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryBatchDeleteLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotificationType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Receiver = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notification", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboundRecord",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryBatchId = table.Column<int>(type: "int", nullable: false),
                    OutboundType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TargetCompany = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RelatedNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OutboundQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    OutboundWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    OutboundDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    Operator = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundRecord", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouse", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatch_MaterialType",
                table: "InventoryBatch",
                column: "MaterialType");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatch_PlantGrade",
                table: "InventoryBatch",
                column: "PlantGrade");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatch_ProductionBatchNo",
                table: "InventoryBatch",
                column: "ProductionBatchNo");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatch_RemainingWeight",
                table: "InventoryBatch",
                column: "RemainingWeight",
                filter: "[RemainingWeight] > 0 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatch_SalesOrderNo",
                table: "InventoryBatch",
                column: "SalesOrderNo");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatch_WarehouseId",
                table: "InventoryBatch",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatch_WorkOrderNo",
                table: "InventoryBatch",
                column: "WorkOrderNo");

            migrationBuilder.CreateIndex(
                name: "UK_InventoryBatch_BatchNo",
                table: "InventoryBatch",
                column: "BatchNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboundRecord_InventoryBatchId",
                table: "OutboundRecord",
                column: "InventoryBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundRecord_OutboundDate",
                table: "OutboundRecord",
                column: "OutboundDate");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundRecord_RelatedNo",
                table: "OutboundRecord",
                column: "RelatedNo");

            migrationBuilder.CreateIndex(
                name: "UK_Warehouse_Code",
                table: "Warehouse",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryBatch");

            migrationBuilder.DropTable(
                name: "InventoryBatchDeleteLog");

            migrationBuilder.DropTable(
                name: "Notification");

            migrationBuilder.DropTable(
                name: "OutboundRecord");

            migrationBuilder.DropTable(
                name: "Warehouse");
        }
    }
}
