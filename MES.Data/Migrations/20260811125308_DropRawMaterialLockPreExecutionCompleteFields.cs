using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropRawMaterialLockPreExecutionCompleteFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBudgetComplete",
                table: "RawMaterialLockPreExecution");

            migrationBuilder.DropColumn(
                name: "IsMainNoMaterialComplete",
                table: "RawMaterialLockPreExecution");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBudgetComplete",
                table: "RawMaterialLockPreExecution",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsMainNoMaterialComplete",
                table: "RawMaterialLockPreExecution",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
