using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinalInspectionRemoveRedundantBatchFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MaterialReceiveCheck_PlantGrade",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropIndex(
                name: "IX_MaterialReceiveCheck_Specification",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "DeliveryState",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "FurnaceNo",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "LengthStatus",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "ManufacturingItem",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "PlantGrade",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "ProductionCutQuantity",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "ProductionType",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "ProductionWeight",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "SalesOrderNo",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "Salesman",
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

            migrationBuilder.DropColumn(
                name: "FixedLength",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "FurnaceNo",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "LengthStatus",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "ManufacturingItem",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "PlantGrade",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "ProductionType",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "SalesOrderNo",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "Salesman",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "SourceUnit",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "Specification",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "TagNo",
                table: "FinalInspection");

            migrationBuilder.DropColumn(
                name: "WorkOrderNo",
                table: "FinalInspection");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryState",
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

            migrationBuilder.AddColumn<string>(
                name: "LengthStatus",
                table: "MaterialReceiveCheck",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManufacturingItem",
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

            migrationBuilder.AddColumn<int>(
                name: "ProductionCutQuantity",
                table: "MaterialReceiveCheck",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProductionType",
                table: "MaterialReceiveCheck",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProductionWeight",
                table: "MaterialReceiveCheck",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesOrderNo",
                table: "MaterialReceiveCheck",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Salesman",
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

            migrationBuilder.AddColumn<string>(
                name: "FixedLength",
                table: "FinalInspection",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FurnaceNo",
                table: "FinalInspection",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LengthStatus",
                table: "FinalInspection",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManufacturingItem",
                table: "FinalInspection",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlantGrade",
                table: "FinalInspection",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionType",
                table: "FinalInspection",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesOrderNo",
                table: "FinalInspection",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Salesman",
                table: "FinalInspection",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUnit",
                table: "FinalInspection",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Specification",
                table: "FinalInspection",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagNo",
                table: "FinalInspection",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkOrderNo",
                table: "FinalInspection",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReceiveCheck_PlantGrade",
                table: "MaterialReceiveCheck",
                column: "PlantGrade");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReceiveCheck_Specification",
                table: "MaterialReceiveCheck",
                column: "Specification");
        }
    }
}
