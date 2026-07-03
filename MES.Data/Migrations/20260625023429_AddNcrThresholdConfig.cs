using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNcrThresholdConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            var sql = $@"
IF NOT EXISTS (SELECT 1 FROM ConfigParameters WHERE Category = 'NcrThreshold' AND ParamKey = 'ReworkCount')
    INSERT INTO ConfigParameters (Category, CategoryDisplay, Context, ParamKey, ParamValue, Remark, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
    VALUES ('NcrThreshold', N'质量-NCR触发阈值', N'质量', 'ReworkCount', 5, N'返工触发绝对支数', '{now}', 'system', '{now}', 'system');

IF NOT EXISTS (SELECT 1 FROM ConfigParameters WHERE Category = 'NcrThreshold' AND ParamKey = 'ReworkPercent')
    INSERT INTO ConfigParameters (Category, CategoryDisplay, Context, ParamKey, ParamValue, Remark, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
    VALUES ('NcrThreshold', N'质量-NCR触发阈值', N'质量', 'ReworkPercent', 0.05, N'返工触发百分比', '{now}', 'system', '{now}', 'system');

IF NOT EXISTS (SELECT 1 FROM ConfigParameters WHERE Category = 'NcrThreshold' AND ParamKey = 'WarehouseCount')
    INSERT INTO ConfigParameters (Category, CategoryDisplay, Context, ParamKey, ParamValue, Remark, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
    VALUES ('NcrThreshold', N'质量-NCR触发阈值', N'质量', 'WarehouseCount', 5, N'让步接收触发绝对支数', '{now}', 'system', '{now}', 'system');

IF NOT EXISTS (SELECT 1 FROM ConfigParameters WHERE Category = 'NcrThreshold' AND ParamKey = 'WarehousePercent')
    INSERT INTO ConfigParameters (Category, CategoryDisplay, Context, ParamKey, ParamValue, Remark, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
    VALUES ('NcrThreshold', N'质量-NCR触发阈值', N'质量', 'WarehousePercent', 0.05, N'让步接收触发百分比', '{now}', 'system', '{now}', 'system');

IF NOT EXISTS (SELECT 1 FROM ConfigParameters WHERE Category = 'NcrThreshold' AND ParamKey = 'ScrapCount')
    INSERT INTO ConfigParameters (Category, CategoryDisplay, Context, ParamKey, ParamValue, Remark, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
    VALUES ('NcrThreshold', N'质量-NCR触发阈值', N'质量', 'ScrapCount', 3, N'报废触发绝对支数', '{now}', 'system', '{now}', 'system');

IF NOT EXISTS (SELECT 1 FROM ConfigParameters WHERE Category = 'NcrThreshold' AND ParamKey = 'ScrapPercent')
    INSERT INTO ConfigParameters (Category, CategoryDisplay, Context, ParamKey, ParamValue, Remark, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy)
    VALUES ('NcrThreshold', N'质量-NCR触发阈值', N'质量', 'ScrapPercent', 0.05, N'报废触发百分比', '{now}', 'system', '{now}', 'system');
";
            migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var sql = @"
DELETE FROM ConfigParameters WHERE Category = 'NcrThreshold';
";
            migrationBuilder.Sql(sql);
        }
    }
}
