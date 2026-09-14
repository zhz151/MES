using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityDefectReturnFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefectReturnQuantity",
                table: "ProcessInspection",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TheoreticalReturnWeight",
                table: "ProcessInspection",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefectReturnQuantity",
                table: "FinalInspection",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefectReturnWeight",
                table: "FinalInspection",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefectReturnQuantity",
                table: "ProcessInspection");

            migrationBuilder.DropColumn(
                name: "TheoreticalReturnWeight",
                table: "ProcessInspection");

            migrationBuilder.DropColumn(
                name: "DefectReturnQuantity",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "DefectReturnWeight",
                table: "FinalInspection");
        }
    }
}
