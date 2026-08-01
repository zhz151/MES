using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductionBatchAddCutFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CutDoubt",
                table: "ProductionBatch",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CutExecution",
                table: "ProductionBatch",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CutQuantity",
                table: "ProductionBatch",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CutRequirement",
                table: "ProductionBatch",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CutDoubt",
                table: "ProductionBatch");

            migrationBuilder.DropColumn(
                name: "CutExecution",
                table: "ProductionBatch");

            migrationBuilder.DropColumn(
                name: "CutQuantity",
                table: "ProductionBatch");

            migrationBuilder.DropColumn(
                name: "CutRequirement",
                table: "ProductionBatch");
        }
    }
}
