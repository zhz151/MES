using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixChineseCategoryValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var sql = @"
-- 修复用户已录入的中文 Category 值（导致后端 GetConfigMapAsync() 无法匹配）
UPDATE ConfigParameters SET Category = 'WarehouseThreshold', CategoryDisplay = N'仓库-完工阈值', Context = N'采购+仓库' WHERE Category = N'工段委外-完工阈值';
UPDATE ConfigParameters SET Category = 'DateBucket', CategoryDisplay = N'排程-日期桶', Context = N'排程' WHERE Category = N'订单总览页面-时间桶';
UPDATE ConfigParameters SET Category = 'SequenceJump', CategoryDisplay = N'批次-工序跳号', Context = N'批次' WHERE Category = N'生产记录-工序跳号';
UPDATE ConfigParameters SET Category = 'LengthDefault', CategoryDisplay = N'工单-长度默认值', Context = N'工单' WHERE Category = N'用料计划-非定尺管长';
";
            migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var sql = @"
-- 回退：将英文 Category 恢复为中文值（仅回退本次迁移修改过的记录）
UPDATE ConfigParameters SET Category = N'工段委外-完工阈值', CategoryDisplay = NULL, Context = NULL WHERE Category = 'WarehouseThreshold' AND CategoryDisplay = N'仓库-完工阈值' AND ParamKey NOT IN (SELECT Category FROM ConfigParameters WHERE Category = 'WarehouseThreshold' AND ParamKey != N'OutsourceRecoveryRatio');
-- 精确回退难以实现，因为同 Category 下还有其他记录。这里只回退明确被改动的记录
UPDATE ConfigParameters SET Category = N'工段委外-完工阈值', CategoryDisplay = NULL, Context = NULL WHERE Category = 'WarehouseThreshold' AND ParamKey = 'OutsourceRecoveryRatio';
UPDATE ConfigParameters SET Category = N'订单总览页面-时间桶', CategoryDisplay = NULL, Context = NULL WHERE Category = 'DateBucket' AND ParamKey IN ('Bucket1','Bucket2','Bucket3','Bucket4','Bucket5');
UPDATE ConfigParameters SET Category = N'生产记录-工序跳号', CategoryDisplay = NULL, Context = NULL WHERE Category = 'SequenceJump' AND ParamKey = 'MaxJump';
UPDATE ConfigParameters SET Category = N'用料计划-非定尺管长', CategoryDisplay = NULL, Context = NULL WHERE Category = 'LengthDefault' AND ParamKey = 'UnitWeightLength';
";
            migrationBuilder.Sql(sql);
        }
    }
}
