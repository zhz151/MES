using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPicklingInRecordExecutionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EquipmentName",
                table: "PicklingInRecord",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFinished",
                table: "PicklingInRecord",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Operator",
                table: "PicklingInRecord",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "PicklingInRecord",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Shift",
                table: "PicklingInRecord",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                table: "PicklingInRecord",
                type: "decimal(18,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EquipmentName",
                table: "PicklingInRecord");

            migrationBuilder.DropColumn(
                name: "IsFinished",
                table: "PicklingInRecord");

            migrationBuilder.DropColumn(
                name: "Operator",
                table: "PicklingInRecord");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "PicklingInRecord");

            migrationBuilder.DropColumn(
                name: "Shift",
                table: "PicklingInRecord");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "PicklingInRecord");
        }
    }
}
