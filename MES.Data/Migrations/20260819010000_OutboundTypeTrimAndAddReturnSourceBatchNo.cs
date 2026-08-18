using MES.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260819010000_OutboundTypeTrimAndAddReturnSourceBatchNo")]
    public partial class OutboundTypeTrimAndAddReturnSourceBatchNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 出库模块调整（仓库管理上下文）：
            // 1. OutboundType 枚举精简 8 值→5 值：删除 ScrapOut(报废出库)/InspectionPick(检验领用)/TransferOut(移库出库)，
            //    保留 ProductionPick/SalesOut/ReturnOut/SubcontractOut/OtherOut。枚举以字符串存储，仅需清理显示配置；
            // 2. 出库新增列「退货-原批次号」ReturnSourceBatchNo（退货出库时记录原批次号，全类型可填可空）；
            // 3. 存量 OutboundRecord 类型 TransferOut 归并入 OtherOut（其它出库），保留业务数据。
            migrationBuilder.Sql("""
                -- 1. 新增列：退货-原批次号（幂等，防重复执行）
                IF COL_LENGTH(N'[OutboundRecord]', N'ReturnSourceBatchNo') IS NULL
                    ALTER TABLE [OutboundRecord] ADD [ReturnSourceBatchNo] nvarchar(100) NULL;

                -- 2. 清理枚举显示配置：删除废弃的 3 个出库类型
                DELETE FROM [EnumDisplayDefinitions]
                WHERE [EnumKey] = N'OutboundType' AND [Value] IN (N'ScrapOut', N'InspectionPick', N'TransferOut');

                -- 3. OtherOut 显示名统一为「其它出库」
                UPDATE [EnumDisplayDefinitions]
                SET [DisplayName] = N'其它出库', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                WHERE [EnumKey] = N'OutboundType' AND [Value] = N'OtherOut';

                -- 4. 重排 DisplayOrder：删除 5/6/7 后 OtherOut 从 8 → 5
                UPDATE [EnumDisplayDefinitions]
                SET [DisplayOrder] = 5, [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                WHERE [EnumKey] = N'OutboundType' AND [Value] = N'OtherOut';

                -- 5. 存量归并：移库出库(TransferOut) → 其它出库(OtherOut)
                UPDATE [OutboundRecord]
                SET [OutboundType] = N'OtherOut', [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                WHERE [OutboundType] = N'TransferOut';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回退：恢复废弃类型显示行 + OtherOut 显示名/顺序 + 删除新列
            // 注意：存量 TransferOut→OtherOut 的归并不回写（已无法区分原记录）
            migrationBuilder.Sql("""
                -- 恢复枚举显示行（DisplayOrder 5/6/7，重复下行会先删再插，保持幂等）
                DELETE FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'OutboundType' AND [Value] = N'OtherOut';
                INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                VALUES (N'OutboundType', N'OtherOut', N'其他出库', 8, NULL, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                VALUES (N'OutboundType', N'ScrapOut', N'报废出库', 5, NULL, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                VALUES (N'OutboundType', N'InspectionPick', N'检验领用', 6, NULL, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                VALUES (N'OutboundType', N'TransferOut', N'移库出库', 7, NULL, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');

                IF COL_LENGTH(N'[OutboundRecord]', N'ReturnSourceBatchNo') IS NOT NULL
                    ALTER TABLE [OutboundRecord] DROP COLUMN [ReturnSourceBatchNo];
                """);
        }
    }
}
