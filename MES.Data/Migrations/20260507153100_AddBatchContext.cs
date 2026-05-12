using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductionBatch",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "None"),
                    TagNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsForceCompleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    QualityRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SolutionParams = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CurrentExecDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    CurrentGroupName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CurrentSectionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CurrentEquipmentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CurrentOutsource = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NextSectionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    WorkOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SalesOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionMainNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionSubNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OrderItemIds = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SignDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    Salesman = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EndCustomer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeliveryDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    DelayPenalty = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MaterialName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SettlementMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StandardCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DeliveryState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OuterDiameterNegative = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    OuterDiameterPositive = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    WallThicknessNegative = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    WallThicknessPositive = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    LengthStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MinLength = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxLength = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalMeters = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    TotalItemCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ItemDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicalRequirements = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceBatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    WarehouseId = table.Column<int>(type: "int", nullable: true),
                    SourceMaterialType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    InboundSource = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InboundDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    SourceHeatNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InputQuantity = table.Column<int>(type: "int", nullable: true),
                    InputWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionBatch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessGroup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionBatchId = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    ProcessName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ManufacturingSpec = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ManufacturingLength = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CuttingTreatment = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ColdRollDraw = table.Column<int>(type: "int", nullable: true),
                    OilPipeCut = table.Column<int>(type: "int", nullable: true),
                    Degrease = table.Column<int>(type: "int", nullable: true),
                    Solution = table.Column<int>(type: "int", nullable: true),
                    Straighten = table.Column<int>(type: "int", nullable: true),
                    Cut = table.Column<int>(type: "int", nullable: true),
                    ThicknessMeasure = table.Column<int>(type: "int", nullable: true),
                    Pickle = table.Column<int>(type: "int", nullable: true),
                    OuterPolish = table.Column<int>(type: "int", nullable: true),
                    InnerGrinding = table.Column<int>(type: "int", nullable: true),
                    OuterSpotGrinding = table.Column<int>(type: "int", nullable: true),
                    Inspection = table.Column<int>(type: "int", nullable: true),
                    WeldingHead = table.Column<int>(type: "int", nullable: true),
                    Lubrication = table.Column<int>(type: "int", nullable: true),
                    Warehouse = table.Column<int>(type: "int", nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessGroup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessGroup_ProductionBatch_ProductionBatchId",
                        column: x => x.ProductionBatchId,
                        principalTable: "ProductionBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessGroup_BatchId",
                table: "ProcessGroup",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "UK_ProcessGroup_Seq",
                table: "ProcessGroup",
                columns: new[] { "ProductionBatchId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatch_SalesOrderNo",
                table: "ProductionBatch",
                column: "SalesOrderNo");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatch_Status",
                table: "ProductionBatch",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatch_TagNo",
                table: "ProductionBatch",
                column: "TagNo");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatch_WorkOrderNo",
                table: "ProductionBatch",
                column: "WorkOrderNo");

            migrationBuilder.CreateIndex(
                name: "UK_ProductionBatch_BatchNo",
                table: "ProductionBatch",
                column: "BatchNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessGroup");

            migrationBuilder.DropTable(
                name: "ProductionBatch");
        }
    }
}
