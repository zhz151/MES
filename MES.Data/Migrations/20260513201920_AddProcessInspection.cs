using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessInspection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessInspection",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionBatchId = table.Column<int>(type: "int", nullable: false),
                    ProcessGroupId = table.Column<int>(type: "int", nullable: false),
                    ProcessName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ManufacturingSpec = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SectionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    InspectionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EquipmentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Inspector = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Shift = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    InspectionItem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    QualifiedQuantity = table.Column<int>(type: "int", nullable: true),
                    QualifiedWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    DefectReworkQuantity = table.Column<int>(type: "int", nullable: true),
                    DefectWarehouseQuantity = table.Column<int>(type: "int", nullable: true),
                    DefectScrapQuantity = table.Column<int>(type: "int", nullable: true),
                    DefectDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SourceUnit = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TagNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessInspection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessInspection_ProcessGroup_ProcessGroupId",
                        column: x => x.ProcessGroupId,
                        principalTable: "ProcessGroup",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProcessInspection_ProductionBatch_ProductionBatchId",
                        column: x => x.ProductionBatchId,
                        principalTable: "ProductionBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInspection_BatchId",
                table: "ProcessInspection",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInspection_ProcessGroupId",
                table: "ProcessInspection",
                column: "ProcessGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessInspection");
        }
    }
}
