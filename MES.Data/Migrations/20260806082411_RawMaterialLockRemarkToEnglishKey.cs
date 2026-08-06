using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RawMaterialLockRemarkToEnglishKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 原锁备注 RawMaterialLockRemark 存量中文 → 英文 Key（§4.10 后端存储一律英文）
            // 四类：A质量补料→QualityReplenish / B执行返整→ExecuteRework / C执行计划→ExecutePlan / D完善计划→ImprovePlan
            migrationBuilder.Sql("""
                UPDATE [WorkOrderExecutionSummary]
                SET [RawMaterialLockRemark] = N'QualityReplenish'
                WHERE [RawMaterialLockRemark] = N'A质量补料'
                """);
            migrationBuilder.Sql("""
                UPDATE [WorkOrderExecutionSummary]
                SET [RawMaterialLockRemark] = N'ExecuteRework'
                WHERE [RawMaterialLockRemark] = N'B执行返整'
                """);
            migrationBuilder.Sql("""
                UPDATE [WorkOrderExecutionSummary]
                SET [RawMaterialLockRemark] = N'ExecutePlan'
                WHERE [RawMaterialLockRemark] = N'C执行计划'
                """);
            migrationBuilder.Sql("""
                UPDATE [WorkOrderExecutionSummary]
                SET [RawMaterialLockRemark] = N'ImprovePlan'
                WHERE [RawMaterialLockRemark] = N'D完善计划'
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [WorkOrderExecutionSummary]
                SET [RawMaterialLockRemark] = N'A质量补料'
                WHERE [RawMaterialLockRemark] = N'QualityReplenish'
                """);
            migrationBuilder.Sql("""
                UPDATE [WorkOrderExecutionSummary]
                SET [RawMaterialLockRemark] = N'B执行返整'
                WHERE [RawMaterialLockRemark] = N'ExecuteRework'
                """);
            migrationBuilder.Sql("""
                UPDATE [WorkOrderExecutionSummary]
                SET [RawMaterialLockRemark] = N'C执行计划'
                WHERE [RawMaterialLockRemark] = N'ExecutePlan'
                """);
            migrationBuilder.Sql("""
                UPDATE [WorkOrderExecutionSummary]
                SET [RawMaterialLockRemark] = N'D完善计划'
                WHERE [RawMaterialLockRemark] = N'ImprovePlan'
                """);
        }
    }
}
