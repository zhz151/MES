using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropSectionOutsourceUK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_SectionOutsource_Section",
                table: "SectionOutsource");

            migrationBuilder.CreateIndex(
                name: "IX_SectionOutsource_Section",
                table: "SectionOutsource",
                columns: new[] { "ProductionBatchId", "ProcessGroupId", "SectionName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SectionOutsource_Section",
                table: "SectionOutsource");

            migrationBuilder.CreateIndex(
                name: "UK_SectionOutsource_Section",
                table: "SectionOutsource",
                columns: new[] { "ProductionBatchId", "ProcessGroupId", "SectionName" },
                unique: true);
        }
    }
}
