using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStandardWorkDayDictionaryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "StandardWorkDays",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EnglishName",
                table: "StandardWorkDays",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "StandardWorkDays",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SectionKey",
                table: "StandardWorkDays",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "StandardWorkDays");

            migrationBuilder.DropColumn(
                name: "EnglishName",
                table: "StandardWorkDays");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "StandardWorkDays");

            migrationBuilder.DropColumn(
                name: "SectionKey",
                table: "StandardWorkDays");
        }
    }
}
