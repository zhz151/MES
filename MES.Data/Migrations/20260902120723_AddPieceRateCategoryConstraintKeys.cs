using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 生产计件类别三约束集合（工序/产类/作业阶段）由主表 JSON 数组列 → 关系成员表
    /// PieceRateProductionCategoryKeys（2026-09-02 约束集合实体化）。
    /// 顺序必须：建成员表 → 用 OPENJSON 从旧 JSON 列回填（0 成员=该维全选）→ 删三旧列。
    /// 存量 JSON 经 SerializeNormalized 归一为「排序去重或 NULL」；NULL 列 ISNULL 为 '[]' → 0 行 → 保持全选。
    /// </summary>
    public partial class AddPieceRateCategoryConstraintKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PieceRateProductionCategoryKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    ConstraintType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PieceRateProductionCategoryKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PieceRateProductionCategoryKeys_PieceRateProductionCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "PieceRateProductionCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UK_CategoryKey_Type_Key",
                table: "PieceRateProductionCategoryKeys",
                columns: new[] { "CategoryId", "ConstraintType", "Key" },
                unique: true);

            // ---- 回填（migration-constraint-keys）：旧三 JSON 列 → 成员行；NULL/空数组 → 0 行 = 全选 ----
            migrationBuilder.Sql("""
                INSERT INTO PieceRateProductionCategoryKeys(CategoryId, ConstraintType, [Key], CreatedBy, CreatedTime, UpdatedBy, UpdatedTime)
                SELECT c.Id, N'Process', jt.[value], N'migration-constraint-keys', GETUTCDATE(), N'migration-constraint-keys', GETUTCDATE()
                FROM PieceRateProductionCategories c
                CROSS APPLY OPENJSON(ISNULL(c.ProcessKeys, N'[]')) jt;
                """);

            migrationBuilder.Sql("""
                INSERT INTO PieceRateProductionCategoryKeys(CategoryId, ConstraintType, [Key], CreatedBy, CreatedTime, UpdatedBy, UpdatedTime)
                SELECT c.Id, N'ProductStatus', jt.[value], N'migration-constraint-keys', GETUTCDATE(), N'migration-constraint-keys', GETUTCDATE()
                FROM PieceRateProductionCategories c
                CROSS APPLY OPENJSON(ISNULL(c.ProductStatusKeys, N'[]')) jt;
                """);

            migrationBuilder.Sql("""
                INSERT INTO PieceRateProductionCategoryKeys(CategoryId, ConstraintType, [Key], CreatedBy, CreatedTime, UpdatedBy, UpdatedTime)
                SELECT c.Id, N'Stage', jt.[value], N'migration-constraint-keys', GETUTCDATE(), N'migration-constraint-keys', GETUTCDATE()
                FROM PieceRateProductionCategories c
                CROSS APPLY OPENJSON(ISNULL(c.StageKeys, N'[]')) jt;
                """);

            // ---- 删旧 JSON 列 ----
            migrationBuilder.DropColumn(
                name: "ProcessKeys",
                table: "PieceRateProductionCategories");

            migrationBuilder.DropColumn(
                name: "ProductStatusKeys",
                table: "PieceRateProductionCategories");

            migrationBuilder.DropColumn(
                name: "StageKeys",
                table: "PieceRateProductionCategories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 还原三 JSON 列
            migrationBuilder.AddColumn<string>(
                name: "ProcessKeys",
                table: "PieceRateProductionCategories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductStatusKeys",
                table: "PieceRateProductionCategories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StageKeys",
                table: "PieceRateProductionCategories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // 逆回填：成员行 → JSON；无行 = NULL（全选）；有行按 Key Ordinal 拼回 JSON 数组
            migrationBuilder.Sql("""
                UPDATE c SET ProcessKeys = k.js
                FROM PieceRateProductionCategories c
                OUTER APPLY (
                    SELECT CASE WHEN COUNT(*) = 0 THEN NULL
                        ELSE N'[' + STRING_AGG(CONCAT(N'"', [Key], N'"'), N',') WITHIN GROUP (ORDER BY [Key]) + N']' END AS js
                    FROM PieceRateProductionCategoryKeys k
                    WHERE k.CategoryId = c.Id AND k.ConstraintType = N'Process') k;
                """);

            migrationBuilder.Sql("""
                UPDATE c SET ProductStatusKeys = k.js
                FROM PieceRateProductionCategories c
                OUTER APPLY (
                    SELECT CASE WHEN COUNT(*) = 0 THEN NULL
                        ELSE N'[' + STRING_AGG(CONCAT(N'"', [Key], N'"'), N',') WITHIN GROUP (ORDER BY [Key]) + N']' END AS js
                    FROM PieceRateProductionCategoryKeys k
                    WHERE k.CategoryId = c.Id AND k.ConstraintType = N'ProductStatus') k;
                """);

            migrationBuilder.Sql("""
                UPDATE c SET StageKeys = k.js
                FROM PieceRateProductionCategories c
                OUTER APPLY (
                    SELECT CASE WHEN COUNT(*) = 0 THEN NULL
                        ELSE N'[' + STRING_AGG(CONCAT(N'"', [Key], N'"'), N',') WITHIN GROUP (ORDER BY [Key]) + N']' END AS js
                    FROM PieceRateProductionCategoryKeys k
                    WHERE k.CategoryId = c.Id AND k.ConstraintType = N'Stage') k;
                """);

            migrationBuilder.DropTable(
                name: "PieceRateProductionCategoryKeys");
        }
    }
}
