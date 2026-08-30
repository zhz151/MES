using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropCombinationGroupAndFlowCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CombinationGroups");

            migrationBuilder.DropTable(
                name: "SectionFlowCategorySettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SectionFlowCategorySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DailyProductionTarget = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    LowerLimitDays = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpperLimitDays = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionFlowCategorySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CombinationGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FlowCategoryId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ParagraphName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProcessGroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProductStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SectionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CombinationGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CombinationGroups_SectionFlowCategorySettings_FlowCategoryId",
                        column: x => x.FlowCategoryId,
                        principalTable: "SectionFlowCategorySettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CombinationGroups_FlowCategoryId",
                table: "CombinationGroups",
                column: "FlowCategoryId");

            migrationBuilder.CreateIndex(
                name: "UK_CG_ProcessGroupName_SectionName_ProductStatus",
                table: "CombinationGroups",
                columns: new[] { "ProcessGroupName", "SectionName", "ProductStatus" },
                unique: true);
        }
    }
}
