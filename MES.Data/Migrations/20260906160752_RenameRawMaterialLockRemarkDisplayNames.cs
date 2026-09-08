using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameRawMaterialLockRemarkDisplayNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 原锁备注（RawMaterialLockRemarkKey）四类显示中文更名（2026-09-07 用户决策）：
            //   A质量补料→质量补料 / B执行返整→生产返整补足 / C执行计划→执行用料计划 / D完善计划→完善用料计划
            // 仅改 DisplayName（表意清晰），DisplayOrder 保持不变（不做排序调整）。
            // 存量旧前缀名（A质量补料 等）由 RawMaterialLockRemarkKeys.ChineseToKey 兼容映射兜底。
            migrationBuilder.Sql(@"
UPDATE [DictValueDefinitions] SET [DisplayName] = N'质量补料',      [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
WHERE [DictKey] = 'RawMaterialLockRemarkKey' AND [Value] = 'QualityReplenish';

UPDATE [DictValueDefinitions] SET [DisplayName] = N'生产返整补足',  [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
WHERE [DictKey] = 'RawMaterialLockRemarkKey' AND [Value] = 'ExecuteRework';

UPDATE [DictValueDefinitions] SET [DisplayName] = N'执行用料计划',  [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
WHERE [DictKey] = 'RawMaterialLockRemarkKey' AND [Value] = 'ExecutePlan';

UPDATE [DictValueDefinitions] SET [DisplayName] = N'完善用料计划',  [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
WHERE [DictKey] = 'RawMaterialLockRemarkKey' AND [Value] = 'ImprovePlan';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE [DictValueDefinitions] SET [DisplayName] = N'A质量补料',     [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
WHERE [DictKey] = 'RawMaterialLockRemarkKey' AND [Value] = 'QualityReplenish';

UPDATE [DictValueDefinitions] SET [DisplayName] = N'B执行返整',     [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
WHERE [DictKey] = 'RawMaterialLockRemarkKey' AND [Value] = 'ExecuteRework';

UPDATE [DictValueDefinitions] SET [DisplayName] = N'C执行计划',     [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
WHERE [DictKey] = 'RawMaterialLockRemarkKey' AND [Value] = 'ExecutePlan';

UPDATE [DictValueDefinitions] SET [DisplayName] = N'D完善计划',     [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
WHERE [DictKey] = 'RawMaterialLockRemarkKey' AND [Value] = 'ImprovePlan';");
        }
    }
}
