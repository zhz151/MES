using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeSalaryFieldsAndPositionCategoryDict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AttendanceCoefficient",
                table: "Employees",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DailyWage",
                table: "Employees",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HourlyWage",
                table: "Employees",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyWage",
                table: "Employees",
                type: "decimal(18,2)",
                nullable: true);

            // ===== 5) 靠工系数存量初始化：靠工计件员工默认 1.0（其余模式 NULL）=====
            migrationBuilder.Sql("UPDATE [Employees] SET [AttendanceCoefficient] = 1.0 WHERE [AttendanceCoefficient] IS NULL AND [SalaryMode] = N'PieceAttendance';");

            // ===== 6) Department（岗位类别）：中文→英文 Key（岗位类别字典化，存量 4 值归一）=====
            migrationBuilder.Sql("UPDATE [Employees] SET [Department] = N'Workshop' WHERE [Department] = N'车间生产';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Department] = N'QualityInspection' WHERE [Department] = N'质检';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Department] = N'ProductionLogistics' WHERE [Department] = N'生产后勤';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Department] = N'Technology' WHERE [Department] = N'生技部';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Department] = NULL WHERE [Department] = N'';");

            // ===== 7) 岗位类别字典默认行（DictKey=PositionCategoryKey，4 行）=====
            // 真库 DictValueDefinitions 已有 9+14 字典行，DbInitializer Any() 为 true 不再补种，此处迁移补齐岗位类别字典
            migrationBuilder.Sql(@"
INSERT INTO [DictValueDefinitions]
    ([DictKey], [Value], [DisplayName], [DisplayOrder], [IsEnabled], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
VALUES
    ('PositionCategoryKey', 'Workshop', '车间生产', 1, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('PositionCategoryKey', 'QualityInspection', '质检', 2, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('PositionCategoryKey', 'ProductionLogistics', '生产后勤', 3, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('PositionCategoryKey', 'Technology', '生技部', 4, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), '')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 数据不可逆恢复（员工字段保持英文 Key），Down 仅删除岗位类别字典行与新增列
            migrationBuilder.Sql("DELETE FROM [DictValueDefinitions] WHERE [DictKey] = 'PositionCategoryKey';");

            migrationBuilder.DropColumn(
                name: "AttendanceCoefficient",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "DailyWage",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "HourlyWage",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "MonthlyWage",
                table: "Employees");
        }
    }
}
