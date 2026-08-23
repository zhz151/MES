using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNcrResponsibilityDictionary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NCR 责任类别由固定枚举改为可扩展字典（DictKey=NcrResponsibilityKey）：
            // 内置 5 项沿用原 ResponsibilityCategory 枚举名作英文 Key，存量 NCR 数据零迁移。
            migrationBuilder.Sql(@"
INSERT INTO [DictValueDefinitions]
    ([DictKey], [Value], [DisplayName], [DisplayOrder], [IsEnabled], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
VALUES
    ('NcrResponsibilityKey', 'ProductionInternal', '生产-厂内', 1, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('NcrResponsibilityKey', 'ProductionOutsource', '生产-外协', 2, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('NcrResponsibilityKey', 'MaterialTubeBlank', '原料-荒管', 3, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('NcrResponsibilityKey', 'MaterialPurchased', '原料-外购成品', 4, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('NcrResponsibilityKey', 'MaterialSurplus', '原料-余库料', 5, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), '')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [DictValueDefinitions] WHERE [DictKey] = 'NcrResponsibilityKey'");
        }
    }
}
