using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderExecutionGroup6ReworkFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReworkBatchCount",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReworkInputEndDate",
                table: "WorkOrderExecutionSummary",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReworkInputQuantity",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ReworkInputWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReworkTheoreticalOutputQty",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReworkTheoreticalOutputWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReworkBatchCount",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ReworkInputEndDate",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ReworkInputQuantity",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ReworkInputWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ReworkTheoreticalOutputQty",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ReworkTheoreticalOutputWeight",
                table: "WorkOrderExecutionSummary");
        }
    }
}
