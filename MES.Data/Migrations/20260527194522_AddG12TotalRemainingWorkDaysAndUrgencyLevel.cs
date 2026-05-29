using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddG12TotalRemainingWorkDaysAndUrgencyLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TotalRemainingWorkDays",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrgencyLevel",
                table: "WorkOrderExecutionSummary",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalRemainingWorkDays",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "UrgencyLevel",
                table: "WorkOrderExecutionSummary");
        }
    }
}
