using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropProductionRecordUK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_ProductionRecord_Section",
                table: "ProductionRecord");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecord_Section",
                table: "ProductionRecord",
                columns: new[] { "ProductionBatchId", "ProcessGroupId", "SectionName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductionRecord_Section",
                table: "ProductionRecord");

            migrationBuilder.CreateIndex(
                name: "UK_ProductionRecord_Section",
                table: "ProductionRecord",
                columns: new[] { "ProductionBatchId", "ProcessGroupId", "SectionName" },
                unique: true);
        }
    }
}
