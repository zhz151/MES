using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedMissingConfigParameterKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 补齐真库 ConfigParameters 缺失的 3 个有效配置键（种子后新增、种子 !Any() 幂等不补，功能一直靠代码兜底默认值运行）。
            // 补齐后配置页可见可调，与种子定义一致：
            // - LengthDefault.PipeLength（默认管长 6000mm）
            // - MaterialPlanTolerance.InputConsistencyTolerance（到料实投一致性容差 ±3%，档5缺口率阈值同用）
            // - WarehouseThreshold.PurchaseOverRatio（采购超额比率阈值 1.05）
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [ConfigParameters] WHERE [Category] = 'LengthDefault' AND [ParamKey] = 'PipeLength')
                    INSERT INTO [ConfigParameters] ([Category], [CategoryDisplay], [Context], [ParamKey], [ParamValue], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES ('LengthDefault', '工单-长度默认值', '工单', 'PipeLength', 6000, '默认管长(mm)', SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), '');
                IF NOT EXISTS (SELECT 1 FROM [ConfigParameters] WHERE [Category] = 'MaterialPlanTolerance' AND [ParamKey] = 'InputConsistencyTolerance')
                    INSERT INTO [ConfigParameters] ([Category], [CategoryDisplay], [Context], [ParamKey], [ParamValue], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES ('MaterialPlanTolerance', '工单-用料计划执行容差', '工单', 'InputConsistencyTolerance', 0.03, '到料实投一致性容差(±3%)与档5缺口率阈值', SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), '');
                IF NOT EXISTS (SELECT 1 FROM [ConfigParameters] WHERE [Category] = 'WarehouseThreshold' AND [ParamKey] = 'PurchaseOverRatio')
                    INSERT INTO [ConfigParameters] ([Category], [CategoryDisplay], [Context], [ParamKey], [ParamValue], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                    VALUES ('WarehouseThreshold', '采购-超额比率', '物料', 'PurchaseOverRatio', 1.05, '采购超额比率阈值(实际采购/委外量>计划量×此比率判定超额采购/超额穿孔)', SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), '');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 撤销：删除本迁移补齐的 3 个配置键
            migrationBuilder.Sql("""
                DELETE FROM [ConfigParameters]
                WHERE ([Category] = 'LengthDefault' AND [ParamKey] = 'PipeLength')
                   OR ([Category] = 'MaterialPlanTolerance' AND [ParamKey] = 'InputConsistencyTolerance')
                   OR ([Category] = 'WarehouseThreshold' AND [ParamKey] = 'PurchaseOverRatio');
                """);
        }
    }
}
