using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGradeChemicalCompositionAndPhysicalProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GradeChemicalComposition",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StandardGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StandardGradeCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Carbon = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Silicon = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Manganese = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phosphorus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Sulfur = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Nickel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Chromium = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Molybdenum = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Copper = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Nitrogen = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Niobium = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Titanium = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Iron = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Aluminum = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Tungsten = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeChemicalComposition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GradePhysicalProperty",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StandardGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StandardGradeCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Density = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    HeatTreatmentTemp = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HardnessRockwell = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HardnessVickers = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HardnessBrinell = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TensileStrength = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    YieldStrength02 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    YieldStrength10 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Elongation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GrainSize = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradePhysicalProperty", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_GradeChemicalComposition_StandardGrade_Category",
                table: "GradeChemicalComposition",
                columns: new[] { "StandardGrade", "StandardGradeCategory" },
                unique: true,
                filter: "[StandardGradeCategory] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_GradePhysicalProperty_StandardGrade_Category",
                table: "GradePhysicalProperty",
                columns: new[] { "StandardGrade", "StandardGradeCategory" },
                unique: true,
                filter: "[StandardGradeCategory] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GradeChemicalComposition");

            migrationBuilder.DropTable(
                name: "GradePhysicalProperty");
        }
    }
}
