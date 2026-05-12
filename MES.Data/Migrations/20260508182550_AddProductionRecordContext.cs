using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionRecordContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "SignDate",
                table: "WorkOrder",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DeliveryDate",
                table: "WorkOrder",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SignDate",
                table: "SalesOrder",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SignDate",
                table: "ProductionBatch",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<DateTime>(
                name: "InboundDate",
                table: "ProductionBatch",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DeliveryDate",
                table: "ProductionBatch",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CurrentExecDate",
                table: "ProductionBatch",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "OutboundDate",
                table: "OutboundRecord",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DeliveryDate",
                table: "OrderItem",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DeletedTime",
                table: "InventoryBatchDeleteLog",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<DateTime>(
                name: "InboundDate",
                table: "InventoryBatch",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.CreateTable(
                name: "MaterialReceiveCheck",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionBatchId = table.Column<int>(type: "int", nullable: false),
                    ReceiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "int", nullable: true),
                    ReceivedWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    Shift = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Checker = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialReceiveCheck", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialReceiveCheck_ProductionBatch_ProductionBatchId",
                        column: x => x.ProductionBatchId,
                        principalTable: "ProductionBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductionRecord",
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
                    ExecDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EquipmentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Operator = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Shift = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    DefectQuantity = table.Column<int>(type: "int", nullable: true),
                    DefectWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    IsFinished = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CuttingRate = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    FinishedCutLength = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PostCutQuantity = table.Column<int>(type: "int", nullable: true),
                    TagNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionRecord_ProcessGroup_ProcessGroupId",
                        column: x => x.ProcessGroupId,
                        principalTable: "ProcessGroup",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductionRecord_ProductionBatch_ProductionBatchId",
                        column: x => x.ProductionBatchId,
                        principalTable: "ProductionBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SectionOutsource",
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
                    OutsourceVendor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SendOutDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SendQuantity = table.Column<int>(type: "int", nullable: true),
                    SendWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "待回收"),
                    TagNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionOutsource", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SectionOutsource_ProcessGroup_ProcessGroupId",
                        column: x => x.ProcessGroupId,
                        principalTable: "ProcessGroup",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SectionOutsource_ProductionBatch_ProductionBatchId",
                        column: x => x.ProductionBatchId,
                        principalTable: "ProductionBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutsourceRecovery",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionOutsourceId = table.Column<int>(type: "int", nullable: false),
                    RecoveryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecoveryQuantity = table.Column<int>(type: "int", nullable: true),
                    RecoveryWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    IsQualified = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutsourceRecovery", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutsourceRecovery_SectionOutsource_SectionOutsourceId",
                        column: x => x.SectionOutsourceId,
                        principalTable: "SectionOutsource",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UK_MaterialReceiveCheck_BatchId",
                table: "MaterialReceiveCheck",
                column: "ProductionBatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutsourceRecovery_OutsourceId",
                table: "OutsourceRecovery",
                column: "SectionOutsourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecord_BatchId",
                table: "ProductionRecord",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecord_ProcessGroupId",
                table: "ProductionRecord",
                column: "ProcessGroupId");

            migrationBuilder.CreateIndex(
                name: "UK_ProductionRecord_Section",
                table: "ProductionRecord",
                columns: new[] { "ProductionBatchId", "ProcessGroupId", "SectionName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SectionOutsource_BatchId",
                table: "SectionOutsource",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SectionOutsource_ProcessGroupId",
                table: "SectionOutsource",
                column: "ProcessGroupId");

            migrationBuilder.CreateIndex(
                name: "UK_SectionOutsource_Section",
                table: "SectionOutsource",
                columns: new[] { "ProductionBatchId", "ProcessGroupId", "SectionName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterialReceiveCheck");

            migrationBuilder.DropTable(
                name: "OutsourceRecovery");

            migrationBuilder.DropTable(
                name: "ProductionRecord");

            migrationBuilder.DropTable(
                name: "SectionOutsource");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SignDate",
                table: "WorkOrder",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DeliveryDate",
                table: "WorkOrder",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SignDate",
                table: "SalesOrder",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SignDate",
                table: "ProductionBatch",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "InboundDate",
                table: "ProductionBatch",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DeliveryDate",
                table: "ProductionBatch",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CurrentExecDate",
                table: "ProductionBatch",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "OutboundDate",
                table: "OutboundRecord",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DeliveryDate",
                table: "OrderItem",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DeletedTime",
                table: "InventoryBatchDeleteLog",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "InboundDate",
                table: "InventoryBatch",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");
        }
    }
}
