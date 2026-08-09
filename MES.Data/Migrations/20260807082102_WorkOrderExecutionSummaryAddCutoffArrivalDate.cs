using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrderExecutionSummaryAddCutoffArrivalDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CutoffArrivalDate",
                table: "WorkOrderExecutionSummary",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MainNoCutoffArrivalDate",
                table: "WorkOrderExecutionSummary",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CutoffArrivalDate",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "MainNoCutoffArrivalDate",
                table: "WorkOrderExecutionSummary");
        }
    }
}
