using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLengthStatusAndProductionWeightToMaterialReceiveCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LengthStatus",
                table: "MaterialReceiveCheck",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductionCutQuantity",
                table: "MaterialReceiveCheck",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ProductionWeight",
                table: "MaterialReceiveCheck",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LengthStatus",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "ProductionCutQuantity",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "ProductionWeight",
                table: "MaterialReceiveCheck");
        }
    }
}
