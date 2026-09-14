using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdjustNcrDisposalMethodTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NCR「处置方式」由 4 档（返整/入库/报废/退货）拆为 5 档（返整/入在制库/可入备库/入次品库/退货）：
            //   WarehouseEntry（入库）→ InProcessWarehouse（入在制库，来源过程检验）/ FinishedWarehouse（可入备库，来源成品检验）
            //   Scrap（报废）→ 枚举名不变，中文显示改「入次品库」
            // Ncr 表无来源类型字段，存量 WarehouseEntry 行无法判别过程检/成品检来源，统一回写为 InProcessWarehouse。
            migrationBuilder.Sql(@"
UPDATE Ncr SET DisposalMethod = 'InProcessWarehouse', UpdatedTime = SYSDATETIMEOFFSET()
WHERE DisposalMethod = 'WarehouseEntry';");

            // 枚举显示配置（EnumDisplayDefinitions）幂等切换
            migrationBuilder.Sql(@"
DELETE FROM EnumDisplayDefinitions WHERE EnumKey = 'DisposalMethod' AND Value = 'WarehouseEntry';

IF NOT EXISTS (SELECT 1 FROM EnumDisplayDefinitions WHERE EnumKey = 'DisposalMethod' AND Value = 'InProcessWarehouse')
INSERT INTO EnumDisplayDefinitions (EnumKey, Value, DisplayName, DisplayOrder, Remark, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
VALUES ('DisposalMethod', 'InProcessWarehouse', N'入在制库', 2, NULL, SYSDATETIMEOFFSET(), N'system', SYSDATETIMEOFFSET(), N'system');

IF NOT EXISTS (SELECT 1 FROM EnumDisplayDefinitions WHERE EnumKey = 'DisposalMethod' AND Value = 'FinishedWarehouse')
INSERT INTO EnumDisplayDefinitions (EnumKey, Value, DisplayName, DisplayOrder, Remark, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
VALUES ('DisposalMethod', 'FinishedWarehouse', N'可入备库', 3, NULL, SYSDATETIMEOFFSET(), N'system', SYSDATETIMEOFFSET(), N'system');

UPDATE EnumDisplayDefinitions SET DisplayName = N'入次品库', DisplayOrder = 4, UpdatedTime = SYSDATETIMEOFFSET()
WHERE EnumKey = 'DisposalMethod' AND Value = 'Scrap';

UPDATE EnumDisplayDefinitions SET DisplayOrder = 5, UpdatedTime = SYSDATETIMEOFFSET()
WHERE EnumKey = 'DisposalMethod' AND Value = 'Return';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE Ncr SET DisposalMethod = 'WarehouseEntry', UpdatedTime = SYSDATETIMEOFFSET()
WHERE DisposalMethod IN ('InProcessWarehouse', 'FinishedWarehouse');");

            migrationBuilder.Sql(@"
DELETE FROM EnumDisplayDefinitions WHERE EnumKey = 'DisposalMethod' AND Value IN ('InProcessWarehouse', 'FinishedWarehouse');

IF NOT EXISTS (SELECT 1 FROM EnumDisplayDefinitions WHERE EnumKey = 'DisposalMethod' AND Value = 'WarehouseEntry')
INSERT INTO EnumDisplayDefinitions (EnumKey, Value, DisplayName, DisplayOrder, Remark, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
VALUES ('DisposalMethod', 'WarehouseEntry', N'入库', 2, NULL, SYSDATETIMEOFFSET(), N'system', SYSDATETIMEOFFSET(), N'system');

UPDATE EnumDisplayDefinitions SET DisplayName = N'报废', DisplayOrder = 3, UpdatedTime = SYSDATETIMEOFFSET()
WHERE EnumKey = 'DisposalMethod' AND Value = 'Scrap';

UPDATE EnumDisplayDefinitions SET DisplayOrder = 4, UpdatedTime = SYSDATETIMEOFFSET()
WHERE EnumKey = 'DisposalMethod' AND Value = 'Return';");
        }
    }
}
