using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 存量枚举/字典字段中文残留数据修正（2026-08-06 全库合规复查发现）：
    /// 迁移漏网的历史中文值 → 统一转英文 Key/枚举名。
    /// 覆盖：OrderListSummary.UrgencyLevel / ColdRollSpecSchedule.ProcessType /
    /// BatchPlanSchedules.FlowCRType / InventoryBatch.ManufacturingStatus /
    /// InventoryPlan.MaterialType / ProductionRecord.Shift /
    /// ProductionBatch.SourceLengthStatus / InventoryBatch.LengthStatus('-')。
    /// </summary>
    public partial class FixLegacyEnumChineseData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. OrderListSummary.UrgencyLevel 中文 → UrgencyLevelKeys
            migrationBuilder.Sql("""
                UPDATE [OrderListSummary] SET UrgencyLevel = CASE UrgencyLevel
                    WHEN N'A+急' THEN 'APlusUrgent' WHEN N'A急' THEN 'AUrgent' WHEN N'B顺' THEN 'BOrder'
                    WHEN N'C缓' THEN 'CSlow' WHEN N'D缓' THEN 'DSlow' WHEN N'E停' THEN 'EPaused'
                    ELSE UrgencyLevel END
                """);

            // 2. ColdRollSpecSchedule.ProcessType 中文 → ProcessKeys
            migrationBuilder.Sql("""
                UPDATE [ColdRollSpecSchedule] SET ProcessType = CASE ProcessType
                    WHEN N'60冷轧' THEN 'ColdRoll60' WHEN N'50冷轧' THEN 'ColdRoll50' WHEN N'30冷轧' THEN 'ColdRoll30'
                    WHEN N'20冷轧' THEN 'ColdRoll20' WHEN N'三辊冷轧' THEN 'ThreeRollColdRoll' WHEN N'冷拔' THEN 'ColdDraw'
                    WHEN N'荒管处理' THEN 'RoughTubeProcessing' WHEN N'在制修检' THEN 'InProcessRepair' WHEN N'附加成检' THEN 'AdditionalFinalInspection'
                    ELSE ProcessType END
                """);

            // 3. BatchPlanSchedules.FlowCRType 中文 → ProcessKeys，'-' 占位 → NULL
            migrationBuilder.Sql("""
                UPDATE [BatchPlanSchedules] SET FlowCRType = CASE FlowCRType
                    WHEN N'60冷轧' THEN 'ColdRoll60' WHEN N'50冷轧' THEN 'ColdRoll50' WHEN N'30冷轧' THEN 'ColdRoll30'
                    WHEN N'20冷轧' THEN 'ColdRoll20' WHEN N'三辊冷轧' THEN 'ThreeRollColdRoll' WHEN N'冷拔' THEN 'ColdDraw'
                    WHEN N'荒管处理' THEN 'RoughTubeProcessing' WHEN N'在制修检' THEN 'InProcessRepair' WHEN N'附加成检' THEN 'AdditionalFinalInspection'
                    ELSE FlowCRType END
                """);
            migrationBuilder.Sql("UPDATE [BatchPlanSchedules] SET FlowCRType = NULL WHERE FlowCRType = '-'");

            // 4. InventoryBatch.ManufacturingStatus 中文 → DeliveryState 英文
            migrationBuilder.Sql("""
                UPDATE [InventoryBatch] SET ManufacturingStatus = CASE ManufacturingStatus
                    WHEN N'固溶酸洗' THEN 'SolutionAnnealedAndPickled' WHEN N'固溶矫直' THEN 'SolidSolutionStraightening'
                    ELSE ManufacturingStatus END
                """);

            // 5. InventoryPlan.MaterialType 中文 → MaterialType 英文（反查关联库存批次确认）
            migrationBuilder.Sql("""
                UPDATE [InventoryPlan] SET MaterialType = CASE MaterialType
                    WHEN N'备料成品' THEN 'Finished' WHEN N'余库料' THEN 'Surplus'
                    ELSE MaterialType END
                """);

            // 6. ProductionRecord.Shift 中文 → ShiftType 英文
            migrationBuilder.Sql("""
                UPDATE [ProductionRecord] SET Shift = CASE Shift
                    WHEN N'白' THEN 'DayShift' WHEN N'白班' THEN 'DayShift' WHEN N'夜' THEN 'NightShift'
                    WHEN N'中' THEN 'MiddleShift' WHEN N'中班' THEN 'MiddleShift'
                    ELSE Shift END
                """);

            // 7. ProductionBatch.SourceLengthStatus '范围尺' → Range
            migrationBuilder.Sql("UPDATE [ProductionBatch] SET SourceLengthStatus = 'Range' WHERE SourceLengthStatus = N'范围尺'");

            // 8. InventoryBatch.LengthStatus '-' 脏数据 → NULL
            migrationBuilder.Sql("UPDATE [InventoryBatch] SET LengthStatus = NULL WHERE LengthStatus = '-'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 反向（回滚用，仅覆盖 Up 中可确定映射；InventoryBatch.LengthStatus 由 '-'→NULL 无法精确还原，回滚时不处理）
            migrationBuilder.Sql("""
                UPDATE [ProductionBatch] SET SourceLengthStatus = N'范围尺' WHERE SourceLengthStatus = 'Range'
                """);
            migrationBuilder.Sql("""
                UPDATE [ProductionRecord] SET Shift = CASE Shift
                    WHEN 'DayShift' THEN N'白班' WHEN 'NightShift' THEN N'夜' WHEN 'MiddleShift' THEN N'中班'
                    ELSE Shift END
                """);
            migrationBuilder.Sql("""
                UPDATE [InventoryPlan] SET MaterialType = CASE MaterialType
                    WHEN 'Finished' THEN N'备料成品' WHEN 'Surplus' THEN N'余库料'
                    ELSE MaterialType END
                """);
            migrationBuilder.Sql("""
                UPDATE [InventoryBatch] SET ManufacturingStatus = CASE ManufacturingStatus
                    WHEN 'SolutionAnnealedAndPickled' THEN N'固溶酸洗' WHEN 'SolidSolutionStraightening' THEN N'固溶矫直'
                    ELSE ManufacturingStatus END
                """);
            migrationBuilder.Sql("""
                UPDATE [BatchPlanSchedules] SET FlowCRType = CASE FlowCRType
                    WHEN 'ColdRoll60' THEN N'60冷轧' WHEN 'ColdRoll50' THEN N'50冷轧' WHEN 'ColdRoll30' THEN N'30冷轧'
                    WHEN 'ColdRoll20' THEN N'20冷轧' WHEN 'ThreeRollColdRoll' THEN N'三辊冷轧' WHEN 'ColdDraw' THEN N'冷拔'
                    WHEN 'RoughTubeProcessing' THEN N'荒管处理' WHEN 'InProcessRepair' THEN N'在制修检' WHEN 'AdditionalFinalInspection' THEN N'附加成检'
                    ELSE FlowCRType END
                """);
            migrationBuilder.Sql("""
                UPDATE [ColdRollSpecSchedule] SET ProcessType = CASE ProcessType
                    WHEN 'ColdRoll60' THEN N'60冷轧' WHEN 'ColdRoll50' THEN N'50冷轧' WHEN 'ColdRoll30' THEN N'30冷轧'
                    WHEN 'ColdRoll20' THEN N'20冷轧' WHEN 'ThreeRollColdRoll' THEN N'三辊冷轧' WHEN 'ColdDraw' THEN N'冷拔'
                    WHEN 'RoughTubeProcessing' THEN N'荒管处理' WHEN 'InProcessRepair' THEN N'在制修检' WHEN 'AdditionalFinalInspection' THEN N'附加成检'
                    ELSE ProcessType END
                """);
            migrationBuilder.Sql("""
                UPDATE [OrderListSummary] SET UrgencyLevel = CASE UrgencyLevel
                    WHEN 'APlusUrgent' THEN N'A+急' WHEN 'AUrgent' THEN N'A急' WHEN 'BOrder' THEN N'B顺'
                    WHEN 'CSlow' THEN N'C缓' WHEN 'DSlow' THEN N'D缓' WHEN 'EPaused' THEN N'E停'
                    ELSE UrgencyLevel END
                """);
        }
    }
}
