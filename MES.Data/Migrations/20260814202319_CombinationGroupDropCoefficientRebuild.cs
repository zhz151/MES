using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class CombinationGroupDropCoefficientRebuild : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. 删除工量系数列（组合表全面重建为 4 列：工序组/工段/产类/归属流转类别）
            migrationBuilder.DropColumn(
                name: "Coefficient",
                table: "CombinationGroups");

            // 2. 清空旧稀疏组合数据（57 行通配配置作废，全面重建）
            migrationBuilder.Sql("DELETE FROM CombinationGroups;");

            // 3. 重建 390 行：启用工序组 × 启用工段(排除仓库) × 3 产类，归属流转类别全空
            //    由用户通过数据工具导出 Excel 填写第 4 列（中文类别名）上传建立 FK
            migrationBuilder.Sql(@"
INSERT INTO CombinationGroups (ProcessGroupName, SectionName, ProductStatus, FlowCategoryId, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
SELECT p.ProcessKey, s.SectionKey, ps.ProductStatus, NULL,
       SYSDATETIMEOFFSET(), N'系统迁移', SYSDATETIMEOFFSET(), N'系统迁移'
FROM (SELECT DISTINCT ProcessKey FROM ProcessDefinitions WHERE IsEnabled = 1) p
CROSS JOIN (SELECT DISTINCT SectionKey FROM StandardWorkDays WHERE IsEnabled = 1 AND SectionKey <> N'Warehouse') s
CROSS JOIN (VALUES (N'RoughTube'), (N'InProgress'), (N'Finished')) ps(ProductStatus);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Coefficient",
                table: "CombinationGroups",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            // 反向：清空重建数据（旧 57 行通配配置不再精确回滚，可接受）
            migrationBuilder.Sql("DELETE FROM CombinationGroups;");
        }
    }
}
