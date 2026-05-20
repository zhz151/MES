using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQualifiedConcessionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConcessionRemark",
                table: "ProcessInspection",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QualifiedConcessionQuantity",
                table: "ProcessInspection",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConcessionRemark",
                table: "FinalInspection",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QualifiedConcessionQuantity",
                table: "FinalInspection",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConcessionRemark",
                table: "ProcessInspection");

            migrationBuilder.DropColumn(
                name: "QualifiedConcessionQuantity",
                table: "ProcessInspection");

            migrationBuilder.DropColumn(
                name: "ConcessionRemark",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "QualifiedConcessionQuantity",
                table: "FinalInspection");
        }
    }
}
