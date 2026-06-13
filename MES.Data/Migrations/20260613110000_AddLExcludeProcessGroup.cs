using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLExcludeProcessGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // L(在制检) 新增排除项：系数 -1 抵消荒管处理在 (全部, 检验) 通配中的贡献
            // 找到 L 的 SettingId 后插入排除记录
            migrationBuilder.Sql(@"
                DECLARE @settingId INT = (SELECT Id FROM SectionFlowCategorySettings WHERE CategoryCode = 'L');
                INSERT INTO SectionFlowCategoryItems
                    (SettingId, ProcessGroupName, SectionName, Coefficient, DisplayOrder, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
                VALUES
                    (@settingId, N'荒管处理', N'检验', -1.0, 2, SYSDATETIMEOFFSET(), N'migration', SYSDATETIMEOFFSET(), N'migration')
                WHERE NOT EXISTS (
                    SELECT 1 FROM SectionFlowCategoryItems
                    WHERE SettingId = @settingId AND ProcessGroupName = N'荒管处理' AND SectionName = N'检验' AND Coefficient = -1.0
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 删除 L 的排除项
            migrationBuilder.Sql(@"
                DELETE i
                FROM SectionFlowCategoryItems i
                INNER JOIN SectionFlowCategorySettings s ON i.SettingId = s.Id
                WHERE s.CategoryCode = 'L'
                  AND i.ProcessGroupName = N'荒管处理'
                  AND i.SectionName = N'检验'
                  AND i.Coefficient = -1.0
            ");
        }
    }
}
