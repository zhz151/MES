using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 工序 ProcessName 存储值 中文 → 英文 Key（与工段 SectionName 解耦同方案）。
    /// 覆盖 11 处存储位置：ProcessGroup / ProductionRecord / SectionOutsource /
    /// PicklingInRecord / PicklingOutRecord / ProcessInspection / MaterialReceiveCheck /
    /// InProcessReworkPlanProcessGroup / InventoryPlanProcessGroup / PiercingPlanProcessGroup /
    /// SemiPlanProcessGroup 的 ProcessName，ProductionBatch 的 CurrentGroupName / NextProcess。
    /// 9 种规范中文 → Key；未知值保留原值（ELSE 原列，ProcessGroupTemplate 的"冷轧"等非规范名不受影响）。
    /// 「收尾-成检」哨兵置 NULL（虚拟工序，非真实工序组，P9 语义已移除）。
    /// </summary>
    public partial class ProcessNameChineseToEnglishKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"UPDATE ProcessGroup SET ProcessName = {BuildCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE ProductionRecord SET ProcessName = {BuildCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE SectionOutsource SET ProcessName = {BuildCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE PicklingInRecord SET ProcessName = {BuildCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE PicklingOutRecord SET ProcessName = {BuildCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE ProcessInspection SET ProcessName = {BuildCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE MaterialReceiveCheck SET ProcessName = {BuildCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE InProcessReworkPlanProcessGroup SET ProcessName = {BuildCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE InventoryPlanProcessGroup SET ProcessName = {BuildCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE PiercingPlanProcessGroup SET ProcessName = {BuildCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE SemiPlanProcessGroup SET ProcessName = {BuildCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE ProductionBatch SET CurrentGroupName = {BuildCase("CurrentGroupName")};");
            migrationBuilder.Sql($"UPDATE ProductionBatch SET NextProcess = {BuildCase("NextProcess")};");
            // 收尾-成检 哨兵置 NULL（虚拟工序，非真实工序组）
            migrationBuilder.Sql("UPDATE WorkOrderExecutionSummary SET MainNoAttentionProcess = NULL WHERE MainNoAttentionProcess = N'收尾-成检';");
            migrationBuilder.Sql("UPDATE WorkOrderExecutionSummary SET ProductionAttentionProcess = NULL WHERE ProductionAttentionProcess = N'收尾-成检';");
            migrationBuilder.Sql("UPDATE WorkOrderPlan SET ProductionAttentionProcess = NULL WHERE ProductionAttentionProcess = N'收尾-成检';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"UPDATE ProcessGroup SET ProcessName = {BuildReverseCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE ProductionRecord SET ProcessName = {BuildReverseCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE SectionOutsource SET ProcessName = {BuildReverseCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE PicklingInRecord SET ProcessName = {BuildReverseCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE PicklingOutRecord SET ProcessName = {BuildReverseCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE ProcessInspection SET ProcessName = {BuildReverseCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE MaterialReceiveCheck SET ProcessName = {BuildReverseCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE InProcessReworkPlanProcessGroup SET ProcessName = {BuildReverseCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE InventoryPlanProcessGroup SET ProcessName = {BuildReverseCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE PiercingPlanProcessGroup SET ProcessName = {BuildReverseCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE SemiPlanProcessGroup SET ProcessName = {BuildReverseCase("ProcessName")};");
            migrationBuilder.Sql($"UPDATE ProductionBatch SET CurrentGroupName = {BuildReverseCase("CurrentGroupName")};");
            migrationBuilder.Sql($"UPDATE ProductionBatch SET NextProcess = {BuildReverseCase("NextProcess")};");
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
