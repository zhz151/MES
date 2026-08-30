using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplyTargetGroupKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupplyTargetGroupKey",
                table: "ColdRollMachineGroupConfig",
                type: "nvarchar(max)",
                nullable: true);

            // 存量回填：5060 供给方组指向 2030 需求方组（2026-08-29 方案 A 供需链显式化）
            migrationBuilder.Sql(
                "UPDATE [ColdRollMachineGroupConfig] SET [SupplyTargetGroupKey] = '2030' " +
                "WHERE [GroupKey] = '5060' AND ([SupplyTargetGroupKey] IS NULL OR [SupplyTargetGroupKey] = '')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupplyTargetGroupKey",
                table: "ColdRollMachineGroupConfig");
        }
    }
}
