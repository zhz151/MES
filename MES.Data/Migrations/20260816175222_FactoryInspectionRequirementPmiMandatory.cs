using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class FactoryInspectionRequirementPmiMandatory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // R17：工厂检验项要求，所有标准的 PMI 检验与「表检+尺寸」一致，均设定为「必检」
            // （预填技术要求时 PMI 带出「终」；此前种子为「按需」）
            migrationBuilder.Sql("""
                UPDATE [FactoryInspectionRequirement]
                SET [PmiInspection] = N'必检'
                WHERE [PmiInspection] <> N'必检';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 撤销：恢复为「按需」（仅回退本迁移改过的行，避免误伤后续手工修改）
            migrationBuilder.Sql("""
                UPDATE [FactoryInspectionRequirement]
                SET [PmiInspection] = N'按需';
                """);
        }
    }
}
