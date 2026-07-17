using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <summary>
    /// 初始压缩迁移 — 聚合所有历史迁移为一。
    /// Up/Down 为空，因为表结构已存在于生产数据库。
    /// 作用仅有：在 __EFMigrationsHistory 中记录一条记录。
    /// </summary>
    public partial class InitialCompressed : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
