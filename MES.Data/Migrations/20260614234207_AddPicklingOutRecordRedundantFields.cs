using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPicklingOutRecordRedundantFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EquipmentName",
                table: "PicklingOutRecord",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFinished",
                table: "PicklingOutRecord",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ManufacturingSpec",
                table: "PicklingOutRecord",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Operator",
                table: "PicklingOutRecord",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlantGrade",
                table: "PicklingOutRecord",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductionBatchId",
                table: "PicklingOutRecord",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "PicklingOutRecord",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SectionName",
                table: "PicklingOutRecord",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Shift",
                table: "PicklingOutRecord",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                table: "PicklingOutRecord",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EquipmentName",
                table: "PicklingOutRecord");

            migrationBuilder.DropColumn(
                name: "IsFinished",
                table: "PicklingOutRecord");

            migrationBuilder.DropColumn(
                name: "ManufacturingSpec",
                table: "PicklingOutRecord");

            migrationBuilder.DropColumn(
                name: "Operator",
                table: "PicklingOutRecord");

            migrationBuilder.DropColumn(
                name: "PlantGrade",
                table: "PicklingOutRecord");

            migrationBuilder.DropColumn(
                name: "ProductionBatchId",
                table: "PicklingOutRecord");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "PicklingOutRecord");

            migrationBuilder.DropColumn(
                name: "SectionName",
                table: "PicklingOutRecord");

            migrationBuilder.DropColumn(
                name: "Shift",
                table: "PicklingOutRecord");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "PicklingOutRecord");
        }
    }
}
