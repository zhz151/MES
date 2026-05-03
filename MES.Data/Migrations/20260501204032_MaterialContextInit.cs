using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class MaterialContextInit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Material",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialCategory = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Material", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrder",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    OrderDate = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Open"),
                    ManualStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MaterialCategory = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UnitWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "date", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LastArrivalDate = table.Column<DateTime>(type: "date", nullable: true),
                    ReceivedQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ReceivedWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    SourceWorkOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrder", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubcontractOrder",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    OrderDate = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Sent"),
                    ManualStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OutMaterialCategory = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OutPlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OutSpecification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OutQuantity = table.Column<int>(type: "int", nullable: false),
                    OutWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ReturnDeadline = table.Column<DateTime>(type: "date", nullable: true),
                    InQuantity = table.Column<int>(type: "int", nullable: true),
                    InWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    SourceWorkOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcontractOrder", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierProfile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactPerson = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierProfile", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubcontractReturnItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubcontractOrderId = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ProcessType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MaterialCategory = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProcessSpecification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProcessStatusRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProcessUnitPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ProcessTotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SourceWorkOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcontractReturnItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubcontractReturnItem_SubcontractOrder_SubcontractOrderId",
                        column: x => x.SubcontractOrderId,
                        principalTable: "SubcontractOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Material_Category",
                table: "Material",
                column: "MaterialCategory");

            migrationBuilder.CreateIndex(
                name: "IX_Material_IsActive",
                table: "Material",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "UK_Material_Combo",
                table: "Material",
                columns: new[] { "MaterialCategory", "PlantGrade", "Specification" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_RequiredDate",
                table: "PurchaseOrder",
                column: "RequiredDate");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_SourceWO",
                table: "PurchaseOrder",
                column: "SourceWorkOrderNo");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_Status",
                table: "PurchaseOrder",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_SupplierId",
                table: "PurchaseOrder",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "UK_PurchaseOrder_OrderNo",
                table: "PurchaseOrder",
                column: "OrderNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractOrder_Status",
                table: "SubcontractOrder",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractOrder_SupplierId",
                table: "SubcontractOrder",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "UK_SubcontractOrder_OrderNo",
                table: "SubcontractOrder",
                column: "OrderNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnItem_OrderId",
                table: "SubcontractReturnItem",
                column: "SubcontractOrderId");

            migrationBuilder.CreateIndex(
                name: "UK_ReturnItem_Seq",
                table: "SubcontractReturnItem",
                columns: new[] { "SubcontractOrderId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Material");

            migrationBuilder.DropTable(
                name: "PurchaseOrder");

            migrationBuilder.DropTable(
                name: "SubcontractReturnItem");

            migrationBuilder.DropTable(
                name: "SupplierProfile");

            migrationBuilder.DropTable(
                name: "SubcontractOrder");
        }
    }
}
