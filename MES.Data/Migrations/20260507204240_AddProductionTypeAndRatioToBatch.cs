using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionTypeAndRatioToBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProductionRatio",
                table: "ProductionBatch",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionType",
                table: "ProductionBatch",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OuterDiameterTolerance",
                table: "ProcessGroup",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WallThicknessTolerance",
                table: "ProcessGroup",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductionRatio",
                table: "ProductionBatch");

            migrationBuilder.DropColumn(
                name: "ProductionType",
                table: "ProductionBatch");

            migrationBuilder.DropColumn(
                name: "OuterDiameterTolerance",
                table: "ProcessGroup");

            migrationBuilder.DropColumn(
                name: "WallThicknessTolerance",
                table: "ProcessGroup");
        }
    }
}
