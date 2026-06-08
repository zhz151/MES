using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddColdRollSpecSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ColdRollSpecSchedule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduleDate = table.Column<DateTime>(type: "date", nullable: false),
                    ProcessType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BilletSpec = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RollingSpec = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsFinished = table.Column<bool>(type: "bit", nullable: false),
                    MachineNo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RollType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "None"),
                    DailyTons = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    RollOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MergeDisplay = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColdRollSpecSchedule", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInspection_BatchNo",
                table: "ProcessInspection",
                column: "BatchNo");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInspection_InspectionDate",
                table: "ProcessInspection",
                column: "InspectionDate");

            migrationBuilder.CreateIndex(
                name: "UK_CRSS_Dimensions",
                table: "ColdRollSpecSchedule",
                columns: new[] { "ScheduleDate", "ProcessType", "BilletSpec", "RollingSpec", "IsFinished" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ColdRollSpecSchedule");

            migrationBuilder.DropIndex(
                name: "IX_ProcessInspection_BatchNo",
                table: "ProcessInspection");

            migrationBuilder.DropIndex(
                name: "IX_ProcessInspection_InspectionDate",
                table: "ProcessInspection");
        }
    }
}
