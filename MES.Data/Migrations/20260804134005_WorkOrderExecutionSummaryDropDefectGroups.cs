using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrderExecutionSummaryDropDefectGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefectiveOutputQty",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "DefectiveOutputWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "DefectiveRatio",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "DefectiveRawQty",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "DefectiveRawWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "GeneralDefectRatio",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "GeneralDefectWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "InspectionDefectQty",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "InspectionDefectRatio",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "InspectionDefectWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "InspectionEndDate",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "InspectionStartDate",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ScrapRatio",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ScrapWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "SeriousDefectRatio",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "SeriousDefectWeight",
                table: "WorkOrderExecutionSummary");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DefectiveOutputQty",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DefectiveOutputWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DefectiveRatio",
                table: "WorkOrderExecutionSummary",
                type: "decimal(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DefectiveRawQty",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DefectiveRawWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GeneralDefectRatio",
                table: "WorkOrderExecutionSummary",
                type: "decimal(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GeneralDefectWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "InspectionDefectQty",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "InspectionDefectRatio",
                table: "WorkOrderExecutionSummary",
                type: "decimal(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InspectionDefectWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "InspectionEndDate",
                table: "WorkOrderExecutionSummary",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InspectionStartDate",
                table: "WorkOrderExecutionSummary",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ScrapRatio",
                table: "WorkOrderExecutionSummary",
                type: "decimal(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ScrapWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SeriousDefectRatio",
                table: "WorkOrderExecutionSummary",
                type: "decimal(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SeriousDefectWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
