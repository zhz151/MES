using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInspectionPatrol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InspectionPatrol",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatrolDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Inspector = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DataSource = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ProductionBatchId = table.Column<int>(type: "int", nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WorkOrderNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProcessGroupId = table.Column<int>(type: "int", nullable: false),
                    ProcessName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ManufacturingSpec = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SectionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    ProductStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProductionUnit = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EquipmentCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProductionOperator = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NeedRectification = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RectificationDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VerificationResult = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionPatrol", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionPatrol_ProcessGroup_ProcessGroupId",
                        column: x => x.ProcessGroupId,
                        principalTable: "ProcessGroup",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InspectionPatrol_ProductionBatch_ProductionBatchId",
                        column: x => x.ProductionBatchId,
                        principalTable: "ProductionBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InspectionPatrolAttachment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatrolId = table.Column<int>(type: "int", nullable: false),
                    PhotoType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoredName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionPatrolAttachment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionPatrolAttachment_InspectionPatrol_PatrolId",
                        column: x => x.PatrolId,
                        principalTable: "InspectionPatrol",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InspectionPatrolItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatrolId = table.Column<int>(type: "int", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionPatrolItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionPatrolItem_InspectionPatrol_PatrolId",
                        column: x => x.PatrolId,
                        principalTable: "InspectionPatrol",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPatrol_BatchId",
                table: "InspectionPatrol",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPatrol_BatchNo",
                table: "InspectionPatrol",
                column: "BatchNo");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPatrol_IsClosed",
                table: "InspectionPatrol",
                column: "IsClosed");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPatrol_NeedRectification",
                table: "InspectionPatrol",
                column: "NeedRectification");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPatrol_PatrolDate",
                table: "InspectionPatrol",
                column: "PatrolDate");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPatrol_ProcessGroupId",
                table: "InspectionPatrol",
                column: "ProcessGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPatrolAttachment_PatrolId",
                table: "InspectionPatrolAttachment",
                column: "PatrolId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPatrolItem_PatrolId",
                table: "InspectionPatrolItem",
                column: "PatrolId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InspectionPatrolAttachment");

            migrationBuilder.DropTable(
                name: "InspectionPatrolItem");

            migrationBuilder.DropTable(
                name: "InspectionPatrol");
        }
    }
}
