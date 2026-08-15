using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class SectionFlowCategoryThreeDimensionProductStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. 删旧二维唯一索引（三维索引包含其语义）
            migrationBuilder.DropIndex(
                name: "UK_CG_ProcessGroupName_SectionName",
                table: "CombinationGroups");

            // 2. 组合归类表加产类列：存量行默认 AllStatus（不限产类）
            migrationBuilder.AddColumn<string>(
                name: "ProductStatus",
                table: "CombinationGroups",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "All");

            // 3. 回填检验类组合行：类别类型为检验类的，在组合表生成 (全部, 检验, 对应产类) 通配行
            //    （必须在删 CategoryKind 列之前执行，读取主表类别类型）
            migrationBuilder.Sql(@"
INSERT INTO CombinationGroups (ProcessGroupName, SectionName, ProductStatus, Coefficient, FlowCategoryId, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
SELECT N'全部', N'Inspection',
       CASE s.CategoryKind
           WHEN N'RoughTubeInspection' THEN N'RoughTube'
           WHEN N'InProcessInspection' THEN N'InProgress'
           WHEN N'FinalInspection' THEN N'Finished'
       END,
       1.0, s.Id, SYSDATETIMEOFFSET(), N'系统迁移', SYSDATETIMEOFFSET(), N'系统迁移'
FROM SectionFlowCategorySettings s
WHERE s.CategoryKind IN (N'RoughTubeInspection', N'InProcessInspection', N'FinalInspection')
  AND NOT EXISTS (
      SELECT 1 FROM CombinationGroups c
      WHERE c.ProcessGroupName = N'全部'
        AND c.SectionName = N'Inspection'
        AND c.ProductStatus = CASE s.CategoryKind
            WHEN N'RoughTubeInspection' THEN N'RoughTube'
            WHEN N'InProcessInspection' THEN N'InProgress'
            WHEN N'FinalInspection' THEN N'Finished'
        END
  );");

            // 4. 删类别类型列（检验类配置已由组合表三维承载）
            migrationBuilder.DropColumn(
                name: "CategoryKind",
                table: "SectionFlowCategorySettings");

            // 5. 删明细表（明细逻辑已并入组合表三维）
            migrationBuilder.DropTable(
                name: "SectionFlowCategoryItems");

            // 6. 建三维唯一索引
            migrationBuilder.CreateIndex(
                name: "UK_CG_ProcessGroupName_SectionName_ProductStatus",
                table: "CombinationGroups",
                columns: new[] { "ProcessGroupName", "SectionName", "ProductStatus" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_CG_ProcessGroupName_SectionName_ProductStatus",
                table: "CombinationGroups");

            migrationBuilder.DropColumn(
                name: "ProductStatus",
                table: "CombinationGroups");

            migrationBuilder.AddColumn<string>(
                name: "CategoryKind",
                table: "SectionFlowCategorySettings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            // 反向：删除迁移插入的检验类通配行（回到 CategoryKind 承载模型）
            migrationBuilder.Sql(@"
DELETE c FROM CombinationGroups c
INNER JOIN SectionFlowCategorySettings s ON s.Id = c.FlowCategoryId
WHERE c.ProcessGroupName = N'全部' AND c.SectionName = N'Inspection'
  AND c.ProductStatus IN (N'RoughTube', N'InProgress', N'Finished');");

            migrationBuilder.CreateTable(
                name: "SectionFlowCategoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettingId = table.Column<int>(type: "int", nullable: false),
                    Coefficient = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    ProcessGroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SectionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
                name: "UK_CG_ProcessGroupName_SectionName",
                table: "CombinationGroups",
                columns: new[] { "ProcessGroupName", "SectionName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_SFCI_SettingId_ProcessGroupName_SectionName",
                table: "SectionFlowCategoryItems",
                columns: new[] { "SettingId", "ProcessGroupName", "SectionName" },
                unique: true);
        }
    }
}
