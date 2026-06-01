using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchNoFieldsForDataImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrderNo",
                table: "SubcontractReturnItem",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ItemSequence",
                table: "ProductRequirement",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderNo",
                table: "ProductRequirement",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchNo",
                table: "ProcessGroup",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderNumber",
                table: "OrderItem",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrderNo",
                table: "SubcontractReturnItem");

            migrationBuilder.DropColumn(
                name: "ItemSequence",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "OrderNo",
                table: "ProductRequirement");

            migrationBuilder.DropColumn(
                name: "BatchNo",
                table: "ProcessGroup");

            migrationBuilder.DropColumn(
                name: "OrderNumber",
                table: "OrderItem");
        }
    }
}
