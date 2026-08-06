using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEnglishNameColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnglishName",
                table: "StandardWorkDays");

            migrationBuilder.DropColumn(
                name: "EnglishName",
                table: "ProcessDefinitions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnglishName",
                table: "StandardWorkDays",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnglishName",
                table: "ProcessDefinitions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
