using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveG14Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentRawMaterialLockRemark",
                table: "RawMaterialLockPlanAndExecution");

            migrationBuilder.DropColumn(
                name: "CurrentScheduleStage",
                table: "RawMaterialLockPlanAndExecution");

            migrationBuilder.DropColumn(
                name: "IsExecuted",
                table: "RawMaterialLockPlanAndExecution");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentRawMaterialLockRemark",
                table: "RawMaterialLockPlanAndExecution",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentScheduleStage",
                table: "RawMaterialLockPlanAndExecution",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExecuted",
                table: "RawMaterialLockPlanAndExecution",
                type: "bit",
                nullable: true);
        }
    }
}
