using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveScheduleDateFromColdRollSpecSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_CRSS_Dimensions",
                table: "ColdRollSpecSchedule");

            migrationBuilder.DropColumn(
                name: "ScheduleDate",
                table: "ColdRollSpecSchedule");

            migrationBuilder.CreateIndex(
                name: "UK_CRSS_Dimensions",
                table: "ColdRollSpecSchedule",
                columns: new[] { "ProcessType", "BilletSpec", "RollingSpec", "IsFinished" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_CRSS_Dimensions",
                table: "ColdRollSpecSchedule");

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduleDate",
                table: "ColdRollSpecSchedule",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "UK_CRSS_Dimensions",
                table: "ColdRollSpecSchedule",
                columns: new[] { "ScheduleDate", "ProcessType", "BilletSpec", "RollingSpec", "IsFinished" },
                unique: true);
        }
    }
}
