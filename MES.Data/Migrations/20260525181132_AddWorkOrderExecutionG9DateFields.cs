using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderExecutionG9DateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InspectionEndDate",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "InspectionStartDate",
                table: "WorkOrderExecutionSummary");
        }
    }
}
