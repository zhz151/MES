using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderDemandAdjustmentForceCompleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsForceCompleted",
                table: "WorkOrderExecutionSummary",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsForceCompleted",
                table: "OrderDemandAdjustment",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsForceCompleted",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "IsForceCompleted",
                table: "OrderDemandAdjustment");
        }
    }
}
