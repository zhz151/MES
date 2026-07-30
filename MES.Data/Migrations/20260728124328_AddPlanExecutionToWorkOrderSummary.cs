using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanExecutionToWorkOrderSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FinishInStatus",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "FinishInWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "FinishOrderStatus",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "FinishOrderWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FinishPendingWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FinishPlanWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "InMainInputStatus",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "InMainInputWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InMainPlanWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "InProcessReworkInputStatus",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "InProcessReworkInputWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InProcessReworkPlanWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "InventoryOutStatus",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "InventoryOutWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InventoryPlanWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PiercingPlanWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PiercingReturnStatus",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PiercingSubInWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PiercingSubOutWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PiercingSubPendingWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PiercingSubStatus",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReworkPlanInputStatus",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ReworkPlanInputWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReworkPlanWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SemiInStatus",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SemiInWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SemiOrderStatus",
                table: "WorkOrderExecutionSummary",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SemiOrderWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SemiPendingWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SemiPlanWeight",
                table: "WorkOrderExecutionSummary",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinishInStatus",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "FinishInWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "FinishOrderStatus",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "FinishOrderWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "FinishPendingWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "FinishPlanWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "InMainInputStatus",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "InMainInputWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "InMainPlanWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "InProcessReworkInputStatus",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "InProcessReworkInputWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "InProcessReworkPlanWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "InventoryOutStatus",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "InventoryOutWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "InventoryPlanWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "PiercingPlanWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "PiercingReturnStatus",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "PiercingSubInWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "PiercingSubOutWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "PiercingSubPendingWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "PiercingSubStatus",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ReworkPlanInputStatus",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ReworkPlanInputWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "ReworkPlanWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "SemiInStatus",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "SemiInWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "SemiOrderStatus",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "SemiOrderWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "SemiPendingWeight",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "SemiPlanWeight",
                table: "WorkOrderExecutionSummary");
        }
    }
}
