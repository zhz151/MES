using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 订单负荷总量「交期截止负荷量」日期桶边界改为 7 桶：交期截止-今日 / 今日+7 / 今日+15 / 今日+30 / 今日+45 / 今日+60 / 远日量（2026-08-19 用户决策）。
    /// 存量 ConfigParameters 的 DateBucket 边界由 15/30/45/60/90 更新为 7/15/30/45/60。
    /// </summary>
    public partial class UpdateDateBucketBoundaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [ConfigParameters] SET [ParamValue] = 7  WHERE [Category] = N'DateBucket' AND [ParamKey] = N'Bucket1';
                UPDATE [ConfigParameters] SET [ParamValue] = 15 WHERE [Category] = N'DateBucket' AND [ParamKey] = N'Bucket2';
                UPDATE [ConfigParameters] SET [ParamValue] = 30 WHERE [Category] = N'DateBucket' AND [ParamKey] = N'Bucket3';
                UPDATE [ConfigParameters] SET [ParamValue] = 45 WHERE [Category] = N'DateBucket' AND [ParamKey] = N'Bucket4';
                UPDATE [ConfigParameters] SET [ParamValue] = 60 WHERE [Category] = N'DateBucket' AND [ParamKey] = N'Bucket5';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回退：恢复旧边界 15/30/45/60/90
            migrationBuilder.Sql("""
                UPDATE [ConfigParameters] SET [ParamValue] = 15 WHERE [Category] = N'DateBucket' AND [ParamKey] = N'Bucket1';
                UPDATE [ConfigParameters] SET [ParamValue] = 30 WHERE [Category] = N'DateBucket' AND [ParamKey] = N'Bucket2';
                UPDATE [ConfigParameters] SET [ParamValue] = 45 WHERE [Category] = N'DateBucket' AND [ParamKey] = N'Bucket3';
                UPDATE [ConfigParameters] SET [ParamValue] = 60 WHERE [Category] = N'DateBucket' AND [ParamKey] = N'Bucket4';
                UPDATE [ConfigParameters] SET [ParamValue] = 90 WHERE [Category] = N'DateBucket' AND [ParamKey] = N'Bucket5';
                """);
        }
    }
}
