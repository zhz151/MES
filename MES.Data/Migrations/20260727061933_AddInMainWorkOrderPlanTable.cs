using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInMainWorkOrderPlanTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InMainWorkOrderPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkOrderId = table.Column<int>(type: "int", nullable: false),
                    PlanDate = table.Column<DateTime>(type: "date", nullable: false),
                    ProductionBatchId = table.Column<int>(type: "int", nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MainWorkOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AllocatedWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    AllocatedQuantity = table.Column<int>(type: "int", nullable: true),
                    StandardCycle = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RequiredDate = table.Column<DateTime>(type: "date", nullable: true),
                    PlanStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Planned"),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InMainWorkOrderPlan", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InMainWorkOrderPlan_PlanStatus",
                table: "InMainWorkOrderPlan",
                column: "PlanStatus");

            migrationBuilder.CreateIndex(
                name: "IX_InMainWorkOrderPlan_ProductionBatchId",
                table: "InMainWorkOrderPlan",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_InMainWorkOrderPlan_WorkOrderId",
                table: "InMainWorkOrderPlan",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InMainWorkOrderPlan");
        }
    }
}
