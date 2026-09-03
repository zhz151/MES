using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollAttendanceWageRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollAttendanceWageRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    WageYear = table.Column<int>(type: "int", nullable: false),
                    WageMonth = table.Column<int>(type: "int", nullable: false),
                    AttendancePositions = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AttendanceHours = table.Column<decimal>(type: "decimal(18,1)", nullable: true),
                    AttendanceCoefficient = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollAttendanceWageRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_PayrollAttendanceWage_Employee_Month",
                table: "PayrollAttendanceWageRecords",
                columns: new[] { "EmployeeId", "WageYear", "WageMonth" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollAttendanceWageRecords");
        }
    }
}
