using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameTheoreticalWorkDaysToCapacityWorkDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TheoreticalWorkDays",
                table: "WorkOrderListSummary",
                newName: "CapacityWorkDays");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CapacityWorkDays",
                table: "WorkOrderListSummary",
                newName: "TheoreticalWorkDays");
        }
    }
}
