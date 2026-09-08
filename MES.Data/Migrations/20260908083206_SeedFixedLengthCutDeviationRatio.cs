using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedFixedLengthCutDeviationRatio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 定尺联通视图主号「切割偏差」阈值配置键（2026-09-08 起由硬编码 5% 改为配置驱动，主号为汇总数据默认 3.5%）。
            // DbInitializer 种子仅空库生效；真库需迁移幂等补插（同 SeedMissingConfigParameterKeys 模式）。
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [ConfigParameters] WHERE [Category] = 'FixedLengthCutRatio' AND [ParamKey] = 'CutDeviationRatio')
                    INSERT INTO [ConfigParameters] ([Category], [CategoryDisplay], [Context], [ParamKey], [ParamValue], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES ('FixedLengthCutRatio', '工单-定尺切割偏差', '工单', 'CutDeviationRatio', 0.035, '定尺主号切割偏差阈值(3.5%)：|实切支数-理论已切|/理论已切>此比率判定异常', SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), '');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 撤销：删除本迁移补齐的配置键
            migrationBuilder.Sql("""
                DELETE FROM [ConfigParameters]
                WHERE [Category] = 'FixedLengthCutRatio' AND [ParamKey] = 'CutDeviationRatio';
                """);
        }
    }
}
