using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialPlanSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MaterialPlanRate",
                table: "WorkOrder",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MaterialPlanStatus",
                table: "WorkOrder",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PurchaseFinishedPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkOrderId = table.Column<int>(type: "int", nullable: false),
                    PlanDate = table.Column<DateTime>(type: "date", nullable: false),
                    ProductType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequiredPiece = table.Column<int>(type: "int", nullable: true),
                    RequiredWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "date", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseFinishedPlan", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseSemiPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkOrderId = table.Column<int>(type: "int", nullable: false),
                    PlanDate = table.Column<DateTime>(type: "date", nullable: false),
                    AdjustedWallThickness = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    YieldRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    InputMultiple = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    QualifiedRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Density = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    UnitWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    RawUnitWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    RequiredPieces = table.Column<int>(type: "int", nullable: true),
                    RequiredWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RawMaterialType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RawMaterialSpec = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "date", nullable: true),
                    ProcessPlan = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseSemiPlan", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseFinishedPlan_WorkOrderId",
                table: "PurchaseFinishedPlan",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseSemiPlan_WorkOrderId",
                table: "PurchaseSemiPlan",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseFinishedPlan");

            migrationBuilder.DropTable(
                name: "PurchaseSemiPlan");

            migrationBuilder.DropColumn(
                name: "MaterialPlanRate",
                table: "WorkOrder");

            migrationBuilder.DropColumn(
                name: "MaterialPlanStatus",
                table: "WorkOrder");
        }
    }
}
