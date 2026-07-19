using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPicklingOutRecordBatchNoProcessNameTagNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BatchNo",
                table: "PicklingOutRecord",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessName",
                table: "PicklingOutRecord",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagNo",
                table: "PicklingOutRecord",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatchNo",
                table: "PicklingOutRecord");

            migrationBuilder.DropColumn(
                name: "ProcessName",
                table: "PicklingOutRecord");

            migrationBuilder.DropColumn(
                name: "TagNo",
                table: "PicklingOutRecord");
        }
    }
}
