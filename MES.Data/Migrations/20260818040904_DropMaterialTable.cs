using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropMaterialTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Material");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Material",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MaterialCategory = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MaterialCode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Material", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Material_Category",
                table: "Material",
                column: "MaterialCategory");

            migrationBuilder.CreateIndex(
                name: "IX_Material_IsActive",
                table: "Material",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "UK_Material_Code",
                table: "Material",
                column: "MaterialCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_Material_Combo",
                table: "Material",
                columns: new[] { "MaterialCategory", "PlantGrade", "Specification" },
                unique: true);
        }
    }
}
