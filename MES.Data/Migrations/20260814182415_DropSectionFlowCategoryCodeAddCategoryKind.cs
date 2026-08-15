using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropSectionFlowCategoryCodeAddCategoryKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_SFCS_CategoryCode",
                table: "SectionFlowCategorySettings");

            migrationBuilder.DropColumn(
                name: "CategoryCode",
                table: "SectionFlowCategorySettings");

            migrationBuilder.AddColumn<string>(
                name: "CategoryKind",
                table: "SectionFlowCategorySettings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            // 回填类别类型：按类别名称映射（荒管检/在制检/成品待检 → 检验类枚举名，其余普通）
            migrationBuilder.Sql(@"
                UPDATE SectionFlowCategorySettings
                SET CategoryKind = CASE CategoryName
                    WHEN N'荒管检' THEN 'RoughTubeInspection'
                    WHEN N'在制检' THEN 'InProcessInspection'
                    WHEN N'成品待检' THEN 'FinalInspection'
                    ELSE 'Normal' END;
            ");

            // 组合归类表不再承载检验类配置（检验类由「生产-工段日流转量」CategoryKind + 代码派生），删除未归属的检验行
            migrationBuilder.Sql(@"
                DELETE FROM CombinationGroups WHERE FlowCategoryId IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CategoryKind",
                table: "SectionFlowCategorySettings");

            migrationBuilder.AddColumn<string>(
                name: "CategoryCode",
                table: "SectionFlowCategorySettings",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "UK_SFCS_CategoryCode",
                table: "SectionFlowCategorySettings",
                column: "CategoryCode",
                unique: true);
        }
    }
}
