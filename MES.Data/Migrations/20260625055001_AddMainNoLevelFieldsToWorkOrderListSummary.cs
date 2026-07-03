using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMainNoLevelFieldsToWorkOrderListSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MainNoMaxStandardCycle",
                table: "WorkOrderListSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TheoreticalCutoffDate",
                table: "WorkOrderListSummary",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TheoreticalWorkDays",
                table: "WorkOrderListSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MainNoMaxStandardCycle",
                table: "WorkOrderListSummary");

            migrationBuilder.DropColumn(
                name: "TheoreticalCutoffDate",
                table: "WorkOrderListSummary");

            migrationBuilder.DropColumn(
                name: "TheoreticalWorkDays",
                table: "WorkOrderListSummary");
        }
    }
}
