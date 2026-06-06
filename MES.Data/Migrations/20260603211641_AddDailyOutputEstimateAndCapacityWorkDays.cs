using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyOutputEstimateAndCapacityWorkDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CapacityWorkDays",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CapacityWorkDays",
                table: "RawMaterialLockPlanAndExecution",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DailyOutputEstimates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinOuterDiameter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DailyOutputTons = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyOutputEstimates", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyOutputEstimates");

            migrationBuilder.DropColumn(
                name: "CapacityWorkDays",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "CapacityWorkDays",
                table: "RawMaterialLockPlanAndExecution");
        }
    }
}
