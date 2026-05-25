using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderExecutionG11WarehousingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MainNoWarehousingStatus",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrderWarehousingStatus",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "WarehousingEndDate",
                table: "WorkOrderExecutionSummary",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WarehousingStartDate",
                table: "WorkOrderExecutionSummary",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarehousingTotalQty",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "WarehousingTotalWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "WoWarehousingStatus",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MainNoWarehousingStatus",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "OrderWarehousingStatus",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "WarehousingEndDate",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "WarehousingStartDate",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "WarehousingTotalQty",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "WarehousingTotalWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "WoWarehousingStatus",
                table: "WorkOrderExecutionSummary");
        }
    }
}
