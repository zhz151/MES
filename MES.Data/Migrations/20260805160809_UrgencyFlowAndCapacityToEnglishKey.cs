using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class UrgencyFlowAndCapacityToEnglishKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ========== WorkOrderExecutionSummary / WorkOrderPlan：UrgencyLevel 中文 → 英文 Key ==========
            migrationBuilder.Sql(
                "UPDATE [WorkOrderExecutionSummary] SET [UrgencyLevel] = CASE [UrgencyLevel] " +
                "WHEN N'A+急' THEN 'APlusUrgent' WHEN N'A急' THEN 'AUrgent' " +
                "WHEN N'B顺' THEN 'BOrder' WHEN N'C缓' THEN 'CSlow' " +
                "WHEN N'D缓' THEN 'DSlow' WHEN N'E停' THEN 'EPaused' ELSE [UrgencyLevel] END;");

            migrationBuilder.Sql(
                "UPDATE [WorkOrderPlan] SET [UrgencyLevel] = CASE [UrgencyLevel] " +
                "WHEN N'A+急' THEN 'APlusUrgent' WHEN N'A急' THEN 'AUrgent' " +
                "WHEN N'B顺' THEN 'BOrder' WHEN N'C缓' THEN 'CSlow' " +
                "WHEN N'D缓' THEN 'DSlow' WHEN N'E停' THEN 'EPaused' ELSE [UrgencyLevel] END;");

            // ========== WorkOrderExecutionSummary / WorkOrderPlan：ProductionFlowProperty 中文 → 英文 Key ==========
            migrationBuilder.Sql(
                "UPDATE [WorkOrderExecutionSummary] SET [ProductionFlowProperty] = CASE [ProductionFlowProperty] " +
                "WHEN N'正常' THEN 'Normal' WHEN N'暂停' THEN 'Paused' " +
                "WHEN N'待料' THEN 'Waiting' WHEN N'疑问' THEN 'Doubt' WHEN N'略' THEN 'Skip' " +
                "ELSE [ProductionFlowProperty] END;");

            migrationBuilder.Sql(
                "UPDATE [WorkOrderPlan] SET [ProductionFlowProperty] = CASE [ProductionFlowProperty] " +
                "WHEN N'正常' THEN 'Normal' WHEN N'暂停' THEN 'Paused' " +
                "WHEN N'待料' THEN 'Waiting' WHEN N'疑问' THEN 'Doubt' WHEN N'略' THEN 'Skip' " +
                "ELSE [ProductionFlowProperty] END;");

            // ========== BatchPlanSchedule：FlowTarget 中文 → 英文 Key ==========
            migrationBuilder.Sql(
                "UPDATE [BatchPlanSchedules] SET [FlowTarget] = CASE [FlowTarget] " +
                "WHEN N'成检' THEN 'Inspection' WHEN N'完工冷轧' THEN 'CompletionColdRoll' WHEN N'冷轧' THEN 'ColdRoll' " +
                "ELSE [FlowTarget] END;");

            // ========== DailyProductionCapacities：ProcessName 中文 → 英文 Key ==========
            migrationBuilder.Sql(
                "UPDATE [DailyProductionCapacities] SET [ProcessName] = CASE [ProcessName] " +
                "WHEN N'荒管抛光' THEN 'Polish' WHEN N'50,60轧机' THEN 'Mill50_60' " +
                "WHEN N'20,30轧机' THEN 'Mill20_30' WHEN N'三辊轧机' THEN 'ThreeRollMill' " +
                "WHEN N'拉机' THEN 'DrawBench' ELSE [ProcessName] END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ========== 反向回滚：英文 Key → 中文 ==========
            migrationBuilder.Sql(
                "UPDATE [WorkOrderExecutionSummary] SET [UrgencyLevel] = CASE [UrgencyLevel] " +
                "WHEN 'APlusUrgent' THEN N'A+急' WHEN 'AUrgent' THEN N'A急' " +
                "WHEN 'BOrder' THEN N'B顺' WHEN 'CSlow' THEN N'C缓' " +
                "WHEN 'DSlow' THEN N'D缓' WHEN 'EPaused' THEN N'E停' ELSE [UrgencyLevel] END;");

            migrationBuilder.Sql(
                "UPDATE [WorkOrderPlan] SET [UrgencyLevel] = CASE [UrgencyLevel] " +
                "WHEN 'APlusUrgent' THEN N'A+急' WHEN 'AUrgent' THEN N'A急' " +
                "WHEN 'BOrder' THEN N'B顺' WHEN 'CSlow' THEN N'C缓' " +
                "WHEN 'DSlow' THEN N'D缓' WHEN 'EPaused' THEN N'E停' ELSE [UrgencyLevel] END;");

            migrationBuilder.Sql(
                "UPDATE [WorkOrderExecutionSummary] SET [ProductionFlowProperty] = CASE [ProductionFlowProperty] " +
                "WHEN 'Normal' THEN N'正常' WHEN 'Paused' THEN N'暂停' " +
                "WHEN 'Waiting' THEN N'待料' WHEN 'Doubt' THEN N'疑问' WHEN 'Skip' THEN N'略' " +
                "ELSE [ProductionFlowProperty] END;");

            migrationBuilder.Sql(
                "UPDATE [WorkOrderPlan] SET [ProductionFlowProperty] = CASE [ProductionFlowProperty] " +
                "WHEN 'Normal' THEN N'正常' WHEN 'Paused' THEN N'暂停' " +
                "WHEN 'Waiting' THEN N'待料' WHEN 'Doubt' THEN N'疑问' WHEN 'Skip' THEN N'略' " +
                "ELSE [ProductionFlowProperty] END;");

            migrationBuilder.Sql(
                "UPDATE [BatchPlanSchedules] SET [FlowTarget] = CASE [FlowTarget] " +
                "WHEN 'Inspection' THEN N'成检' WHEN 'CompletionColdRoll' THEN N'完工冷轧' WHEN 'ColdRoll' THEN N'冷轧' " +
                "ELSE [FlowTarget] END;");

            migrationBuilder.Sql(
                "UPDATE [DailyProductionCapacities] SET [ProcessName] = CASE [ProcessName] " +
                "WHEN 'Polish' THEN N'荒管抛光' WHEN 'Mill50_60' THEN N'50,60轧机' " +
                "WHEN 'Mill20_30' THEN N'20,30轧机' WHEN 'ThreeRollMill' THEN N'三辊轧机' " +
                "WHEN 'DrawBench' THEN N'拉机' ELSE [ProcessName] END;");
        }
    }
}
