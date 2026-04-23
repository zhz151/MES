using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkOrder",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SalesOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionMainNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionSubNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OrderItemIds = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SignDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    Salesman = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EndCustomer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeliveryDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    DelayPenalty = table.Column<bool>(type: "bit", nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SettlementMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StandardCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DeliveryState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OuterDiameterMinus = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    OuterDiameterPlus = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    WallThicknessMinus = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    WallThicknessPlus = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    LengthStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MinLength = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxLength = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalMeters = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    TotalItemCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ItemDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicalRequirements = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Normal"),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrder", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrder_DeliveryDate",
                table: "WorkOrder",
                column: "DeliveryDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrder_MaterialName",
                table: "WorkOrder",
                column: "MaterialName");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrder_SalesOrderNo",
                table: "WorkOrder",
                column: "SalesOrderNo");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrder_Specification",
                table: "WorkOrder",
                column: "Specification");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrder_Status",
                table: "WorkOrder",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UK_WorkOrder_MainSub",
                table: "WorkOrder",
                columns: new[] { "SalesOrderNo", "ProductionMainNo", "ProductionSubNo" },
                unique: true,
                filter: "[ProductionSubNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_WorkOrder_WorkOrderNo",
                table: "WorkOrder",
                column: "WorkOrderNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkOrder");
        }
    }
}
