using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class CleanConfigParameterDeadKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 配置审计②：清理 8 个无代码消费的死配置键（git 种子历史注册后被删除、未写清理迁移、全库 0 消费）。
            // 917c666「配置审计①」已删 ValidInputUpper/Lower + RoughTubeFinishRatio，本次补齐遗漏：
            // - ProductionThreshold.InspectionInputUpper/Lower（检验投料比率上下限，代码不再读取）
            // - ProductionCapacity.Polish/Mill50_60/Mill20_30/ThreeRoll/DrawBench（日产能，被 ColdRollCapacity/DailyProductionCapacity 替代）
            // - DefaultValue.ProcessCycle（默认工序周期，被 DefaultProcessCycle 取代）
            migrationBuilder.Sql("""
                DELETE FROM [ConfigParameters]
                WHERE ([Category] = 'ProductionThreshold' AND [ParamKey] IN ('InspectionInputUpper', 'InspectionInputLower'))
                   OR ([Category] = 'ProductionCapacity' AND [ParamKey] IN ('Polish', 'Mill50_60', 'Mill20_30', 'ThreeRoll', 'DrawBench'))
                   OR ([Category] = 'DefaultValue' AND [ParamKey] = 'ProcessCycle');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 撤销：重建 8 个被删除的死配置键（沿用原种子值）
            migrationBuilder.Sql(@"
INSERT INTO [ConfigParameters]
    ([Category], [CategoryDisplay], [Context], [ParamKey], [ParamValue], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
VALUES
    ('ProductionThreshold', NULL, NULL, 'InspectionInputUpper', 1.02, '检验投料比率上限', SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('ProductionThreshold', NULL, NULL, 'InspectionInputLower', 0.98, '检验投料比率下限', SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('ProductionCapacity', NULL, NULL, 'Polish', 12, '荒管抛光日产能(吨)', SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('ProductionCapacity', NULL, NULL, 'Mill50_60', 11, '50/60轧机日产能(吨)', SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('ProductionCapacity', NULL, NULL, 'Mill20_30', 9, '20/30轧机日产能(吨)', SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('ProductionCapacity', NULL, NULL, 'ThreeRoll', 0.5, '三辊轧机日产能(吨)', SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('ProductionCapacity', NULL, NULL, 'DrawBench', 3, '拉机日产能(吨)', SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('DefaultValue', NULL, NULL, 'ProcessCycle', 25, '默认工序周期(天)', SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), '')");
        }
    }
}
