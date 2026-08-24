using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRawMaterialLockRemarkAndAttentionDicts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) 新增 2 个可配置字典的默认行：
            //    - 原锁备注（RawMaterialLockRemarkKey）4 档英文 Key，显示名可经配置页调整
            //    - 生产关注工序特殊值（ProductionAttentionKey）1 值（生产收尾）
            // 2) 删除 3 个无代码消费的死配置键（审计确认：种子注册但全库无消费）：
            //    - ProductionThreshold.ValidInputUpper / ValidInputLower（docs 记载代码不再读取）
            //    - DefaultValue.RoughTubeFinishRatio（docs 声称被消费但实际无消费点）
            migrationBuilder.Sql(@"
INSERT INTO [DictValueDefinitions]
    ([DictKey], [Value], [DisplayName], [DisplayOrder], [IsEnabled], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
VALUES
    ('RawMaterialLockRemarkKey', 'QualityReplenish', 'A质量补料', 1, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('RawMaterialLockRemarkKey', 'ExecuteRework', 'B执行返整', 2, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('RawMaterialLockRemarkKey', 'ExecutePlan', 'C执行计划', 3, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('RawMaterialLockRemarkKey', 'ImprovePlan', 'D完善计划', 4, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('ProductionAttentionKey', 'ProductionFinish', '生产收尾', 1, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), '')");

            migrationBuilder.Sql(@"
DELETE FROM [ConfigParameters]
WHERE ([Category] = 'ProductionThreshold' AND [ParamKey] IN ('ValidInputUpper', 'ValidInputLower'))
   OR ([Category] = 'DefaultValue' AND [ParamKey] = 'RoughTubeFinishRatio')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [DictValueDefinitions]
WHERE [DictKey] IN ('RawMaterialLockRemarkKey', 'ProductionAttentionKey')");

            migrationBuilder.Sql(@"
INSERT INTO [ConfigParameters]
    ([Category], [CategoryDisplay], [Context], [ParamKey], [ParamValue], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
VALUES
    ('ProductionThreshold', '批次-生产阈值', '批次', 'ValidInputUpper', 1.03, '有效投料比率上限', SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('ProductionThreshold', '批次-生产阈值', '批次', 'ValidInputLower', 0.97, '有效投料比率下限', SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('DefaultValue', '工单-荒管成品系数', '工单', 'RoughTubeFinishRatio', 0.92, '荒管转成品系数', SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), '')");
        }
    }
}
