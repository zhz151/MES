using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductionStatusChineseToEnglishKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 产类 ProductStatus 中文化 → 英文稳定 Key（枚举化）
            // 存量值域已验证（无脏值）：成品→Finished / 荒管→RoughTube / 在制→InProgress
            var caseSql = "CASE ProductStatus WHEN N'成品' THEN N'Finished' WHEN N'荒管' THEN N'RoughTube' WHEN N'在制' THEN N'InProgress' ELSE ProductStatus END";
            foreach (var table in new[] { "ProductionRecord", "SectionOutsource", "PicklingInRecord", "PicklingOutRecord", "ProcessInspection" })
            {
                migrationBuilder.Sql($"UPDATE [{table}] SET ProductStatus = {caseSql} WHERE ProductStatus IS NOT NULL AND ProductStatus != ''");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 英文 Key → 中文（回滚）
            var caseSql = "CASE ProductStatus WHEN N'Finished' THEN N'成品' WHEN N'RoughTube' THEN N'荒管' WHEN N'InProgress' THEN N'在制' ELSE ProductStatus END";
            foreach (var table in new[] { "ProductionRecord", "SectionOutsource", "PicklingInRecord", "PicklingOutRecord", "ProcessInspection" })
            {
                migrationBuilder.Sql($"UPDATE [{table}] SET ProductStatus = {caseSql} WHERE ProductStatus IS NOT NULL AND ProductStatus != ''");
            }
        }
    }
}
