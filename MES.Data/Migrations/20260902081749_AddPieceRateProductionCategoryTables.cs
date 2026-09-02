using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPieceRateProductionCategoryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PieceRateProductionCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProcessKeys = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProductStatusKeys = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StageKeys = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BasePrice = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PieceRateProductionCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PieceRateProductionCategoryTiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    DimensionKey = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RangeText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MinValue = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    MaxValue = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    MinInt = table.Column<int>(type: "int", nullable: true),
                    MaxInt = table.Column<int>(type: "int", nullable: true),
                    MatchValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ratio = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PieceRateProductionCategoryTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PieceRateProductionCategoryTiers_PieceRateProductionCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "PieceRateProductionCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Category_Section_Active",
                table: "PieceRateProductionCategories",
                columns: new[] { "SectionKey", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Tier_Category",
                table: "PieceRateProductionCategoryTiers",
                column: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PieceRateProductionCategoryTiers");

            migrationBuilder.DropTable(
                name: "PieceRateProductionCategories");
        }
    }
}
