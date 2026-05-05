using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubcontractReturnItemMissingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlantGrade",
                table: "SubcontractReturnItem",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remark",
                table: "SubcontractReturnItem",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredQuantity",
                table: "SubcontractReturnItem",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RequiredWeight",
                table: "SubcontractReturnItem",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitWeight",
                table: "SubcontractReturnItem",
                type: "decimal(18,4)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlantGrade",
                table: "SubcontractReturnItem");

            migrationBuilder.DropColumn(
                name: "Remark",
                table: "SubcontractReturnItem");

            migrationBuilder.DropColumn(
                name: "RequiredQuantity",
                table: "SubcontractReturnItem");

            migrationBuilder.DropColumn(
                name: "RequiredWeight",
                table: "SubcontractReturnItem");

            migrationBuilder.DropColumn(
                name: "UnitWeight",
                table: "SubcontractReturnItem");
        }
    }
}
