using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductRequirementInspectionStageEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UnderwaterPressure",
                table: "ProductRequirement",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FinalOnly",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "UltrasonicTest",
                table: "ProductRequirement",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FinalOnly",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "SurfaceInspection",
                table: "ProductRequirement",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FinalOnly",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "RadiographicTest",
                table: "ProductRequirement",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FinalOnly",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "PortColoring",
                table: "ProductRequirement",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FinalOnly",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "PmiInspection",
                table: "ProductRequirement",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FinalOnly",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "HydrostaticTest",
                table: "ProductRequirement",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FinalOnly",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Endoscopy",
                table: "ProductRequirement",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FinalOnly",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "EddyCurrent",
                table: "ProductRequirement",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FinalOnly",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Dimension",
                table: "ProductRequirement",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FinalOnly",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            // 存量 bit 值转换：bit→nvarchar 隐式转 '1'/'0'，按设计映射为枚举字符串（true→FinalOnly「终」/ false→None「-」）
            migrationBuilder.Sql("""
                UPDATE "ProductRequirement"
                SET "PmiInspection" = CASE WHEN "PmiInspection" = '1' THEN 'FinalOnly' WHEN "PmiInspection" = '0' THEN 'None' ELSE "PmiInspection" END,
                    "SurfaceInspection" = CASE WHEN "SurfaceInspection" = '1' THEN 'FinalOnly' WHEN "SurfaceInspection" = '0' THEN 'None' ELSE "SurfaceInspection" END,
                    "Dimension" = CASE WHEN "Dimension" = '1' THEN 'FinalOnly' WHEN "Dimension" = '0' THEN 'None' ELSE "Dimension" END,
                    "Endoscopy" = CASE WHEN "Endoscopy" = '1' THEN 'FinalOnly' WHEN "Endoscopy" = '0' THEN 'None' ELSE "Endoscopy" END,
                    "HydrostaticTest" = CASE WHEN "HydrostaticTest" = '1' THEN 'FinalOnly' WHEN "HydrostaticTest" = '0' THEN 'None' ELSE "HydrostaticTest" END,
                    "UnderwaterPressure" = CASE WHEN "UnderwaterPressure" = '1' THEN 'FinalOnly' WHEN "UnderwaterPressure" = '0' THEN 'None' ELSE "UnderwaterPressure" END,
                    "EddyCurrent" = CASE WHEN "EddyCurrent" = '1' THEN 'FinalOnly' WHEN "EddyCurrent" = '0' THEN 'None' ELSE "EddyCurrent" END,
                    "UltrasonicTest" = CASE WHEN "UltrasonicTest" = '1' THEN 'FinalOnly' WHEN "UltrasonicTest" = '0' THEN 'None' ELSE "UltrasonicTest" END,
                    "PortColoring" = CASE WHEN "PortColoring" = '1' THEN 'FinalOnly' WHEN "PortColoring" = '0' THEN 'None' ELSE "PortColoring" END,
                    "RadiographicTest" = CASE WHEN "RadiographicTest" = '1' THEN 'FinalOnly' WHEN "RadiographicTest" = '0' THEN 'None' ELSE "RadiographicTest" END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "UnderwaterPressure",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "FinalOnly");

            migrationBuilder.AlterColumn<bool>(
                name: "UltrasonicTest",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "FinalOnly");

            migrationBuilder.AlterColumn<bool>(
                name: "SurfaceInspection",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "FinalOnly");

            migrationBuilder.AlterColumn<bool>(
                name: "RadiographicTest",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "FinalOnly");

            migrationBuilder.AlterColumn<bool>(
                name: "PortColoring",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "FinalOnly");

            migrationBuilder.AlterColumn<bool>(
                name: "PmiInspection",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "FinalOnly");

            migrationBuilder.AlterColumn<bool>(
                name: "HydrostaticTest",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "FinalOnly");

            migrationBuilder.AlterColumn<bool>(
                name: "Endoscopy",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "FinalOnly");

            migrationBuilder.AlterColumn<bool>(
                name: "EddyCurrent",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "FinalOnly");

            migrationBuilder.AlterColumn<bool>(
                name: "Dimension",
                table: "ProductRequirement",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "FinalOnly");
        }
    }
}
