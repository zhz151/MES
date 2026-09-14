using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNcrFlowDirectionAndDisposalDictionary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DisposalMethod",
                table: "Ncr",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FlowDirection",
                table: "Ncr",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ncr_FlowDirection",
                table: "Ncr",
                column: "FlowDirection");

            // NCR 处置方式由固定枚举改为可扩展字典（DictKey=NcrDisposalKey）：
            // 内置 8 档；其中 5 档沿用原 DisposalMethod 枚举名作英文 Key，
            // 存量 Ncr.DisposalMethod 数据零迁移（原列语义即「流向」，对应 8 档中的同 5 类）。
            // 说明：原列留作「处置方式」并扩容为 8 档字典；新增 FlowDirection 列为「流向」（枚举 5 档，
            // 由过程检验/成品检验记录带出，人工上报来源为空）。
            migrationBuilder.Sql(@"
INSERT INTO [DictValueDefinitions]
    ([DictKey], [Value], [DisplayName], [DisplayOrder], [IsEnabled], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
VALUES
    ('NcrDisposalKey', 'Concession', '让步放行', 1, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('NcrDisposalKey', 'Reprocess', '返工', 2, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('NcrDisposalKey', 'Rework', '返整(新卡流转)', 3, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('NcrDisposalKey', 'InProcessWarehouse', '入在制库(可改制)', 4, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('NcrDisposalKey', 'FinishedWarehouse', '可入备库(尺寸偏差)', 5, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('NcrDisposalKey', 'ScrapCorrection', '入次品库(修正改制)', 6, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('NcrDisposalKey', 'Scrap', '入次品库(报废)', 7, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('NcrDisposalKey', 'Return', '退货', 8, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), '')");

            // 枚举改名：EnumDisplayDefinitions 的 EnumKey 约定=枚举类型名，
            // 原 DisposalMethod 枚举已更名为 FlowDirection（语义=流向），配置行 EnumKey 一并改名，
            // 否则该 5 行成为指向不存在枚举的死数据、且 FlowDirection 拿不到配置表覆盖。
            migrationBuilder.Sql(@"
UPDATE [EnumDisplayDefinitions] SET [EnumKey] = 'FlowDirection', [UpdatedTime] = SYSDATETIMEOFFSET()
WHERE [EnumKey] = 'DisposalMethod'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE [EnumDisplayDefinitions] SET [EnumKey] = 'DisposalMethod', [UpdatedTime] = SYSDATETIMEOFFSET()
WHERE [EnumKey] = 'FlowDirection'");

            migrationBuilder.Sql(@"
DELETE FROM [DictValueDefinitions] WHERE [DictKey] = 'NcrDisposalKey'");

            migrationBuilder.DropIndex(
                name: "IX_Ncr_FlowDirection",
                table: "Ncr");

            migrationBuilder.DropColumn(
                name: "FlowDirection",
                table: "Ncr");

            migrationBuilder.AlterColumn<string>(
                name: "DisposalMethod",
                table: "Ncr",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);
        }
    }
}
