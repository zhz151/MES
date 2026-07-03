using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixOutsourceRecoveryRatioContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var sql = @"
-- OutsourceRecoveryRatio 用于委外管理和生产记录，属于批次上下文
UPDATE ConfigParameters SET Context = N'批次' WHERE Category = 'WarehouseThreshold' AND ParamKey = 'OutsourceRecoveryRatio';
";
            migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var sql = @"
UPDATE ConfigParameters SET Context = N'采购+仓库' WHERE Category = 'WarehouseThreshold' AND ParamKey = 'OutsourceRecoveryRatio';
";
            migrationBuilder.Sql(sql);
        }
    }
}
