using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinalInspectionAddInspectionTypeAndWeightFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InspectionType",
                table: "MaterialReceiveCheck",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefectReworkWeight",
                table: "FinalInspection",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefectScrapWeight",
                table: "FinalInspection",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefectWarehouseWeight",
                table: "FinalInspection",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InspectionType",
                table: "FinalInspection",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InspectionType",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "DefectReworkWeight",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "DefectScrapWeight",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "DefectWarehouseWeight",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "InspectionType",
                table: "FinalInspection");
        }
    }
}
