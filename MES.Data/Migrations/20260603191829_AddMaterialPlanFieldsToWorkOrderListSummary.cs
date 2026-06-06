using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialPlanFieldsToWorkOrderListSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LatestRequiredDate",
                table: "WorkOrderListSummary",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaterialPlanCoveredCount",
                table: "WorkOrderListSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MaterialPlanProportion",
                table: "WorkOrderListSummary",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LatestRequiredDate",
                table: "WorkOrderListSummary");

            migrationBuilder.DropColumn(
                name: "MaterialPlanCoveredCount",
                table: "WorkOrderListSummary");

            migrationBuilder.DropColumn(
                name: "MaterialPlanProportion",
                table: "WorkOrderListSummary");
        }
    }
}
