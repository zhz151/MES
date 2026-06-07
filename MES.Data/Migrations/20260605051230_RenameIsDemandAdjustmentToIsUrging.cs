using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameIsDemandAdjustmentToIsUrging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // OrderDemandAdjustment: IsDemandAdjustment → IsUrging
            migrationBuilder.RenameColumn(
                name: "IsDemandAdjustment",
                table: "OrderDemandAdjustment",
                newName: "IsUrging");

            // WorkOrderSchedule: IsDemandAdjustment → IsUrging
            migrationBuilder.RenameColumn(
                name: "IsDemandAdjustment",
                table: "WorkOrderSchedules",
                newName: "IsUrging");

            // WorkOrderSchedule: add IsPaused column
            migrationBuilder.AddColumn<bool>(
                name: "IsPaused",
                table: "WorkOrderSchedules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // OrderDemandAdjustment: set defaults for IsBatchDelivery/IsPaused
            // (existing columns need AlterColumn to add default values)
            migrationBuilder.AlterColumn<bool>(
                name: "IsBatchDelivery",
                table: "OrderDemandAdjustment",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsPaused",
                table: "OrderDemandAdjustment",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // OrderDemandAdjustment: IsUrging → IsDemandAdjustment
            migrationBuilder.RenameColumn(
                name: "IsUrging",
                table: "OrderDemandAdjustment",
                newName: "IsDemandAdjustment");

            // WorkOrderSchedule: IsUrging → IsDemandAdjustment
            migrationBuilder.RenameColumn(
                name: "IsUrging",
                table: "WorkOrderSchedules",
                newName: "IsDemandAdjustment");

            // WorkOrderSchedule: remove IsPaused
            migrationBuilder.DropColumn(
                name: "IsPaused",
                table: "WorkOrderSchedules");

            // OrderDemandAdjustment: revert defaults
            migrationBuilder.AlterColumn<bool>(
                name: "IsBatchDelivery",
                table: "OrderDemandAdjustment",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsPaused",
                table: "OrderDemandAdjustment",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);
        }
    }
}
