using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderExecutionSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkOrderExecutionSummary",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkOrderId = table.Column<int>(type: "int", nullable: false),
                    WorkOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Salesman = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SignDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DelayPenalty = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SettlementMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SalesOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionMainNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionSubNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MaterialName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DeliveryState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LengthStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MinLength = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxLength = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalItemCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalMeters = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    LatestPlanDate = table.Column<DateTime>(type: "date", nullable: true),
                    MaterialPlanRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    MaterialPlanStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MainNoMaterialPlanRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    MainNoMaterialPlanStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    InputStartDate = table.Column<DateTime>(type: "date", nullable: true),
                    InputEndDate = table.Column<DateTime>(type: "date", nullable: true),
                    TotalBatchCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    InputQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    InputWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    TheoreticalOutputQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    TheoreticalOutputWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    InputOutputRatio = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    InputStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MainNoInputOutputRatio = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    MainNoInputStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ValidBatchCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ValidInputQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ValidInputWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    ValidOutputQty = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    ValidOutputWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    ValidInputOutputRatio = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    ValidInputStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastRefreshTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderExecutionSummary", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WES_InputStatus",
                table: "WorkOrderExecutionSummary",
                column: "InputStatus");

            migrationBuilder.CreateIndex(
                name: "IX_WES_ProductionMainNo",
                table: "WorkOrderExecutionSummary",
                column: "ProductionMainNo");

            migrationBuilder.CreateIndex(
                name: "IX_WES_SalesOrderNo",
                table: "WorkOrderExecutionSummary",
                column: "SalesOrderNo");

            migrationBuilder.CreateIndex(
                name: "IX_WES_WorkOrderNo",
                table: "WorkOrderExecutionSummary",
                column: "WorkOrderNo");

            migrationBuilder.CreateIndex(
                name: "UK_WES_WorkOrderId",
                table: "WorkOrderExecutionSummary",
                column: "WorkOrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkOrderExecutionSummary");
        }
    }
}
