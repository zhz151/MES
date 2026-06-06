using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialPlanCoverageToExecutionSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LatestRequiredDate",
                table: "WorkOrderExecutionSummary",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaterialPlanCoveredCount",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LatestRequiredDate",
                table: "RawMaterialLockPlanAndExecution",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaterialPlanCoveredCount",
                table: "RawMaterialLockPlanAndExecution",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LatestRequiredDate",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "MaterialPlanCoveredCount",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "LatestRequiredDate",
                table: "RawMaterialLockPlanAndExecution");

            migrationBuilder.DropColumn(
                name: "MaterialPlanCoveredCount",
                table: "RawMaterialLockPlanAndExecution");
        }
    }
}
