using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSortIndexesToMaterialReceiveCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MaterialReceiveCheck_BatchNo",
                table: "MaterialReceiveCheck",
                column: "BatchNo");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReceiveCheck_PlantGrade",
                table: "MaterialReceiveCheck",
                column: "PlantGrade");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReceiveCheck_Specification",
                table: "MaterialReceiveCheck",
                column: "Specification");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MaterialReceiveCheck_BatchNo",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropIndex(
                name: "IX_MaterialReceiveCheck_PlantGrade",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropIndex(
                name: "IX_MaterialReceiveCheck_Specification",
                table: "MaterialReceiveCheck");
        }
    }
}
