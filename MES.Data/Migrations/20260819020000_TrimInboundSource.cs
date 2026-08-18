using MES.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260819020000_TrimInboundSource")]
    public partial class TrimInboundSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 入库来源（InboundSource）枚举精简 7 值→5 值（仓库管理上下文）：
            // 删除 ReturnIn(退货入库)/TransferIn(移库入库)，保留 Purchase/Subcontract/ProductionInbound/InspectionInbound/Other。
            // 枚举以字符串存储（InventoryBatch.InboundSource 为 nvarchar(20)），真库无 ReturnIn/TransferIn 存量记录，
            // 仅需清理显示配置 EnumDisplayDefinitions 并重排 DisplayOrder。
            migrationBuilder.Sql("""
                -- 1. 清理枚举显示配置：删除废弃的 2 个入库来源
                DELETE FROM [EnumDisplayDefinitions]
                WHERE [EnumKey] = N'InboundSource' AND [Value] IN (N'ReturnIn', N'TransferIn');

                -- 2. 重排 DisplayOrder：ProductionInbound 4→3、InspectionInbound 5→4、Other 7→5
                UPDATE [EnumDisplayDefinitions]
                SET [DisplayOrder] = 3, [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                WHERE [EnumKey] = N'InboundSource' AND [Value] = N'ProductionInbound';
                UPDATE [EnumDisplayDefinitions]
                SET [DisplayOrder] = 4, [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                WHERE [EnumKey] = N'InboundSource' AND [Value] = N'InspectionInbound';
                UPDATE [EnumDisplayDefinitions]
                SET [DisplayOrder] = 5, [UpdatedTime] = SYSDATETIMEOFFSET(), [UpdatedBy] = N'System'
                WHERE [EnumKey] = N'InboundSource' AND [Value] = N'Other';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回退：恢复废弃类型显示行 + 恢复原 DisplayOrder（重复下行会先删再插，保持幂等）
            migrationBuilder.Sql("""
                -- 先删 InboundSource 全部行，再按原 7 值顺序重插
                DELETE FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'InboundSource';
                INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                VALUES (N'InboundSource', N'Purchase', N'外购', 1, NULL, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                VALUES (N'InboundSource', N'Subcontract', N'委外', 2, NULL, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                VALUES (N'InboundSource', N'ReturnIn', N'退货入库', 3, NULL, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                VALUES (N'InboundSource', N'ProductionInbound', N'生产入库', 4, NULL, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                VALUES (N'InboundSource', N'InspectionInbound', N'检验入库', 5, NULL, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                VALUES (N'InboundSource', N'TransferIn', N'移库入库', 6, NULL, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                VALUES (N'InboundSource', N'Other', N'其它', 7, NULL, SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);
        }
    }
}
