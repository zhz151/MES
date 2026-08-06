using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 补充：关注工序列（ProductionAttentionProcess / MainNoAttentionProcess）中文 → 英文 Key。
    /// 前序迁移 ProcessNameChineseToEnglishKey 已处理 11 表 ProcessName + ProductionBatch 2 列，
    /// 但漏了 WorkOrderExecutionSummary / WorkOrderPlan 的关注工序列（非 ProcessName 列名）。
    /// 「收尾-成检」哨兵已在前序迁移置 NULL，此处 9 种规范中文 → Key；未知值保留原值。
    /// </summary>
    public partial class ProcessNameAttentionProcessToKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"UPDATE WorkOrderExecutionSummary SET ProductionAttentionProcess = {BuildCase("ProductionAttentionProcess")};");
            migrationBuilder.Sql($"UPDATE WorkOrderExecutionSummary SET MainNoAttentionProcess = {BuildCase("MainNoAttentionProcess")};");
            migrationBuilder.Sql($"UPDATE WorkOrderPlan SET ProductionAttentionProcess = {BuildCase("ProductionAttentionProcess")};");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"UPDATE WorkOrderExecutionSummary SET ProductionAttentionProcess = {BuildReverseCase("ProductionAttentionProcess")};");
            migrationBuilder.Sql($"UPDATE WorkOrderExecutionSummary SET MainNoAttentionProcess = {BuildReverseCase("MainNoAttentionProcess")};");
            migrationBuilder.Sql($"UPDATE WorkOrderPlan SET ProductionAttentionProcess = {BuildReverseCase("ProductionAttentionProcess")};");
        }

        /// <summary>中文 → 英文 Key 的 CASE 表达式</summary>
        private static string BuildCase(string column)
            => $@"CASE {column}
    WHEN N'荒管处理' THEN 'RoughTubeProcessing'
    WHEN N'在制修检' THEN 'InProcessRepair'
    WHEN N'60冷轧' THEN 'ColdRoll60'
    WHEN N'50冷轧' THEN 'ColdRoll50'
    WHEN N'30冷轧' THEN 'ColdRoll30'
    WHEN N'20冷轧' THEN 'ColdRoll20'
    WHEN N'三辊冷轧' THEN 'ThreeRollColdRoll'
    WHEN N'冷拔' THEN 'ColdDraw'
    WHEN N'附加成检' THEN 'AdditionalFinalInspection'
    ELSE {column} END";

        /// <summary>英文 Key → 规范中文 的 CASE 表达式（Down 反向）</summary>
        private static string BuildReverseCase(string column)
            => $@"CASE {column}
    WHEN 'RoughTubeProcessing' THEN N'荒管处理'
    WHEN 'InProcessRepair' THEN N'在制修检'
    WHEN 'ColdRoll60' THEN N'60冷轧'
    WHEN 'ColdRoll50' THEN N'50冷轧'
    WHEN 'ColdRoll30' THEN N'30冷轧'
    WHEN 'ColdRoll20' THEN N'20冷轧'
    WHEN 'ThreeRollColdRoll' THEN N'三辊冷轧'
    WHEN 'ColdDraw' THEN N'冷拔'
    WHEN 'AdditionalFinalInspection' THEN N'附加成检'
    ELSE {column} END";
    }
}
