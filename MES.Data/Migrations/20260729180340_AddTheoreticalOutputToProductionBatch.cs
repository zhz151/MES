using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTheoreticalOutputToProductionBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TheoreticalOutputQty",
                table: "ProductionBatch",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TheoreticalOutputWeight",
                table: "ProductionBatch",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TheoreticalUnitWeight",
                table: "ProductionBatch",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TheoreticalOutputQty",
                table: "ProductionBatch");

            migrationBuilder.DropColumn(
                name: "TheoreticalOutputWeight",
                table: "ProductionBatch");

            migrationBuilder.DropColumn(
                name: "TheoreticalUnitWeight",
                table: "ProductionBatch");
        }
    }
}
