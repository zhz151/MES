using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSteelPropertyToGradeMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SteelProperty",
                table: "StandardGradeMapping",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "镍基合金");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SteelProperty",
                table: "StandardGradeMapping");
        }
    }
}
