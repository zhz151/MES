using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSolutionTemperatureAndSoakTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SoakTime",
                table: "ProductionRecord",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SolutionTemperature",
                table: "ProductionRecord",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SoakTime",
                table: "ProductionRecord");

            migrationBuilder.DropColumn(
                name: "SolutionTemperature",
                table: "ProductionRecord");
        }
    }
}
