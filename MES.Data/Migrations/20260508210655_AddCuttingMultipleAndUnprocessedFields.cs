using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCuttingMultipleAndUnprocessedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CuttingRate",
                table: "ProductionRecord",
                newName: "CuttingMultiple");

            migrationBuilder.AddColumn<int>(
                name: "UnprocessedQuantity",
                table: "OutsourceRecovery",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnprocessedWeight",
                table: "OutsourceRecovery",
                type: "decimal(18,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnprocessedQuantity",
                table: "OutsourceRecovery");

            migrationBuilder.DropColumn(
                name: "UnprocessedWeight",
                table: "OutsourceRecovery");

            migrationBuilder.RenameColumn(
                name: "CuttingMultiple",
                table: "ProductionRecord",
                newName: "CuttingRate");
        }
    }
}
