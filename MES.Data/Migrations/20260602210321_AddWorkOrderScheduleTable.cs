using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderScheduleTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkOrderSchedules",
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
                    DelayPenalty = table.Column<bool>(type: "bit", nullable: false),
                    SettlementMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SalesOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionMainNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProductionSubNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MaterialName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DeliveryState = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LengthStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalItemCount = table.Column<int>(type: "int", nullable: false),
                    TotalQuantity = table.Column<int>(type: "int", nullable: false),
                    TotalMeters = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalWeight = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LatestPlanDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MaterialPlanRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaterialPlanStatus = table.Column<int>(type: "int", nullable: false),
                    MainNoMaterialPlanRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MainNoMaterialPlanStatus = table.Column<int>(type: "int", nullable: false),
                    ProcessCycle = table.Column<int>(type: "int", nullable: false),
                    PendingRoughTubeQty = table.Column<int>(type: "int", nullable: false),
                    PendingRoughTubeWeight = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PendingOutsourceFinishQty = table.Column<int>(type: "int", nullable: false),
                    PendingOutsourceFinishWeight = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TheoreticalFinishQty = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TheoreticalFinishWeight = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InputStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InputEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalBatchCount = table.Column<int>(type: "int", nullable: false),
                    InputQuantity = table.Column<int>(type: "int", nullable: false),
                    InputWeight = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TheoreticalOutputQty = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TheoreticalOutputWeight = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InputOutputRatio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InputStatus = table.Column<int>(type: "int", nullable: false),
                    MainNoInputOutputRatio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MainNoInputStatus = table.Column<int>(type: "int", nullable: false),
                    FlowOutputRatio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FlowStatus = table.Column<int>(type: "int", nullable: false),
                    MainNoFlowOutputRatio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MainNoFlowStatus = table.Column<int>(type: "int", nullable: false),
                    FlowTotalBatchCount = table.Column<int>(type: "int", nullable: false),
                    FlowIncompleteBatchCount = table.Column<int>(type: "int", nullable: false),
                    FlowMaxRemainingWorkDays = table.Column<int>(type: "int", nullable: false),
                    GeneralDefectWeight = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GeneralDefectRatio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SeriousDefectWeight = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SeriousDefectRatio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ScrapWeight = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ScrapRatio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ScheduleStage = table.Column<int>(type: "int", nullable: false),
                    TotalRemainingWorkDays = table.Column<int>(type: "int", nullable: true),
                    UrgencyLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    EstimatedProcessCompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DaysDiffFromDelivery = table.Column<int>(type: "int", nullable: true),
                    RawMaterialLockRemark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SalesUrging = table.Column<bool>(type: "bit", nullable: false),
                    UrgingRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CurrentScheduleStage = table.Column<int>(type: "int", nullable: true),
                    CurrentRawMaterialLockRemark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsExecuted = table.Column<bool>(type: "bit", nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PlannedStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PlannedEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduleStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UrgencyReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HasAbnormality = table.Column<bool>(type: "bit", nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderSchedules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_WOS_WorkOrderId",
                table: "WorkOrderSchedules",
                column: "WorkOrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkOrderSchedules");
        }
    }
}
