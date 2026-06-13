using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFlowAnalysisLMCategoryItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // L(在制检): 从 (在制修检, 全部) 改为 (全部, 检验)
            // M(成品待检): 从 (在制修检, 全部) 改为 (全部, 检验)
            migrationBuilder.Sql(@"
                UPDATE i
                SET i.ProcessGroupName = N'全部', i.SectionName = N'检验'
                FROM SectionFlowCategoryItems i
                INNER JOIN SectionFlowCategorySettings s ON i.SettingId = s.Id
                WHERE s.CategoryCode IN ('L', 'M')
                  AND i.ProcessGroupName = N'在制修检'
                  AND i.SectionName = N'全部'
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回退: 从 (全部, 检验) 改回 (在制修检, 全部)
            migrationBuilder.Sql(@"
                UPDATE i
                SET i.ProcessGroupName = N'在制修检', i.SectionName = N'全部'
                FROM SectionFlowCategoryItems i
                INNER JOIN SectionFlowCategorySettings s ON i.SettingId = s.Id
                WHERE s.CategoryCode IN ('L', 'M')
                  AND i.ProcessGroupName = N'全部'
                  AND i.SectionName = N'检验'
            ");
        }
    }
}
