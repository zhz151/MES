using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSalesOrderNoTagNoFromNcr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SalesOrderNo",
                table: "Ncr");

            migrationBuilder.DropColumn(
                name: "TagNo",
                table: "Ncr");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SalesOrderNo",
                table: "Ncr",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagNo",
                table: "Ncr",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
