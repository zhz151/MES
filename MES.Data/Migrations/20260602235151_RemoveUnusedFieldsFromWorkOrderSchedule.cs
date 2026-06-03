using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedFieldsFromWorkOrderSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentRawMaterialLockRemark",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "CurrentScheduleStage",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "GeneralDefectRatio",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "GeneralDefectWeight",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "HasAbnormality",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "InputEndDate",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "InputOutputRatio",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "InputQuantity",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "InputStartDate",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "InputStatus",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "InputWeight",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "IsExecuted",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "LatestPlanDate",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "MainNoInputOutputRatio",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "MainNoInputStatus",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "MainNoMaterialPlanRate",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "MainNoMaterialPlanStatus",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "MaterialPlanRate",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "MaterialPlanStatus",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "PendingOutsourceFinishQty",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "PendingOutsourceFinishWeight",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "PendingRoughTubeQty",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "PendingRoughTubeWeight",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "PlannedEndDate",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "PlannedStartDate",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "ProcessCycle",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "Remark",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "ScheduleStatus",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "ScrapRatio",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "ScrapWeight",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "SeriousDefectRatio",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "SeriousDefectWeight",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "TheoreticalFinishQty",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "TheoreticalFinishWeight",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "TheoreticalOutputQty",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "TheoreticalOutputWeight",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "TotalBatchCount",
                table: "WorkOrderSchedules");

            migrationBuilder.DropColumn(
                name: "UrgencyReason",
                table: "WorkOrderSchedules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentRawMaterialLockRemark",
                table: "WorkOrderSchedules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentScheduleStage",
                table: "WorkOrderSchedules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GeneralDefectRatio",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GeneralDefectWeight",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "HasAbnormality",
                table: "WorkOrderSchedules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "InputEndDate",
                table: "WorkOrderSchedules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InputOutputRatio",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "InputQuantity",
                table: "WorkOrderSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "InputStartDate",
                table: "WorkOrderSchedules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InputStatus",
                table: "WorkOrderSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "InputWeight",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsExecuted",
                table: "WorkOrderSchedules",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LatestPlanDate",
                table: "WorkOrderSchedules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MainNoInputOutputRatio",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MainNoInputStatus",
                table: "WorkOrderSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MainNoMaterialPlanRate",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MainNoMaterialPlanStatus",
                table: "WorkOrderSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MaterialPlanRate",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MaterialPlanStatus",
                table: "WorkOrderSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PendingOutsourceFinishQty",
                table: "WorkOrderSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingOutsourceFinishWeight",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PendingRoughTubeQty",
                table: "WorkOrderSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingRoughTubeWeight",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedEndDate",
                table: "WorkOrderSchedules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedStartDate",
                table: "WorkOrderSchedules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "WorkOrderSchedules",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessCycle",
                table: "WorkOrderSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Remark",
                table: "WorkOrderSchedules",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScheduleStatus",
                table: "WorkOrderSchedules",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ScrapRatio",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ScrapWeight",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SeriousDefectRatio",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SeriousDefectWeight",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TheoreticalFinishQty",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TheoreticalFinishWeight",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TheoreticalOutputQty",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TheoreticalOutputWeight",
                table: "WorkOrderSchedules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TotalBatchCount",
                table: "WorkOrderSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UrgencyReason",
                table: "WorkOrderSchedules",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
