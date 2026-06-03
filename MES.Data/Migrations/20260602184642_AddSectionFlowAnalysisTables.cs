using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSectionFlowAnalysisTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SectionFlowCategorySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CategoryName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DailyProductionTarget = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LowerLimitDays = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UpperLimitDays = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionFlowCategorySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SectionFlowCategoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettingId = table.Column<int>(type: "int", nullable: false),
                    ProcessGroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SectionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Coefficient = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionFlowCategoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SectionFlowCategoryItems_SectionFlowCategorySettings_SettingId",
                        column: x => x.SettingId,
                        principalTable: "SectionFlowCategorySettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UK_SFCI_SettingId_ProcessGroupName_SectionName",
                table: "SectionFlowCategoryItems",
                columns: new[] { "SettingId", "ProcessGroupName", "SectionName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_SFCS_CategoryCode",
                table: "SectionFlowCategorySettings",
                column: "CategoryCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SectionFlowCategoryItems");

            migrationBuilder.DropTable(
                name: "SectionFlowCategorySettings");
        }
    }
}
