using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInProcessReworkPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InProcessReworkPlanTotalPieces",
                table: "WorkOrderListSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InProcessReworkPlanTotalWeight",
                table: "WorkOrderListSummary",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InProcessReworkPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkOrderId = table.Column<int>(type: "int", nullable: false),
                    PlanDate = table.Column<DateTime>(type: "date", nullable: false),
                    ProductionBatchId = table.Column<int>(type: "int", nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BatchTagNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MaterialName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LengthStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalBatchQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalBatchWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    CurrentValidQty = table.Column<int>(type: "int", nullable: true),
                    CurrentValidWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    InputMultiple = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    UsedQuantity = table.Column<int>(type: "int", nullable: true),
                    UsedWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "date", nullable: true),
                    PlanStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Planned"),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReworkType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StandardCycle = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InProcessReworkPlan", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InProcessReworkPlanProcessGroup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InProcessReworkPlanId = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    ProcessName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ManufacturingSpec = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OuterDiameterTolerance = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    WallThicknessTolerance = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ManufacturingLength = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CuttingTreatment = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ManufacturingMultiple = table.Column<int>(type: "int", nullable: false),
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
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InProcessReworkPlanProcessGroup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InProcessReworkPlanProcessGroup_InProcessReworkPlan_InProcessReworkPlanId",
                        column: x => x.InProcessReworkPlanId,
                        principalTable: "InProcessReworkPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InProcessReworkPlan_PlanStatus",
                table: "InProcessReworkPlan",
                column: "PlanStatus");

            migrationBuilder.CreateIndex(
                name: "IX_InProcessReworkPlan_ProductionBatchId",
                table: "InProcessReworkPlan",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_InProcessReworkPlan_WorkOrderId",
                table: "InProcessReworkPlan",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_InProcessReworkPlanPG_PlanId",
                table: "InProcessReworkPlanProcessGroup",
                column: "InProcessReworkPlanId");

            migrationBuilder.CreateIndex(
                name: "UK_InProcessReworkPlanPG_Seq",
                table: "InProcessReworkPlanProcessGroup",
                columns: new[] { "InProcessReworkPlanId", "SequenceNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InProcessReworkPlanProcessGroup");

            migrationBuilder.DropTable(
                name: "InProcessReworkPlan");

            migrationBuilder.DropColumn(
                name: "InProcessReworkPlanTotalPieces",
                table: "WorkOrderListSummary");

            migrationBuilder.DropColumn(
                name: "InProcessReworkPlanTotalWeight",
                table: "WorkOrderListSummary");
        }
    }
}
