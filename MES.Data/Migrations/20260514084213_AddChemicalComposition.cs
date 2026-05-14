using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChemicalComposition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChemicalComposition",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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
                    PREN = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChemicalComposition", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_ChemicalComposition_PlantGrade",
                table: "ChemicalComposition",
                column: "PlantGrade",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChemicalComposition");
        }
    }
}
