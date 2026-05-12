using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceWarehouseFieldsToBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceLengthStatus",
                table: "ProductionBatch",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcePlantGrade",
                table: "ProductionBatch",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceSpecification",
                table: "ProductionBatch",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SourceUnitWeight",
                table: "ProductionBatch",
                type: "decimal(18,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceLengthStatus",
                table: "ProductionBatch");

            migrationBuilder.DropColumn(
                name: "SourcePlantGrade",
                table: "ProductionBatch");

            migrationBuilder.DropColumn(
                name: "SourceSpecification",
                table: "ProductionBatch");

            migrationBuilder.DropColumn(
                name: "SourceUnitWeight",
                table: "ProductionBatch");
        }
    }
}
