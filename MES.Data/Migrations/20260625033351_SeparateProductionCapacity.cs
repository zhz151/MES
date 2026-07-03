using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeparateProductionCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyProductionCapacities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DailyCapacity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyProductionCapacities", x => x.Id);
                });

            // 从 ConfigParameter 迁移 ProductionCapacity 数据到新表
            var now = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffffff +00:00");
            var sysUser = "system";
            migrationBuilder.Sql($@"
                INSERT INTO [DailyProductionCapacities] ([ProcessName], [DailyCapacity], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                SELECT [ParamKey], [ParamValue], [Remark], '{now}', '{sysUser}', '{now}', '{sysUser}'
                FROM [ConfigParameters]
                WHERE [Category] = 'ProductionCapacity'
            ");

            // 删除 ConfigParameter 中旧的 ProductionCapacity 记录
            migrationBuilder.Sql("DELETE FROM [ConfigParameters] WHERE [Category] = 'ProductionCapacity'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚：将 DailyProductionCapacities 数据重新插入 ConfigParameter
            var now = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffffff +00:00");
            var sysUser = "system";
            migrationBuilder.Sql($@"
                INSERT INTO [ConfigParameters] ([Category], [CategoryDisplay], [Context], [ParamKey], [ParamValue], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
                SELECT 'ProductionCapacity', '排程-订单总览页面', '排程', [ProcessName], [DailyCapacity], [Remark], '{now}', '{sysUser}', '{now}', '{sysUser}'
                FROM [DailyProductionCapacities]
            ");

            migrationBuilder.DropTable(
                name: "DailyProductionCapacities");
        }
    }
}
