using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessGroupTemplateAndPlanProcessGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DaysDiffFromDelivery",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EstimatedProcessCompletionDate",
                table: "WorkOrderExecutionSummary",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryPlanProcessGroup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryPlanId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_InventoryPlanProcessGroup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryPlanProcessGroup_InventoryPlan_InventoryPlanId",
                        column: x => x.InventoryPlanId,
                        principalTable: "InventoryPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PiercingPlanProcessGroup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoundBarPiercingPlanId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_PiercingPlanProcessGroup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PiercingPlanProcessGroup_RoundBarPiercingPlan_RoundBarPiercingPlanId",
                        column: x => x.RoundBarPiercingPlanId,
                        principalTable: "RoundBarPiercingPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SemiPlanProcessGroup",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseSemiPlanId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_SemiPlanProcessGroup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SemiPlanProcessGroup_PurchaseSemiPlan_PurchaseSemiPlanId",
                        column: x => x.PurchaseSemiPlanId,
                        principalTable: "PurchaseSemiPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPlanProcessGroup_PlanId",
                table: "InventoryPlanProcessGroup",
                column: "InventoryPlanId");

            migrationBuilder.CreateIndex(
                name: "UK_InventoryPlanProcessGroup_Seq",
                table: "InventoryPlanProcessGroup",
                columns: new[] { "InventoryPlanId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PiercingPlanProcessGroup_PlanId",
                table: "PiercingPlanProcessGroup",
                column: "RoundBarPiercingPlanId");

            migrationBuilder.CreateIndex(
                name: "UK_PiercingPlanProcessGroup_Seq",
                table: "PiercingPlanProcessGroup",
                columns: new[] { "RoundBarPiercingPlanId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SemiPlanProcessGroup_PlanId",
                table: "SemiPlanProcessGroup",
                column: "PurchaseSemiPlanId");

            migrationBuilder.CreateIndex(
                name: "UK_SemiPlanProcessGroup_Seq",
                table: "SemiPlanProcessGroup",
                columns: new[] { "PurchaseSemiPlanId", "SequenceNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryPlanProcessGroup");

            migrationBuilder.DropTable(
                name: "PiercingPlanProcessGroup");

            migrationBuilder.DropTable(
                name: "SemiPlanProcessGroup");

            migrationBuilder.DropColumn(
                name: "DaysDiffFromDelivery",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "EstimatedProcessCompletionDate",
                table: "WorkOrderExecutionSummary");
        }
    }
}
