using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedulingContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RawMaterialLockPlanAndExecution",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkOrderId = table.Column<int>(type: "int", nullable: false),
                    WorkOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Salesman = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SignDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DelayPenalty = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SettlementMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SalesOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionMainNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionSubNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MaterialName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DeliveryState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LengthStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MinLength = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    MaxLength = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    TotalItemCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalMeters = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    TotalWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    LatestPlanDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MaterialPlanRate = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    MaterialPlanStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MainNoMaterialPlanRate = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    MainNoMaterialPlanStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ProcessCycle = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    PendingRoughTubeQty = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    PendingRoughTubeWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    PendingOutsourceFinishQty = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    PendingOutsourceFinishWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    TheoreticalFinishQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    TheoreticalFinishWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    InputStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InputEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalBatchCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    InputQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    InputWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    TheoreticalOutputQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    TheoreticalOutputWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    InputOutputRatio = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    InputStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MainNoInputOutputRatio = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    MainNoInputStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    FlowOutputRatio = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    FlowStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MainNoFlowOutputRatio = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    MainNoFlowStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    FlowTotalBatchCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    FlowIncompleteBatchCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    FlowMaxRemainingWorkDays = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    GeneralDefectWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    GeneralDefectRatio = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    SeriousDefectWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    SeriousDefectRatio = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    ScrapWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    ScrapRatio = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    ScheduleStage = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalRemainingWorkDays = table.Column<int>(type: "int", nullable: true),
                    UrgencyLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    EstimatedProcessCompletionDate = table.Column<DateTime>(type: "date", nullable: true),
                    DaysDiffFromDelivery = table.Column<int>(type: "int", nullable: true),
                    RawMaterialLockRemark = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SalesUrging = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    UrgingRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CurrentScheduleStage = table.Column<int>(type: "int", nullable: true),
                    CurrentRawMaterialLockRemark = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsExecuted = table.Column<bool>(type: "bit", nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawMaterialLockPlanAndExecution", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesUrging",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkOrderId = table.Column<int>(type: "int", nullable: false),
                    IsSalesUrging = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    UrgingRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesUrging", x => x.Id);
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

            migrationBuilder.CreateIndex(
                name: "UK_SU_WorkOrderId",
                table: "SalesUrging",
                column: "WorkOrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawMaterialLockPlanAndExecution");

            migrationBuilder.DropTable(
                name: "SalesUrging");
        }
    }
}
