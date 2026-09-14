using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedDisposalMethodReturnAndNcrReturnThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 不合格品处置方式新增第 4 档「退货」（EnumDisplayDefinition 显示配置，幂等）
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM EnumDisplayDefinitions WHERE EnumKey = 'DisposalMethod' AND Value = 'Return')
INSERT INTO EnumDisplayDefinitions (EnumKey, Value, DisplayName, DisplayOrder, Remark, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
VALUES ('DisposalMethod', 'Return', N'退货', 4, NULL, SYSDATETIMEOFFSET(), N'system', SYSDATETIMEOFFSET(), N'system');");

            // NCR 触发阈值新增退货档（默认与报废档一致：3 支 / 5%）
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM ConfigParameters WHERE Category = 'NcrThreshold' AND ParamKey = 'ReturnCount')
INSERT INTO ConfigParameters (Category, CategoryDisplay, Context, ParamKey, ParamValue, Remark, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
VALUES ('NcrThreshold', N'质量-NCR触发阈值', N'质量', 'ReturnCount', 5, N'退货触发绝对支数', SYSDATETIMEOFFSET(), N'system', SYSDATETIMEOFFSET(), N'system');
IF NOT EXISTS (SELECT 1 FROM ConfigParameters WHERE Category = 'NcrThreshold' AND ParamKey = 'ReturnPercent')
INSERT INTO ConfigParameters (Category, CategoryDisplay, Context, ParamKey, ParamValue, Remark, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
VALUES ('NcrThreshold', N'质量-NCR触发阈值', N'质量', 'ReturnPercent', 0.05, N'退货触发百分比', SYSDATETIMEOFFSET(), N'system', SYSDATETIMEOFFSET(), N'system');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM EnumDisplayDefinitions WHERE EnumKey = 'DisposalMethod' AND Value = 'Return';
DELETE FROM ConfigParameters WHERE Category = 'NcrThreshold' AND ParamKey IN ('ReturnCount', 'ReturnPercent');");
        }
    }
}
