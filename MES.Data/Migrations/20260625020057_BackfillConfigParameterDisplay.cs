using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillConfigParameterDisplay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 回填已有记录的 CategoryDisplay 和 Context
            var sql = @"
UPDATE ConfigParameters SET CategoryDisplay = N'仓库-完工阈值', Context = N'采购+仓库' WHERE Category = 'WarehouseThreshold';
UPDATE ConfigParameters SET CategoryDisplay = N'批次-生产阈值', Context = N'批次' WHERE Category = 'ProductionThreshold';
UPDATE ConfigParameters SET CategoryDisplay = N'工单-用料计划比率', Context = N'工单' WHERE Category = 'MaterialPlanRatio';
UPDATE ConfigParameters SET CategoryDisplay = N'工单-尺寸公差', Context = N'工单' WHERE Category = 'DimensionTolerance';
UPDATE ConfigParameters SET CategoryDisplay = N'工单-改制系数', Context = N'工单' WHERE Category = 'ReworkRatio';
UPDATE ConfigParameters SET CategoryDisplay = N'工单-长度默认值', Context = N'工单' WHERE Category = 'LengthDefault';
UPDATE ConfigParameters SET CategoryDisplay = N'工单-用料计划状态阈值', Context = N'工单' WHERE Category = 'MaterialPlanStatus';
UPDATE ConfigParameters SET CategoryDisplay = N'批次-加工损耗率', Context = N'批次' WHERE Category = 'ProcessingDiscount';
UPDATE ConfigParameters SET CategoryDisplay = N'工单-交期排程天数', Context = N'工单' WHERE Category = 'WorkOrderDays';
UPDATE ConfigParameters SET CategoryDisplay = N'工单-紧急度阈值', Context = N'工单' WHERE Category = 'UrgencyThreshold';
UPDATE ConfigParameters SET CategoryDisplay = N'排程-日期桶', Context = N'排程' WHERE Category = 'DateBucket';
UPDATE ConfigParameters SET CategoryDisplay = N'排程-日产能', Context = N'排程' WHERE Category = 'ProductionCapacity';
UPDATE ConfigParameters SET CategoryDisplay = N'批次-工序跳号', Context = N'批次' WHERE Category = 'SequenceJump';
UPDATE ConfigParameters SET CategoryDisplay = N'订单-合同重量校验', Context = N'订单' WHERE Category = 'ContractWeight';
UPDATE ConfigParameters SET CategoryDisplay = N'通用-默认值', Context = N'通用' WHERE Category = 'DefaultValue';
-- 删除无代码引用的旧参数
DELETE FROM ConfigParameters WHERE Category = 'DefaultValue' AND ParamKey = 'ProcessCycle';

";
            migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var sql = @"
UPDATE ConfigParameters SET CategoryDisplay = NULL, Context = NULL WHERE Category IN (
    'WarehouseThreshold','ProductionThreshold','MaterialPlanRatio','DimensionTolerance',
    'ReworkRatio','LengthDefault','MaterialPlanStatus','ProcessingDiscount',
    'WorkOrderDays','UrgencyThreshold','DateBucket','ProductionCapacity',
    'SequenceJump','ContractWeight','DefaultValue'
);";
            migrationBuilder.Sql(sql);
        }
    }
}
