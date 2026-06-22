using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHardnessTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HardnessTest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InspectionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Inspector = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FurnaceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Grade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SampleNo = table.Column<int>(type: "int", nullable: true),
                    SampleSize = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InspectionStandard = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HardnessMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    HardnessValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Judgment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardnessTest", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HardnessTest_FurnaceNo",
                table: "HardnessTest",
                column: "FurnaceNo");

            migrationBuilder.CreateIndex(
                name: "IX_HardnessTest_Grade",
                table: "HardnessTest",
                column: "Grade");

            migrationBuilder.CreateIndex(
                name: "IX_HardnessTest_InspectionDate",
                table: "HardnessTest",
                column: "InspectionDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HardnessTest");
        }
    }
}
