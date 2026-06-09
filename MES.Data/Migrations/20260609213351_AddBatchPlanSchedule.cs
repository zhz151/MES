using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchPlanSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BatchPlanSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    IsFlow = table.Column<bool>(type: "bit", nullable: false),
                    FlowLevel = table.Column<int>(type: "int", nullable: false),
                    FlowTarget = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FlowCRType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FlowExecSpec = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TargetSequence = table.Column<int>(type: "int", nullable: true),
                    ExecutionSequence = table.Column<int>(type: "int", nullable: true),
                    IsGrabOrder = table.Column<bool>(type: "bit", nullable: false),
                    PlanRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchPlanSchedules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_BPS_BatchId",
                table: "BatchPlanSchedules",
                column: "BatchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BatchPlanSchedules");
        }
    }
}
