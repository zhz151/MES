using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNcrModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ncr",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReportDepartment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Reporter = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PipeCategory = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WorkOrderNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SalesOrderNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TagNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DefectiveQuantity = table.Column<int>(type: "int", nullable: true),
                    ProblemDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisposalMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DisposalRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisposalIsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    DisposalCompleteDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RootCauseAnalysis = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AnalysisConfirmer = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AnalysisConfirmDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponsibilityCategory = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ResponsibleDept = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OperationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponsiblePerson = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PersonDisposition = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PersonIsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    PersonCompleteDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrectiveAction = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActionPlanner = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ActionPlanDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActionVerifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ActionVerifyDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActionResult = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VerifyResult = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ncr", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ncr_BatchNo",
                table: "Ncr",
                column: "BatchNo");

            migrationBuilder.CreateIndex(
                name: "IX_Ncr_DisposalMethod",
                table: "Ncr",
                column: "DisposalMethod");

            migrationBuilder.CreateIndex(
                name: "IX_Ncr_ReportDate",
                table: "Ncr",
                column: "ReportDate");

            migrationBuilder.CreateIndex(
                name: "IX_Ncr_Severity",
                table: "Ncr",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_Ncr_Status",
                table: "Ncr",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ncr");
        }
    }
}
