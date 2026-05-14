using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFurnaceRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FurnaceRegistration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncomingDate = table.Column<DateTime>(type: "date", nullable: false),
                    RawMaterialUnit = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RawMaterialType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RegisteredGrade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RelatedPlantGrade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FurnaceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    Carbon = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Silicon = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Manganese = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Phosphorus = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Sulfur = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Nickel = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Chromium = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Molybdenum = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Copper = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Nitrogen = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Niobium = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Titanium = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Iron = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Aluminum = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Tungsten = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    PREN = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FurnaceRegistration", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_FurnaceRegistration_FurnaceNumber",
                table: "FurnaceRegistration",
                column: "FurnaceNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FurnaceRegistration");
        }
    }
}
