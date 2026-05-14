using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChemicalValidationRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChemicalValidationRule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlantGrade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CMin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CMax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SiMin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SiMax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MnMin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MnMax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PMin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PMax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SMin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SMax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NiMin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NiMax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CrMin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CrMax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MoMin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MoMax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CuMin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CuMax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NMin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NMax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NbMin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NbMax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TiMin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TiMax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FeMin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FeMax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AlMin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AlMax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    WMin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    WMax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PRENMin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChemicalValidationRule", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_ChemicalValidationRule_PlantGrade",
                table: "ChemicalValidationRule",
                column: "PlantGrade",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChemicalValidationRule");
        }
    }
}
