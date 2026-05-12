using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixSectionOutsourceStatusInProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 清理旧中文状态值，转换为新的枚举名称
            // '在轧' → 'InProgress'（旧系统遗留状态）
            // '待回收' → 'PendingRecovery'（与当前默认值一致）
            // '已回收' → 'Recovered'
            // 其他未知值 → 'PendingRecovery'（兜底）
            migrationBuilder.Sql(@"UPDATE SectionOutsource SET Status = 'InProgress' WHERE Status = N'在轧'");
            migrationBuilder.Sql(@"UPDATE SectionOutsource SET Status = 'PendingRecovery' WHERE Status = N'待回收'");
            migrationBuilder.Sql(@"UPDATE SectionOutsource SET Status = 'Recovered' WHERE Status = N'已回收'");
            migrationBuilder.Sql(@"UPDATE SectionOutsource SET Status = 'PendingRecovery' WHERE Status NOT IN ('PendingRecovery', 'Recovered', 'InProgress')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚：将枚举名称还原为中文值
            // 注意：InProgress 原始值为 '在轧'，但回滚时旧值已丢失，统一还原为默认中文
            migrationBuilder.Sql(@"UPDATE SectionOutsource SET Status = N'在轧' WHERE Status = 'InProgress'");
            migrationBuilder.Sql(@"UPDATE SectionOutsource SET Status = N'待回收' WHERE Status = 'PendingRecovery'");
            migrationBuilder.Sql(@"UPDATE SectionOutsource SET Status = N'已回收' WHERE Status = 'Recovered'");
        }
    }
}
