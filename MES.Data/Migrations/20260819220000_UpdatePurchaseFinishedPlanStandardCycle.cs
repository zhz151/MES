using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 存量成品采购（外购）计划 StandardCycle 对齐当前配置 DefaultValue.StandardCycle=2（2026-08-19 用户决策）。
    /// 存量 33 条创建于 2026-05-19，当时默认值为 3 写入后固化；配置现为 2，仅对新创建生效，存量需一次性对齐。
    /// </summary>
    public partial class UpdatePurchaseFinishedPlanStandardCycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [PurchaseFinishedPlan] SET [StandardCycle] = 2 WHERE [StandardCycle] = 3;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回退：把 StandardCycle=2 的成品采购计划恢复为 3（含回滚后新建，尽力恢复旧口径）
            migrationBuilder.Sql("""
                UPDATE [PurchaseFinishedPlan] SET [StandardCycle] = 3 WHERE [StandardCycle] = 2;
                """);
        }
    }
}
