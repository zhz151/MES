using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropPieceRateStandard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PieceRateStandards");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PieceRateStandards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CutRateMax = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CutRateMin = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CutRateRangeText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CutRateRatio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DeviceCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DimensionKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "date", nullable: true),
                    FixedLengthCountMax = table.Column<int>(type: "int", nullable: true),
                    FixedLengthCountMin = table.Column<int>(type: "int", nullable: true),
                    FixedLengthCountRangeText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FixedLengthCountRatio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LengthMax = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    LengthMin = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    LengthRangeText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LengthRatio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OuterDiameterMax = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    OuterDiameterMin = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    OuterDiameterRangeText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OuterDiameterRatio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PieceRateType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProductRatio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ProductStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SectionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SpecialGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SpecialGradeRatio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SpecialState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SpecialStateRatio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalRatio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WallThicknessMax = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    WallThicknessMin = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    WallThicknessRangeText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WallThicknessRatio = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PieceRateStandards", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PieceRateStandard_SectionName",
                table: "PieceRateStandards",
                column: "SectionName");

            migrationBuilder.CreateIndex(
                name: "IX_PieceRateStandard_SectionName_Active",
                table: "PieceRateStandards",
                columns: new[] { "SectionName", "IsActive" });
        }
    }
}
