using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChemicalAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChemicalAnalysis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnalysisDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Analyst = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FurnaceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Grade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AnalysisCount = table.Column<int>(type: "int", nullable: true),
                    AnalysisStandard = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    C = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Si = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Mn = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    P = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    S = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Ni = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Cr = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Mo = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Cu = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    N = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Nb = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Ti = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Fe = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Al = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    W = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChemicalAnalysis", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChemicalAnalysis_AnalysisDate",
                table: "ChemicalAnalysis",
                column: "AnalysisDate");

            migrationBuilder.CreateIndex(
                name: "IX_ChemicalAnalysis_FurnaceNo",
                table: "ChemicalAnalysis",
                column: "FurnaceNo");

            migrationBuilder.CreateIndex(
                name: "IX_ChemicalAnalysis_Grade",
                table: "ChemicalAnalysis",
                column: "Grade");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChemicalAnalysis");
        }
    }
}
