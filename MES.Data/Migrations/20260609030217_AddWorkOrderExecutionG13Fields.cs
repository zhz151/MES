using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderExecutionG13Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdjustmentRemark",
                table: "WorkOrderExecutionSummary",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBatchDelivery",
                table: "WorkOrderExecutionSummary",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPaused",
                table: "WorkOrderExecutionSummary",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsUrging",
                table: "WorkOrderExecutionSummary",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProductionFlowProperty",
                table: "WorkOrderExecutionSummary",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdjustmentRemark",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "IsBatchDelivery",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "IsPaused",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "IsUrging",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ProductionFlowProperty",
                table: "WorkOrderExecutionSummary");
        }
    }
}
