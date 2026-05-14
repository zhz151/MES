using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFurnaceNumberUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_FurnaceRegistration_FurnaceNumber",
                table: "FurnaceRegistration");

            migrationBuilder.CreateIndex(
                name: "IX_FurnaceRegistration_FurnaceNumber",
                table: "FurnaceRegistration",
                column: "FurnaceNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FurnaceRegistration_FurnaceNumber",
                table: "FurnaceRegistration");

            migrationBuilder.CreateIndex(
                name: "UK_FurnaceRegistration_FurnaceNumber",
                table: "FurnaceRegistration",
                column: "FurnaceNumber",
                unique: true);
        }
    }
}
