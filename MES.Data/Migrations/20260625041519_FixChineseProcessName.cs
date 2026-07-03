using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixChineseProcessName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 将 DailyProductionCapacities 表中英文 ProcessName 改为中文
            migrationBuilder.Sql("UPDATE [DailyProductionCapacities] SET [ProcessName] = '荒管抛光' WHERE [ProcessName] = 'Polish'");
            migrationBuilder.Sql("UPDATE [DailyProductionCapacities] SET [ProcessName] = '50,60轧机' WHERE [ProcessName] = 'Mill50_60'");
            migrationBuilder.Sql("UPDATE [DailyProductionCapacities] SET [ProcessName] = '20,30轧机' WHERE [ProcessName] = 'Mill20_30'");
            migrationBuilder.Sql("UPDATE [DailyProductionCapacities] SET [ProcessName] = '三辊轧机' WHERE [ProcessName] = 'ThreeRoll'");
            migrationBuilder.Sql("UPDATE [DailyProductionCapacities] SET [ProcessName] = '拉机' WHERE [ProcessName] = 'DrawBench'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚：中文改回英文
            migrationBuilder.Sql("UPDATE [DailyProductionCapacities] SET [ProcessName] = 'Polish' WHERE [ProcessName] = '荒管抛光'");
            migrationBuilder.Sql("UPDATE [DailyProductionCapacities] SET [ProcessName] = 'Mill50_60' WHERE [ProcessName] = '50,60轧机'");
            migrationBuilder.Sql("UPDATE [DailyProductionCapacities] SET [ProcessName] = 'Mill20_30' WHERE [ProcessName] = '20,30轧机'");
            migrationBuilder.Sql("UPDATE [DailyProductionCapacities] SET [ProcessName] = 'ThreeRoll' WHERE [ProcessName] = '三辊轧机'");
            migrationBuilder.Sql("UPDATE [DailyProductionCapacities] SET [ProcessName] = 'DrawBench' WHERE [ProcessName] = '拉机'");
        }
    }
}
