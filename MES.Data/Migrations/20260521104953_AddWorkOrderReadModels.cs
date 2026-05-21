using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkOrderListSummary",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkOrderId = table.Column<int>(type: "int", nullable: false),
                    WorkOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SalesOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionMainNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionSubNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OrderItemIds = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SignDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Salesman = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EndCustomer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DelayPenalty = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SettlementMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StandardCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeliveryState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PlantGrade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OuterDiameterNegative = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OuterDiameterPositive = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WallThicknessNegative = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WallThicknessPositive = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LengthStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MinLength = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxLength = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalMeters = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    TotalItemCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ItemDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicalRequirements = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Normal"),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestPlanDate = table.Column<DateTime>(type: "date", nullable: true),
                    MaterialPlanRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    MaterialPlanStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SemiPlanTotalWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    SemiPlanTotalPieces = table.Column<int>(type: "int", nullable: true),
                    FinishedPlanTotalWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    FinishedPlanTotalPieces = table.Column<int>(type: "int", nullable: true),
                    InventoryPlanTotalWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    InventoryPlanTotalPieces = table.Column<int>(type: "int", nullable: true),
                    ReworkPlanTotalWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    ReworkPlanTotalPieces = table.Column<int>(type: "int", nullable: true),
                    PiercingPlanTotalWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    PiercingPlanTotalPieces = table.Column<int>(type: "int", nullable: true),
                    MainNoMaterialPlanRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    MainNoMaterialPlanStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    OrderMaterialPlanStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    LastRefreshTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderListSummary", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderStatusSummary",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesOrderId = table.Column<int>(type: "int", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SignDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Salesman = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EndCustomer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeliveryStart = table.Column<DateTime>(type: "date", nullable: true),
                    DeliveryEnd = table.Column<DateTime>(type: "date", nullable: true),
                    HasDelayPenalty = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TotalContractWeight = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ItemCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    WorkOrderCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    WorkOrderStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "NotGenerated"),
                    HasWorkOrder = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    WorkOrderId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    LastChangeDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderStatusSummary", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WOLS_LatestPlanDate",
                table: "WorkOrderListSummary",
                column: "LatestPlanDate");

            migrationBuilder.CreateIndex(
                name: "IX_WOLS_MainNoMaterialPlanStatus",
                table: "WorkOrderListSummary",
                column: "MainNoMaterialPlanStatus");

            migrationBuilder.CreateIndex(
                name: "IX_WOLS_MaterialPlanStatus",
                table: "WorkOrderListSummary",
                column: "MaterialPlanStatus");

            migrationBuilder.CreateIndex(
                name: "IX_WOLS_OrderMaterialPlanStatus",
                table: "WorkOrderListSummary",
                column: "OrderMaterialPlanStatus");

            migrationBuilder.CreateIndex(
                name: "IX_WOLS_ProductionMainNo",
                table: "WorkOrderListSummary",
                column: "ProductionMainNo");

            migrationBuilder.CreateIndex(
                name: "IX_WOLS_SalesOrderNo",
                table: "WorkOrderListSummary",
                column: "SalesOrderNo");

            migrationBuilder.CreateIndex(
                name: "IX_WOLS_Status",
                table: "WorkOrderListSummary",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WOLS_WorkOrderNo",
                table: "WorkOrderListSummary",
                column: "WorkOrderNo");

            migrationBuilder.CreateIndex(
                name: "UK_WOLS_WorkOrderId",
                table: "WorkOrderListSummary",
                column: "WorkOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WOSS_CustomerName",
                table: "WorkOrderStatusSummary",
                column: "CustomerName");

            migrationBuilder.CreateIndex(
                name: "IX_WOSS_OrderNumber",
                table: "WorkOrderStatusSummary",
                column: "OrderNumber");

            migrationBuilder.CreateIndex(
                name: "IX_WOSS_SignDate",
                table: "WorkOrderStatusSummary",
                column: "SignDate");

            migrationBuilder.CreateIndex(
                name: "IX_WOSS_WorkOrderStatus",
                table: "WorkOrderStatusSummary",
                column: "WorkOrderStatus");

            migrationBuilder.CreateIndex(
                name: "UK_WOSS_SalesOrderId",
                table: "WorkOrderStatusSummary",
                column: "SalesOrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkOrderListSummary");

            migrationBuilder.DropTable(
                name: "WorkOrderStatusSummary");
        }
    }
}
