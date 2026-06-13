using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPicklingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PicklingInRecord",
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
                    InDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InputQuantity = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false, defaultValue: "Soaking"),
                    TagNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PlantGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DataSource = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true, defaultValue: "MANUAL"),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PicklingInRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PicklingInRecord_ProcessGroup_ProcessGroupId",
                        column: x => x.ProcessGroupId,
                        principalTable: "ProcessGroup",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PicklingInRecord_ProductionBatch_ProductionBatchId",
                        column: x => x.ProductionBatchId,
                        principalTable: "ProductionBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PicklingOutRecord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PicklingInRecordId = table.Column<int>(type: "int", nullable: false),
                    CompleteDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DataSource = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true, defaultValue: "MANUAL"),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PicklingOutRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PicklingOutRecord_PicklingInRecord_PicklingInRecordId",
                        column: x => x.PicklingInRecordId,
                        principalTable: "PicklingInRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PicklingInRecord_BatchId",
                table: "PicklingInRecord",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PicklingInRecord_ProcessGroupId",
                table: "PicklingInRecord",
                column: "ProcessGroupId");

            migrationBuilder.CreateIndex(
                name: "UK_PicklingInRecord_Section",
                table: "PicklingInRecord",
                columns: new[] { "ProductionBatchId", "ProcessGroupId", "SectionName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PicklingOutRecord_InRecordId",
                table: "PicklingOutRecord",
                column: "PicklingInRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PicklingOutRecord");

            migrationBuilder.DropTable(
                name: "PicklingInRecord");
        }
    }
}
