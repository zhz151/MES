using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBatchPlanScheduleFlowLevelFiveTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 等级 4 档 → 5 档（V5.28：1=急+ 2=急 3=急- 4=一般 5=略，特急A/B 手工档已删除，急+ 直接透传实时档位）：
            // 存量 1(特急A/B)→1(急+)、2(急)→2(急)、3(一般)→4(一般)、4(略)→5(略)；重跑 PlanAllAsync 会按三规则重建，此为近似保序迁移
            migrationBuilder.Sql(
                "UPDATE [BatchPlanSchedules] SET [FlowLevel] = [FlowLevel] + 1 WHERE [FlowLevel] >= 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 反向仅能近似恢复：4(一般)→3、5(略)→4；原 1/2 保持
            migrationBuilder.Sql(
                "UPDATE [BatchPlanSchedules] SET [FlowLevel] = [FlowLevel] - 1 WHERE [FlowLevel] >= 4");
        }
    }
}
