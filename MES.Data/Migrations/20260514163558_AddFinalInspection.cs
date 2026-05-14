using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalInspection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinalInspection",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InspectionItem = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InspectionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionBatchId = table.Column<int>(type: "int", nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TagNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    WorkOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SalesOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SourceUnit = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FurnaceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FixedLength = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EquipmentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Shift = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Operator = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    QualifiedQuantity = table.Column<int>(type: "int", nullable: true),
                    QualifiedWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    DefectReworkQuantity = table.Column<int>(type: "int", nullable: true),
                    DefectWarehouseQuantity = table.Column<int>(type: "int", nullable: true),
                    DefectScrapQuantity = table.Column<int>(type: "int", nullable: true),
                    DefectDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OuterDiameterRange = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WallThicknessRange = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LengthAllowanceRange = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Pressure = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    HoldTime = table.Column<int>(type: "int", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinalInspection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinalInspection_ProductionBatch_ProductionBatchId",
                        column: x => x.ProductionBatchId,
                        principalTable: "ProductionBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinalInspection_BatchNo",
                table: "FinalInspection",
                column: "BatchNo");

            migrationBuilder.CreateIndex(
                name: "IX_FinalInspection_InspectionDate",
                table: "FinalInspection",
                column: "InspectionDate");

            migrationBuilder.CreateIndex(
                name: "IX_FinalInspection_InspectionItem",
                table: "FinalInspection",
                column: "InspectionItem");

            migrationBuilder.CreateIndex(
                name: "IX_FinalInspection_ProductionBatchId",
                table: "FinalInspection",
                column: "ProductionBatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinalInspection");
        }
    }
}
