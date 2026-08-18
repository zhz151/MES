using MES.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260819100000_SeedSubcontractOrderOverReceived")]
    public partial class SeedSubcontractOrderOverReceived : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 子项查询新增「超量到货」状态档位（SubcontractOrderStatus.OverReceived）+ 委外超量回收配置：
            // 1. 枚举显示配置补 OverReceived→超量到货（新增枚举值不触发 DbInitializer 的 !Any() 种子，存量库需数据迁移补行）；
            // 2. 业务参数补「委外-超额比率」SubcontractOverRatio=1.05 与「委外-超额偏差」SubcontractOverDeviation=100kg
            //    （回收重量>需求重量×超额比率 且 超出量>超额偏差 → 超量到货，仿采购订单超量到货判定）。
            // 均幂等 IF NOT EXISTS 锚定唯一索引（UK_EDD_EnumKey_Value / UK_CP_Category_ParamKey）。
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [EnumDisplayDefinitions] WHERE [EnumKey] = N'SubcontractOrderStatus' AND [Value] = N'OverReceived')
                    INSERT INTO [EnumDisplayDefinitions] ([EnumKey], [Value], [DisplayName], [DisplayOrder], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'SubcontractOrderStatus', N'OverReceived', N'超量到货', 4, N'回收重量>需求重量×超额比率 且 超出量>超额偏差阈值', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');

                IF NOT EXISTS (SELECT 1 FROM [ConfigParameters] WHERE [Category] = N'WarehouseThreshold' AND [ParamKey] = N'SubcontractOverRatio')
                    INSERT INTO [ConfigParameters] ([Category], [CategoryDisplay], [Context], [ParamKey], [ParamValue], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WarehouseThreshold', N'委外-超额比率', N'物料', N'SubcontractOverRatio', 1.05, N'委外超额回收比率阈值(回收重量>需求重量×此比率判定超量回收)', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');

                IF NOT EXISTS (SELECT 1 FROM [ConfigParameters] WHERE [Category] = N'WarehouseThreshold' AND [ParamKey] = N'SubcontractOverDeviation')
                    INSERT INTO [ConfigParameters] ([Category], [CategoryDisplay], [Context], [ParamKey], [ParamValue], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES (N'WarehouseThreshold', N'委外-超额偏差', N'物料', N'SubcontractOverDeviation', 100, N'委外超额回收绝对偏差(kg)：回收重量>需求×超额比率且超出量>此阈值判定超量到货', SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回退：删除本迁移写入的枚举显示行与参数行（仅限 CreatedBy=System 的本类行）
            migrationBuilder.Sql("""
                DELETE FROM [EnumDisplayDefinitions]
                WHERE [EnumKey] = N'SubcontractOrderStatus' AND [Value] = N'OverReceived' AND [CreatedBy] = N'System';

                DELETE FROM [ConfigParameters]
                WHERE [Category] = N'WarehouseThreshold' AND [ParamKey] = N'SubcontractOverRatio' AND [CreatedBy] = N'System';

                DELETE FROM [ConfigParameters]
                WHERE [Category] = N'WarehouseThreshold' AND [ParamKey] = N'SubcontractOverDeviation' AND [CreatedBy] = N'System';
                """);
        }
    }
}
