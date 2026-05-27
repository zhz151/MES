using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStandardProcessCycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StandardProcessCycle",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RawMaterialType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RawSpec = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProductSpec = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeliveryState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StandardCycleDays = table.Column<int>(type: "int", nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandardProcessCycle", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StandardProcessCycle_PlantGrade",
                table: "StandardProcessCycle",
                column: "PlantGrade");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StandardProcessCycle");
        }
    }
}
