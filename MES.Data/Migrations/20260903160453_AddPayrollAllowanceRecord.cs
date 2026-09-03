using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollAllowanceRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollAllowanceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    FullAttendanceBonus = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SeniorityBonus = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NightShiftAllowance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PositionAllowance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    HighTempAllowance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InjurySubsidy = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LeadBonus = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Penalty = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SocialSecurity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollAllowanceRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_PayrollAllowance_Employee_Month",
                table: "PayrollAllowanceRecords",
                columns: new[] { "EmployeeId", "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollAllowanceRecords");
        }
    }
}
