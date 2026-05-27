using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMaterialReceiveCheckRemoveQtyWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceivedQuantity",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "ReceivedWeight",
                table: "MaterialReceiveCheck");

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "ProductionBatch",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "DataSource",
                table: "MaterialReceiveCheck",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchNo",
                table: "MaterialReceiveCheck",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FurnaceNo",
                table: "MaterialReceiveCheck",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsForceCompleted",
                table: "MaterialReceiveCheck",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MaterialName",
                table: "MaterialReceiveCheck",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlantGrade",
                table: "MaterialReceiveCheck",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesOrderNo",
                table: "MaterialReceiveCheck",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUnit",
                table: "MaterialReceiveCheck",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Specification",
                table: "MaterialReceiveCheck",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagNo",
                table: "MaterialReceiveCheck",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkOrderNo",
                table: "MaterialReceiveCheck",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "ProductionBatch");

            migrationBuilder.DropColumn(
                name: "BatchNo",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "FurnaceNo",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "IsForceCompleted",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "MaterialName",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "PlantGrade",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "SalesOrderNo",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "SourceUnit",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "Specification",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "TagNo",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "WorkOrderNo",
                table: "MaterialReceiveCheck");

            migrationBuilder.AlterColumn<string>(
                name: "DataSource",
                table: "MaterialReceiveCheck",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReceivedQuantity",
                table: "MaterialReceiveCheck",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedWeight",
                table: "MaterialReceiveCheck",
                type: "decimal(18,3)",
                nullable: true);
        }
    }
}
