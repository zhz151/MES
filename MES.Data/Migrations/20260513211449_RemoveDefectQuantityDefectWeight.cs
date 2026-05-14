using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDefectQuantityDefectWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefectQuantity",
                table: "ProductionRecord");

            migrationBuilder.DropColumn(
                name: "DefectWeight",
                table: "ProductionRecord");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefectQuantity",
                table: "ProductionRecord",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefectWeight",
                table: "ProductionRecord",
                type: "decimal(18,3)",
                nullable: true);
        }
    }
}
