using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeMaterialReceiveCheckUniqueIndexToProcessGroupId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MaterialReceiveCheck_ProcessGroupId",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropIndex(
                name: "UK_MaterialReceiveCheck_BatchId",
                table: "MaterialReceiveCheck");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReceiveCheck_ProductionBatchId",
                table: "MaterialReceiveCheck",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "UK_MaterialReceiveCheck_ProcessGroup",
                table: "MaterialReceiveCheck",
                column: "ProcessGroupId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MaterialReceiveCheck_ProductionBatchId",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropIndex(
                name: "UK_MaterialReceiveCheck_ProcessGroup",
                table: "MaterialReceiveCheck");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReceiveCheck_ProcessGroupId",
                table: "MaterialReceiveCheck",
                column: "ProcessGroupId");

            migrationBuilder.CreateIndex(
                name: "UK_MaterialReceiveCheck_BatchId",
                table: "MaterialReceiveCheck",
                column: "ProductionBatchId",
                unique: true);
        }
    }
}
