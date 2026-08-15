using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSectionParagraphConfigAndParagraphName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ParagraphName",
                table: "CombinationGroups",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SectionParagraphConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParagraphName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    DailyFlowTarget = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
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
                    table.PrimaryKey("PK_SectionParagraphConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_SPC_ParagraphName",
                table: "SectionParagraphConfigs",
                column: "ParagraphName",
                unique: true);

            // 回填组合归类表的「归属段落」：按已归属的流转类别（FlowCategoryId → SectionFlowCategorySettings.CategoryName）映射到生产段落。
            // 未归属行（FlowCategoryId 为空或类别未在映射清单）保持 NULL，由用户后续经数据工具/配置页填写。
            migrationBuilder.Sql(@"
UPDATE cg
SET cg.ParagraphName = CASE fc.CategoryName
    WHEN N'荒管抛光' THEN N'荒管抛光'
    WHEN N'荒管检验' THEN N'荒管修检'
    WHEN N'荒管内修' THEN N'荒管修检'
    WHEN N'荒管外点' THEN N'荒管修检'
    WHEN N'在制检验' THEN N'在制修检'
    WHEN N'荒管固溶' THEN N'固溶'
    WHEN N'固溶5060' THEN N'固溶'
    WHEN N'固溶2030' THEN N'固溶'
    WHEN N'荒管矫直' THEN N'矫直'
    WHEN N'矫直5060' THEN N'矫直'
    WHEN N'矫直2030' THEN N'矫直'
    WHEN N'荒管平头' THEN N'切割'
    WHEN N'油断5060' THEN N'切割'
    WHEN N'断切5060' THEN N'切割'
    WHEN N'油断2030' THEN N'切割'
    WHEN N'断切2030' THEN N'切割'
    WHEN N'去油5060' THEN N'去油'
    WHEN N'去油2030' THEN N'去油'
    WHEN N'荒管酸洗' THEN N'酸洗'
    WHEN N'酸洗5060' THEN N'酸洗'
    WHEN N'酸洗2030' THEN N'酸洗'
    WHEN N'冷轧5060' THEN N'冷轧5060'
    WHEN N'冷拔打头' THEN N'冷轧2030'
    WHEN N'冷轧2030' THEN N'冷轧2030'
    WHEN N'成品待检' THEN N'成品待检'
    ELSE cg.ParagraphName
END
FROM CombinationGroups cg
LEFT JOIN SectionFlowCategorySettings fc ON cg.FlowCategoryId = fc.Id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SectionParagraphConfigs");

            migrationBuilder.DropColumn(
                name: "ParagraphName",
                table: "CombinationGroups");
        }
    }
}
