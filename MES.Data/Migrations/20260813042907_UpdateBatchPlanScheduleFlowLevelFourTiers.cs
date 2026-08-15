using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBatchPlanScheduleFlowLevelFourTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 等级 5 档 → 4 档：5(非流转) → 4(略)、4(其余流转) → 3(一般)、3(B顺) → 3(一般)
            migrationBuilder.Sql(
                "UPDATE [BatchPlanSchedules] SET [FlowLevel] = CASE WHEN [FlowLevel] = 5 THEN 4 WHEN [FlowLevel] = 4 THEN 3 ELSE [FlowLevel] END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 反向仅能近似恢复：4(略) → 5(非流转)；原 3/4 合并不可逆，保持 3
            migrationBuilder.Sql(
                "UPDATE [BatchPlanSchedules] SET [FlowLevel] = 5 WHERE [FlowLevel] = 4");
        }
    }
}
