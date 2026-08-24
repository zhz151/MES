using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeInspectionFlagsToBoolean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 旧列存 InspectionItem 枚举名逗号串（项目资质版），现改布尔开关：
            // 非空存量无法直接转 bit，清空为 NULL（语义 = 否/未配置）
            migrationBuilder.Sql("UPDATE Employees SET ProcessInspectionItems = NULL WHERE ProcessInspectionItems IS NOT NULL");
            migrationBuilder.Sql("UPDATE Employees SET MaterialReceiveCheckItems = NULL WHERE MaterialReceiveCheckItems IS NOT NULL");

            migrationBuilder.AlterColumn<bool>(
                name: "ProcessInspectionItems",
                table: "Employees",
                type: "bit",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "MaterialReceiveCheckItems",
                table: "Employees",
                type: "bit",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ProcessInspectionItems",
                table: "Employees",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MaterialReceiveCheckItems",
                table: "Employees",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);
        }
    }
}
