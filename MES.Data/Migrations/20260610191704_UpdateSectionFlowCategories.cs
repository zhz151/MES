using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSectionFlowCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===== 更新 K 荒管检 =====
            migrationBuilder.Sql("UPDATE SectionFlowCategorySettings SET CategoryName = N'荒管检' WHERE CategoryCode = 'K'");

            // ===== 更新 L 在制检 =====
            migrationBuilder.Sql("UPDATE SectionFlowCategorySettings SET CategoryName = N'在制检' WHERE CategoryCode = 'L'");

            // ===== 删除 K 和 L 的旧明细项 =====
            migrationBuilder.Sql("DELETE FROM SectionFlowCategoryItems WHERE SettingId IN (SELECT Id FROM SectionFlowCategorySettings WHERE CategoryCode IN ('K','L'))");

            // ===== 插入 K 的新明细项 (荒管处理, 检验) =====
            migrationBuilder.Sql(@"INSERT INTO SectionFlowCategoryItems (SettingId, ProcessGroupName, SectionName, Coefficient, DisplayOrder, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
                SELECT Id, N'荒管处理', N'检验', 1.0, 1, GETDATE(), '', GETDATE(), ''
                FROM SectionFlowCategorySettings WHERE CategoryCode = 'K'");

            // ===== 插入 L 的新明细项 (在制修检, 全部) =====
            migrationBuilder.Sql(@"INSERT INTO SectionFlowCategoryItems (SettingId, ProcessGroupName, SectionName, Coefficient, DisplayOrder, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
                SELECT Id, N'在制修检', N'全部', 1.0, 1, GETDATE(), '', GETDATE(), ''
                FROM SectionFlowCategorySettings WHERE CategoryCode = 'L'");

            // ===== 插入 M 成品待检 =====
            migrationBuilder.Sql(@"INSERT INTO SectionFlowCategorySettings (CategoryCode, CategoryName, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
                VALUES ('M', N'成品待检', GETDATE(), '', GETDATE(), '')");

            // ===== 插入 M 的明细项 (在制修检, 全部) =====
            migrationBuilder.Sql(@"INSERT INTO SectionFlowCategoryItems (SettingId, ProcessGroupName, SectionName, Coefficient, DisplayOrder, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
                SELECT Id, N'在制修检', N'全部', 1.0, 1, GETDATE(), '', GETDATE(), ''
                FROM SectionFlowCategorySettings WHERE CategoryCode = 'M'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ===== 删除 M 类别及明细（外键级联删除明细） =====
            migrationBuilder.Sql("DELETE FROM SectionFlowCategorySettings WHERE CategoryCode = 'M'");

            // ===== 回退 K 和 L 的名称 =====
            migrationBuilder.Sql("UPDATE SectionFlowCategorySettings SET CategoryName = N'过程检' WHERE CategoryCode = 'K'");
            migrationBuilder.Sql("UPDATE SectionFlowCategorySettings SET CategoryName = N'成品待检' WHERE CategoryCode = 'L'");

            // ===== 删除新插入的 K/L 明细 =====
            migrationBuilder.Sql("DELETE FROM SectionFlowCategoryItems WHERE SettingId IN (SELECT Id FROM SectionFlowCategorySettings WHERE CategoryCode IN ('K','L'))");

            // ===== 恢复 K 的 8 个原始明细项 =====
            migrationBuilder.Sql(@"
                DECLARE @KId int = (SELECT Id FROM SectionFlowCategorySettings WHERE CategoryCode = 'K');
                INSERT INTO SectionFlowCategoryItems (SettingId, ProcessGroupName, SectionName, Coefficient, DisplayOrder, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy) VALUES
                (@KId, N'20冷轧',   N'检验', 1.0,   1, GETDATE(), '', GETDATE(), ''),
                (@KId, N'30冷轧',   N'检验', 1.0,   2, GETDATE(), '', GETDATE(), ''),
                (@KId, N'冷拔',     N'检验', 1.0,   3, GETDATE(), '', GETDATE(), ''),
                (@KId, N'三辊冷轧', N'检验', 1.0,   4, GETDATE(), '', GETDATE(), ''),
                (@KId, N'荒管处理', N'检验', 0.75,  5, GETDATE(), '', GETDATE(), ''),
                (@KId, N'在制修检', N'检验', 0.75,  6, GETDATE(), '', GETDATE(), ''),
                (@KId, N'50冷轧',   N'检验', 0.5,   7, GETDATE(), '', GETDATE(), ''),
                (@KId, N'60冷轧',   N'检验', 0.5,   8, GETDATE(), '', GETDATE(), '');
            ");

            // ===== 恢复 L 的 8 个原始明细项 =====
            migrationBuilder.Sql(@"
                DECLARE @LId int = (SELECT Id FROM SectionFlowCategorySettings WHERE CategoryCode = 'L');
                INSERT INTO SectionFlowCategoryItems (SettingId, ProcessGroupName, SectionName, Coefficient, DisplayOrder, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy) VALUES
                (@LId, N'20冷轧',   N'检验', 1.0,   1, GETDATE(), '', GETDATE(), ''),
                (@LId, N'30冷轧',   N'检验', 1.0,   2, GETDATE(), '', GETDATE(), ''),
                (@LId, N'50冷轧',   N'检验', 0.5,   3, GETDATE(), '', GETDATE(), ''),
                (@LId, N'60冷轧',   N'检验', 0.5,   4, GETDATE(), '', GETDATE(), ''),
                (@LId, N'荒管处理', N'检验', 0.75,  5, GETDATE(), '', GETDATE(), ''),
                (@LId, N'冷拔',     N'检验', 1.0,   6, GETDATE(), '', GETDATE(), ''),
                (@LId, N'三辊冷轧', N'检验', 1.0,   7, GETDATE(), '', GETDATE(), ''),
                (@LId, N'在制修检', N'检验', 0.75,  8, GETDATE(), '', GETDATE(), '');
            ");
        }
    }
}
