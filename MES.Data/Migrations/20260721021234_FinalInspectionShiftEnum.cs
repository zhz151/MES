using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinalInspectionShiftEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===== Shift（班次）：中文→枚举名 =====
            migrationBuilder.Sql("UPDATE [FinalInspection] SET [Shift] = N'DayShift' WHERE [Shift] = N'白班';");
            migrationBuilder.Sql("UPDATE [FinalInspection] SET [Shift] = N'MiddleShift' WHERE [Shift] = N'中班';");
            migrationBuilder.Sql("UPDATE [FinalInspection] SET [Shift] = N'NightShift' WHERE [Shift] = N'夜班';");
            migrationBuilder.Sql("UPDATE [FinalInspection] SET [Shift] = NULL WHERE [Shift] = N'';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 数据不可逆恢复，Down 无操作
        }
    }
}
