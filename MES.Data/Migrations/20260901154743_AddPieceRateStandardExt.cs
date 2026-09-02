using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPieceRateStandardExt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveDate",
                table: "PieceRateStandards",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "PieceRateStandards",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MaterialCategory",
                table: "PieceRateStandards",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaterialRatio",
                table: "PieceRateStandards",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PieceRateType",
                table: "PieceRateStandards",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PieceRateStandard_SectionName_Active",
                table: "PieceRateStandards",
                columns: new[] { "SectionName", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PieceRateStandard_SectionName_Active",
                table: "PieceRateStandards");

            migrationBuilder.DropColumn(
                name: "EffectiveDate",
                table: "PieceRateStandards");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "PieceRateStandards");

            migrationBuilder.DropColumn(
                name: "MaterialCategory",
                table: "PieceRateStandards");

            migrationBuilder.DropColumn(
                name: "MaterialRatio",
                table: "PieceRateStandards");

            migrationBuilder.DropColumn(
                name: "PieceRateType",
                table: "PieceRateStandards");
        }
    }
}
