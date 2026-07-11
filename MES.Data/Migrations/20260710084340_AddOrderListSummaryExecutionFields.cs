using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderListSummaryExecutionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EstimatedCompletionDate",
                table: "OrderListSummary",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScheduleStage",
                table: "OrderListSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrgencyLevel",
                table: "OrderListSummary",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedCompletionDate",
                table: "OrderListSummary");

            migrationBuilder.DropColumn(
                name: "ScheduleStage",
                table: "OrderListSummary");

            migrationBuilder.DropColumn(
                name: "UrgencyLevel",
                table: "OrderListSummary");
        }
    }
}
