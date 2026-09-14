using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNonconformingFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NonconformingFeedback",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reporter = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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
                    IncomingQuantity = table.Column<int>(type: "int", nullable: true),
                    IncomingWeight = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    DefectQuantity = table.Column<int>(type: "int", nullable: true),
                    DefectWeight = table.Column<int>(type: "int", nullable: true),
                    ProblemDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsHandled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NonconformingFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NonconformingFeedback_ProcessGroup_ProcessGroupId",
                        column: x => x.ProcessGroupId,
                        principalTable: "ProcessGroup",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_NonconformingFeedback_ProductionBatch_ProductionBatchId",
                        column: x => x.ProductionBatchId,
                        principalTable: "ProductionBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NonconformingFeedbackAttachment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FeedbackId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_NonconformingFeedbackAttachment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NonconformingFeedbackAttachment_NonconformingFeedback_FeedbackId",
                        column: x => x.FeedbackId,
                        principalTable: "NonconformingFeedback",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NonconformingFeedback_BatchId",
                table: "NonconformingFeedback",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_NonconformingFeedback_BatchNo",
                table: "NonconformingFeedback",
                column: "BatchNo");

            migrationBuilder.CreateIndex(
                name: "IX_NonconformingFeedback_IsHandled",
                table: "NonconformingFeedback",
                column: "IsHandled");

            migrationBuilder.CreateIndex(
                name: "IX_NonconformingFeedback_ProcessGroupId",
                table: "NonconformingFeedback",
                column: "ProcessGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_NonconformingFeedback_ReportDate",
                table: "NonconformingFeedback",
                column: "ReportDate");

            migrationBuilder.CreateIndex(
                name: "IX_NCFAttachment_FeedbackId",
                table: "NonconformingFeedbackAttachment",
                column: "FeedbackId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NonconformingFeedbackAttachment");

            migrationBuilder.DropTable(
                name: "NonconformingFeedback");
        }
    }
}
