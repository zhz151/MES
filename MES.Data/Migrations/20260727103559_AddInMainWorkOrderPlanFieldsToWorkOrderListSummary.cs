using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInMainWorkOrderPlanFieldsToWorkOrderListSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InMainWorkOrderPlanTotalPieces",
                table: "WorkOrderListSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InMainWorkOrderPlanTotalWeight",
                table: "WorkOrderListSummary",
                type: "decimal(18,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InMainWorkOrderPlanTotalPieces",
                table: "WorkOrderListSummary");

            migrationBuilder.DropColumn(
                name: "InMainWorkOrderPlanTotalWeight",
                table: "WorkOrderListSummary");
        }
    }
}
