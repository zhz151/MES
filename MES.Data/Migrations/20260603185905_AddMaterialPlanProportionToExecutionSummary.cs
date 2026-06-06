using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialPlanProportionToExecutionSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaterialPlanProportion",
                table: "WorkOrderExecutionSummary",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaterialPlanProportion",
                table: "RawMaterialLockPlanAndExecution",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaterialPlanProportion",
                table: "WorkOrderExecutionSummary");

            migrationBuilder.DropColumn(
                name: "MaterialPlanProportion",
                table: "RawMaterialLockPlanAndExecution");
        }
    }
}
