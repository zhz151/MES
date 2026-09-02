using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPieceRateStandard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PieceRateStandards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MachineType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OuterDiameterRangeText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OuterDiameterMin = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    OuterDiameterMax = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    OuterDiameterRatio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    WallThicknessRangeText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WallThicknessMin = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    WallThicknessMax = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    WallThicknessRatio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LengthRangeText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LengthMin = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    LengthMax = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    LengthRatio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CutRateRangeText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CutRateMin = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CutRateMax = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CutRateRatio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    FixedLengthCountRangeText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FixedLengthCountMin = table.Column<int>(type: "int", nullable: true),
                    FixedLengthCountMax = table.Column<int>(type: "int", nullable: true),
                    FixedLengthCountRatio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsFinishedProduct = table.Column<bool>(type: "bit", nullable: true),
                    FinishedProductRatio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalRatio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PieceRateStandards", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PieceRateStandard_SectionName",
                table: "PieceRateStandards",
                column: "SectionName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PieceRateStandards");
        }
    }
}
