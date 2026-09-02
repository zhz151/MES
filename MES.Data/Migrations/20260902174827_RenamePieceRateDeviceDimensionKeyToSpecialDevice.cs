using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenamePieceRateDeviceDimensionKeyToSpecialDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 维度等值键更名：设备号(Device) → 特殊设备号(SpecialDevice)，纯数据回写（无表结构变更）。
            migrationBuilder.Sql("""
                UPDATE [PieceRateProductionCategoryTiers]
                SET [DimensionKey] = N'SpecialDevice'
                WHERE [DimensionKey] = N'Device';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [PieceRateProductionCategoryTiers]
                SET [DimensionKey] = N'Device'
                WHERE [DimensionKey] = N'SpecialDevice';
                """);
        }
    }
}
