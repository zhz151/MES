using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTheoryWeightToProcessInspection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TheoreticalReworkWeight",
                table: "ProcessInspection",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TheoreticalScrapWeight",
                table: "ProcessInspection",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TheoreticalWarehouseWeight",
                table: "ProcessInspection",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TheoreticalReworkWeight",
                table: "ProcessInspection");

            migrationBuilder.DropColumn(
                name: "TheoreticalScrapWeight",
                table: "ProcessInspection");

            migrationBuilder.DropColumn(
                name: "TheoreticalWarehouseWeight",
                table: "ProcessInspection");
        }
    }
}
