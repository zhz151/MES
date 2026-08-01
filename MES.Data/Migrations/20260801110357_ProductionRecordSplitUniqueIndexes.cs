using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductionRecordSplitUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_ProductionRecord_Section",
                table: "ProductionRecord");

            migrationBuilder.CreateIndex(
                name: "UK_ProductionRecord_Section",
                table: "ProductionRecord",
                columns: new[] { "ProductionBatchId", "ProcessGroupId", "SectionName" },
                unique: true,
                filter: "[FinishedCutLength] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UK_ProductionRecord_Section_Cut",
                table: "ProductionRecord",
                columns: new[] { "ProductionBatchId", "ProcessGroupId", "SectionName", "ExecDate", "FinishedCutLength" },
                unique: true,
                filter: "[FinishedCutLength] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_ProductionRecord_Section",
                table: "ProductionRecord");

            migrationBuilder.DropIndex(
                name: "UK_ProductionRecord_Section_Cut",
                table: "ProductionRecord");

            migrationBuilder.CreateIndex(
                name: "UK_ProductionRecord_Section",
                table: "ProductionRecord",
                columns: new[] { "ProductionBatchId", "ProcessGroupId", "SectionName" },
                unique: true);
        }
    }
}
