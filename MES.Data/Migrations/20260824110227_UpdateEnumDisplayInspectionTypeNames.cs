using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEnumDisplayInspectionTypeNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 统一 InspectionType 中文口径为「预检/终检」（批次模块），与 EnumHelper.Register 保持一致。
            // GetDisplayName 优先配置表 _displayOverrides，前端 options-map 亦直读配置表，故此处同步存量行。
            migrationBuilder.Sql("""
                UPDATE [EnumDisplayDefinitions]
                SET [DisplayName] = N'预检'
                WHERE [EnumKey] = N'InspectionType' AND [Value] = N'PreInspection';
                UPDATE [EnumDisplayDefinitions]
                SET [DisplayName] = N'终检'
                WHERE [EnumKey] = N'InspectionType' AND [Value] = N'FormalInspection';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [EnumDisplayDefinitions]
                SET [DisplayName] = N'预成检'
                WHERE [EnumKey] = N'InspectionType' AND [Value] = N'PreInspection';
                UPDATE [EnumDisplayDefinitions]
                SET [DisplayName] = N'正式成检'
                WHERE [EnumKey] = N'InspectionType' AND [Value] = N'FormalInspection';
                """);
        }
    }
}
