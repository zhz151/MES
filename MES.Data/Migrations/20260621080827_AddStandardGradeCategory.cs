using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStandardGradeCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_StandardGradeMapping_StandardGrade",
                table: "StandardGradeMapping");

            migrationBuilder.AddColumn<string>(
                name: "StandardGradeCategory",
                table: "StandardGradeMapping",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UK_StandardGradeMapping_StandardGrade_Category",
                table: "StandardGradeMapping",
                columns: new[] { "StandardGrade", "StandardGradeCategory" },
                unique: true,
                filter: "[StandardGradeCategory] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_StandardGradeMapping_StandardGrade_Category",
                table: "StandardGradeMapping");

            migrationBuilder.DropColumn(
                name: "StandardGradeCategory",
                table: "StandardGradeMapping");

            migrationBuilder.CreateIndex(
                name: "UK_StandardGradeMapping_StandardGrade",
                table: "StandardGradeMapping",
                column: "StandardGrade",
                unique: true);
        }
    }
}
