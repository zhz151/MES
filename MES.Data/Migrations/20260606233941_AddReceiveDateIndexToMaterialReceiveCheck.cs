using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiveDateIndexToMaterialReceiveCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MaterialReceiveCheck_ReceiveDate",
                table: "MaterialReceiveCheck",
                column: "ReceiveDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MaterialReceiveCheck_ReceiveDate",
                table: "MaterialReceiveCheck");
        }
    }
}
