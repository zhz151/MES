using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProductRequirementNavigationFromProductionStandard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 直接删除 ProductionStandardId 列（忽略不存在的约束）
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductRequirement') AND name = 'ProductionStandardId')
                BEGIN
                    ALTER TABLE [ProductRequirement] DROP COLUMN [ProductionStandardId];
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚时重新添加列（允许 NULL）
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProductRequirement') AND name = 'ProductionStandardId')
                BEGIN
                    ALTER TABLE [ProductRequirement] ADD [ProductionStandardId] int NULL;
                END
            ");
        }
    }
}