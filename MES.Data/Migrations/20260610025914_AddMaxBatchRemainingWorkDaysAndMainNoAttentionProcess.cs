using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxBatchRemainingWorkDaysAndMainNoAttentionProcess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MainNoAttentionProcess",
                table: "WorkOrderExecutionSummary",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxBatchRemainingWorkDays",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MainNoAttentionProcess",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "MaxBatchRemainingWorkDays",
                table: "WorkOrderExecutionSummary");
        }
    }
}
