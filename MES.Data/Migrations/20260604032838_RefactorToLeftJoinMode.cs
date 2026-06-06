using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorToLeftJoinMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawMaterialLockPlanAndExecution");

            migrationBuilder.CreateTable(
                name: "RawMaterialLockPreExecution",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkOrderId = table.Column<int>(type: "int", nullable: false),
                    IsPreInput = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    BudgetInputDate = table.Column<DateTime>(type: "date", nullable: true),
                    IsMainNoMaterialComplete = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawMaterialLockPreExecution", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_RMLPE_WorkOrderId",
                table: "RawMaterialLockPreExecution",
                column: "WorkOrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawMaterialLockPreExecution");

            migrationBuilder.CreateTable(
                name: "RawMaterialLockPlanAndExecution",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CapacityWorkDays = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DaysDiffFromDelivery = table.Column<int>(type: "int", nullable: true),
                    DelayPenalty = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveryState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EstimatedProcessCompletionDate = table.Column<DateTime>(type: "date", nullable: true),
                    FlowIncompleteBatchCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    FlowMaxRemainingWorkDays = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    FlowOutputRatio = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    FlowStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    FlowTotalBatchCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    GeneralDefectRatio = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    GeneralDefectWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    HasAbnormality = table.Column<bool>(type: "bit", nullable: false),
                    InputEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InputOutputRatio = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    InputQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    InputStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InputStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    InputWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    IsMainNoMaterialComplete = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsPreInput = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LatestPlanDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LatestRequiredDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LengthStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MainNoFlowOutputRatio = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    MainNoFlowStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MainNoInputOutputRatio = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    MainNoInputStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MainNoMaterialPlanRate = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    MainNoMaterialPlanStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MaterialName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MaterialPlanCoveredCount = table.Column<int>(type: "int", nullable: false),
                    MaterialPlanProportion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaterialPlanRate = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    MaterialPlanStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MaxLength = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    MinLength = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    PendingOutsourceFinishQty = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    PendingOutsourceFinishWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    PendingRoughTubeQty = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    PendingRoughTubeWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProcessCycle = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ProductionMainNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionSubNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RawMaterialLockRemark = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SalesOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SalesUrging = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Salesman = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ScheduleStage = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ScrapRatio = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    ScrapWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    SeriousDefectRatio = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    SeriousDefectWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    SettlementMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TheoreticalFinishQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    TheoreticalFinishWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    TheoreticalOutputQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    TheoreticalOutputWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    TotalBatchCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalItemCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalMeters = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    TotalQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalRemainingWorkDays = table.Column<int>(type: "int", nullable: true),
                    TotalWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UrgencyLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UrgingRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WorkOrderId = table.Column<int>(type: "int", nullable: false),
                    WorkOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawMaterialLockPlanAndExecution", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RMLPAE_ScheduleStage",
                table: "RawMaterialLockPlanAndExecution",
                column: "ScheduleStage");

            migrationBuilder.CreateIndex(
                name: "IX_RMLPAE_WorkOrderNo",
                table: "RawMaterialLockPlanAndExecution",
                column: "WorkOrderNo");

            migrationBuilder.CreateIndex(
                name: "UK_RMLPAE_WorkOrderId",
                table: "RawMaterialLockPlanAndExecution",
                column: "WorkOrderId",
                unique: true);
        }
    }
}
