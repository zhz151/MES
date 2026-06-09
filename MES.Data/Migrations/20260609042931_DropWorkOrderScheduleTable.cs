using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropWorkOrderScheduleTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkOrderSchedules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkOrderSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdjustmentRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CapacityWorkDays = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DaysDiffFromDelivery = table.Column<int>(type: "int", nullable: true),
                    DeformedProcessCompleted = table.Column<bool>(type: "bit", nullable: false),
                    DelayPenalty = table.Column<bool>(type: "bit", nullable: false),
                    DeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveryState = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EstimatedProcessCompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FlowIncompleteBatchCount = table.Column<int>(type: "int", nullable: false),
                    FlowMaxRemainingWorkDays = table.Column<int>(type: "int", nullable: false),
                    FlowOutputRatio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FlowStatus = table.Column<int>(type: "int", nullable: false),
                    FlowTotalBatchCount = table.Column<int>(type: "int", nullable: false),
                    IsBatchDelivery = table.Column<bool>(type: "bit", nullable: false),
                    IsPaused = table.Column<bool>(type: "bit", nullable: false),
                    IsUrging = table.Column<bool>(type: "bit", nullable: false),
                    LengthStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MainNoFlowOutputRatio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MainNoFlowStatus = table.Column<int>(type: "int", nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MaxLength = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MinLength = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PendingSection20Roll = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PendingSection30Roll = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PendingSection50Roll = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PendingSection60Roll = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PendingSectionDrawBench = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PendingSectionRoughTube = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PendingSectionThreeRoll = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PendingSectionWarehouseFix = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionAttentionProcess = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProductionMainNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProductionSubNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RawMaterialLockRemark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SalesOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Salesman = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ScheduleStage = table.Column<int>(type: "int", nullable: false),
                    SettlementMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SignDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TotalItemCount = table.Column<int>(type: "int", nullable: false),
                    TotalMeters = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalQuantity = table.Column<int>(type: "int", nullable: false),
                    TotalRemainingWorkDays = table.Column<int>(type: "int", nullable: true),
                    TotalWeight = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UrgencyLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    WorkOrderId = table.Column<int>(type: "int", nullable: false),
                    WorkOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
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
    }
}
