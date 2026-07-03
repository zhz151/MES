using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixDefaultValueContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var sql = @"
-- StandardCycle 用于用料计划，归属工单上下文
UPDATE ConfigParameters SET CategoryDisplay = N'工单-标准周期', Context = N'工单' WHERE Category = 'DefaultValue' AND ParamKey = 'StandardCycle';

-- BatchMaxSequence 用于批次号生成，归属批次上下文
UPDATE ConfigParameters SET CategoryDisplay = N'批次-最大序号', Context = N'批次' WHERE Category = 'DefaultValue' AND ParamKey = 'BatchMaxSequence';

-- RoughTubeFinishRatio 用于工单执行，归属工单上下文
UPDATE ConfigParameters SET CategoryDisplay = N'工单-荒管成品系数', Context = N'工单' WHERE Category = 'DefaultValue' AND ParamKey = 'RoughTubeFinishRatio';

-- 删除 DefaultValue 下未被代码读取的冗余 PipeLength 记录（代码从 LengthDefault 读取）
DELETE FROM ConfigParameters WHERE Category = 'DefaultValue' AND ParamKey = 'PipeLength';
";
            migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var sql = @"
UPDATE ConfigParameters SET CategoryDisplay = N'通用-默认值', Context = N'通用' WHERE Category = 'DefaultValue';
";
            migrationBuilder.Sql(sql);
        }
    }
}
