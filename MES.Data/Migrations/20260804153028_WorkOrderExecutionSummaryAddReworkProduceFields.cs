using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrderExecutionSummaryAddReworkProduceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PendingReworkOutputQty",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingReworkOutputWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReworkMainNoStatus",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ReworkTheoreticalProduceWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingReworkOutputQty",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "PendingReworkOutputWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ReworkMainNoStatus",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ReworkTheoreticalProduceWeight",
                table: "WorkOrderExecutionSummary");
        }
    }
}
