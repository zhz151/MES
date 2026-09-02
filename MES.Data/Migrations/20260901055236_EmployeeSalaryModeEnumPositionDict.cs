using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeSalaryModeEnumPositionDict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===== 1) SalaryMode：中文→枚举名（列类型不变 nvarchar(50)，仅存量数据归一）=====
            migrationBuilder.Sql("UPDATE [Employees] SET [SalaryMode] = N'PieceIndividual' WHERE [SalaryMode] = N'个人计件';");
            migrationBuilder.Sql("UPDATE [Employees] SET [SalaryMode] = N'PieceCollective' WHERE [SalaryMode] = N'集体计件';");
            migrationBuilder.Sql("UPDATE [Employees] SET [SalaryMode] = N'PieceAttendance' WHERE [SalaryMode] = N'计件靠工';");
            migrationBuilder.Sql("UPDATE [Employees] SET [SalaryMode] = N'Hourly' WHERE [SalaryMode] = N'小时工资';");
            migrationBuilder.Sql("UPDATE [Employees] SET [SalaryMode] = N'Daily' WHERE [SalaryMode] = N'日工资';");
            migrationBuilder.Sql("UPDATE [Employees] SET [SalaryMode] = NULL WHERE [SalaryMode] = N'';");

            // ===== 2) Position：中文→英文 Key（岗位字典化，存量 14 岗位归一）=====
            migrationBuilder.Sql("UPDATE [Employees] SET [Position] = N'FinishedInspection' WHERE [Position] = N'成品检验';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Position] = N'AcidWashing' WHERE [Position] = N'酸洗';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Position] = N'HighSpeedMill' WHERE [Position] = N'高速轧机';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Position] = N'Straightening' WHERE [Position] = N'矫直';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Position] = N'Cutting' WHERE [Position] = N'切割';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Position] = N'ProductionLogistics' WHERE [Position] = N'生产后勤';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Position] = N'Grinding' WHERE [Position] = N'修磨';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Position] = N'SewageTreatment' WHERE [Position] = N'污水处理';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Position] = N'Solution' WHERE [Position] = N'固溶';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Position] = N'ColdRoll60' WHERE [Position] = N'60冷轧';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Position] = N'Office' WHERE [Position] = N'办公室';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Position] = N'MaterialWarehouse' WHERE [Position] = N'材料仓库';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Position] = N'ProductionWorkshop' WHERE [Position] = N'生产车间';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Position] = N'RollingDrawing' WHERE [Position] = N'轧拉机';");
            migrationBuilder.Sql("UPDATE [Employees] SET [Position] = NULL WHERE [Position] = N'';");

            // ===== 3) 岗位字典默认行（DictKey=PositionKey，14 行）=====
            // 真库 DictValueDefinitions 已有 9 字典行，DbInitializer 8f 段 Any() 为 true 不再补种，此处迁移补齐岗位字典
            migrationBuilder.Sql(@"
INSERT INTO [DictValueDefinitions]
    ([DictKey], [Value], [DisplayName], [DisplayOrder], [IsEnabled], [Remark], [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
VALUES
    ('PositionKey', 'FinishedInspection', '成品检验', 1, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('PositionKey', 'AcidWashing', '酸洗', 2, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('PositionKey', 'HighSpeedMill', '高速轧机', 3, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('PositionKey', 'Straightening', '矫直', 4, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('PositionKey', 'Cutting', '切割', 5, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('PositionKey', 'ProductionLogistics', '生产后勤', 6, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('PositionKey', 'Grinding', '修磨', 7, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('PositionKey', 'SewageTreatment', '污水处理', 8, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('PositionKey', 'Solution', '固溶', 9, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('PositionKey', 'ColdRoll60', '60冷轧', 10, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('PositionKey', 'Office', '办公室', 11, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('PositionKey', 'MaterialWarehouse', '材料仓库', 12, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('PositionKey', 'ProductionWorkshop', '生产车间', 13, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), ''),
    ('PositionKey', 'RollingDrawing', '轧拉机', 14, 1, NULL, SYSDATETIMEOFFSET(), '', SYSDATETIMEOFFSET(), '')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 数据不可逆恢复，Down 仅删除岗位字典行（员工字段保持英文 Key）
            migrationBuilder.Sql("DELETE FROM [DictValueDefinitions] WHERE [DictKey] = 'PositionKey';");
        }
    }
}
