using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboundRecord_RelatedNo",
                table: "OutboundRecord");

            migrationBuilder.DropColumn(
                name: "RelatedNo",
                table: "OutboundRecord");

            migrationBuilder.DropColumn(
                name: "IsFrozen",
                table: "InventoryBatch");

            migrationBuilder.DropColumn(
                name: "IsMixedPackage",
                table: "InventoryBatch");

            migrationBuilder.DropColumn(
                name: "PackageNo",
                table: "InventoryBatch");

            migrationBuilder.DropColumn(
                name: "RelatedNo",
                table: "InventoryBatch");

            migrationBuilder.CreateTable(
                name: "InventoryPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkOrderId = table.Column<int>(type: "int", nullable: false),
                    PlanDate = table.Column<DateTime>(type: "date", nullable: false),
                    InventoryBatchId = table.Column<int>(type: "int", nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InputMultiple = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    UsageMode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "All"),
                    UsedQuantity = table.Column<int>(type: "int", nullable: true),
                    UsedWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "date", nullable: true),
                    PlanStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Planned"),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryPlan", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPlan_InventoryBatchId",
                table: "InventoryPlan",
                column: "InventoryBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPlan_PlanStatus",
                table: "InventoryPlan",
                column: "PlanStatus");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPlan_WorkOrderId",
                table: "InventoryPlan",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryPlan");

            migrationBuilder.AddColumn<string>(
                name: "RelatedNo",
                table: "OutboundRecord",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFrozen",
                table: "InventoryBatch",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsMixedPackage",
                table: "InventoryBatch",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PackageNo",
                table: "InventoryBatch",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedNo",
                table: "InventoryBatch",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboundRecord_RelatedNo",
                table: "OutboundRecord",
                column: "RelatedNo");
        }
    }
}
