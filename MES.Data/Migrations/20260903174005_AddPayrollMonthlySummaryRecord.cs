using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollMonthlySummaryRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollMonthlySummaryRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    AttendanceDays = table.Column<int>(type: "int", nullable: false),
                    BaseWage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MiscWorkAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PositionAllowance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SeniorityBonus = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FullAttendanceBonus = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LeadBonus = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NightShiftAllowance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HighTempAllowance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InjurySubsidy = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Penalty = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SocialSecurity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPayable = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollMonthlySummaryRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_PayrollMonthlySummary_Employee_Month",
                table: "PayrollMonthlySummaryRecords",
                columns: new[] { "EmployeeId", "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollMonthlySummaryRecords");
        }
    }
}
