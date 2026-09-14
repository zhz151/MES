using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderExecutionDefectReturnWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FinalInspectionReturnWeight",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessInspectionReturnWeight",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalInspectionReturnWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ProcessInspectionReturnWeight",
                table: "WorkOrderExecutionSummary");
        }
    }
}
