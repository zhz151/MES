using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCombinationGroupAndDisplayOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "SectionFlowCategorySettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // 回填展示序号：A-N 依次 1-14（汇总表展示顺序）
            migrationBuilder.Sql(@"
                UPDATE SectionFlowCategorySettings SET DisplayOrder = CASE CategoryCode
                    WHEN 'A' THEN 1 WHEN 'B' THEN 2 WHEN 'C' THEN 3 WHEN 'D' THEN 4 WHEN 'E' THEN 5
                    WHEN 'F' THEN 6 WHEN 'G' THEN 7 WHEN 'H' THEN 8 WHEN 'I' THEN 9 WHEN 'J' THEN 10
                    WHEN 'K' THEN 11 WHEN 'L' THEN 12 WHEN 'M' THEN 13 WHEN 'N' THEN 14 ELSE 99 END;
            ");

            migrationBuilder.CreateTable(
                name: "CombinationGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessGroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SectionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FlowCategoryId = table.Column<int>(type: "int", nullable: true),
                    Coefficient = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
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
                name: "UK_CG_ProcessGroupName_SectionName",
                table: "CombinationGroups",
                columns: new[] { "ProcessGroupName", "SectionName" },
                unique: true);

            // 展平填充组合归类表：从旧明细展平普通类别（排除 D/E/N 检验类、排除"全部"通配），
            // 取每个(工序组,工段)唯一组合（ROW_NUMBER 按类别/明细序号去重），审计字段手动填充
            migrationBuilder.Sql(@"
                INSERT INTO CombinationGroups (ProcessGroupName, SectionName, FlowCategoryId, Coefficient, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
                SELECT ProcessGroupName, SectionName, FlowCategoryId, Coefficient, SYSDATETIMEOFFSET(), N'', SYSDATETIMEOFFSET(), N''
                FROM (
                    SELECT i.ProcessGroupName, i.SectionName, s.Id AS FlowCategoryId, i.Coefficient,
                           ROW_NUMBER() OVER (PARTITION BY i.ProcessGroupName, i.SectionName ORDER BY s.Id, i.Id) AS rn
                    FROM SectionFlowCategoryItems i
                    INNER JOIN SectionFlowCategorySettings s ON i.SettingId = s.Id
                    WHERE s.CategoryCode NOT IN ('D','E','N')
                      AND i.ProcessGroupName <> N'全部'
                      AND i.SectionName <> N'全部'
                ) x
                WHERE rn = 1;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CombinationGroups");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "SectionFlowCategorySettings");
        }
    }
}
