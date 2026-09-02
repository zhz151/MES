using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 计件标准维度体系重构（2026-09-02）：
    /// 1) MachineType→DeviceCategory（机型改名设备类别，值不变）
    /// 2) IsFinishedProduct bool?→ProductStatus nvarchar(20)（是否成品改产类三档：true→Finished、false→InProgress）
    /// 3) FinishedProductRatio→ProductRatio（产类比值）
    /// 4) Category 列删除（真库 0 使用）
    /// 5) MaterialCategory→SpecialGrade + MaterialRatio→SpecialGradeRatio（材料类别改特殊牌号）
    /// 6) 新增 SpecialState/SpecialStateRatio（特殊制造状态）：光亮管存量行迁入 Bright×1.35
    /// 7) 删除 老厂/焊管 材料行（Key 已删，业务废弃）
    /// </summary>
    public partial class PieceRateStandardDimensionRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) 列改名（sp_rename 保留数据）
            migrationBuilder.RenameColumn(
                name: "MachineType",
                table: "PieceRateStandards",
                newName: "DeviceCategory");

            migrationBuilder.RenameColumn(
                name: "FinishedProductRatio",
                table: "PieceRateStandards",
                newName: "ProductRatio");

            migrationBuilder.RenameColumn(
                name: "MaterialCategory",
                table: "PieceRateStandards",
                newName: "SpecialGrade");

            migrationBuilder.RenameColumn(
                name: "MaterialRatio",
                table: "PieceRateStandards",
                newName: "SpecialGradeRatio");

            // 2) IsFinishedProduct bool? → ProductStatus nvarchar(20)（先加列回填，再删旧列）
            migrationBuilder.AddColumn<string>(
                name: "ProductStatus",
                table: "PieceRateStandards",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql(
                @"UPDATE [PieceRateStandards]
SET [ProductStatus] = CASE WHEN [IsFinishedProduct] = 1 THEN 'Finished' WHEN [IsFinishedProduct] = 0 THEN 'InProgress' ELSE NULL END");

            migrationBuilder.DropColumn(
                name: "IsFinishedProduct",
                table: "PieceRateStandards");

            // 3) 新增特殊制造状态列（状态倍数默认 1）
            migrationBuilder.AddColumn<string>(
                name: "SpecialState",
                table: "PieceRateStandards",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecialStateRatio",
                table: "PieceRateStandards",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 1m);

            // 4) 删除类别 Category 列（真库 0 使用）
            migrationBuilder.DropColumn(
                name: "Category",
                table: "PieceRateStandards");

            // 5) 数据迁移（存量 MaterialCategory 存英文 Key）：
            //    a) 原「光亮管」材料行(BrightTube) → 特殊制造状态 Bright ×1.35（牌号置空，避免牌号/状态双特殊）
            migrationBuilder.Sql(
                @"UPDATE [PieceRateStandards]
SET [SpecialState] = 'Bright', [SpecialStateRatio] = 1.35, [SpecialGrade] = NULL
WHERE [SpecialGrade] = 'BrightTube'");

            //    b) 删除 老厂(LaoChang)/焊管(WeldedPipe) 材料行（PieceRateGradeKeys 已删，业务废弃）
            migrationBuilder.Sql(
                @"DELETE FROM [PieceRateStandards] WHERE [SpecialGrade] IN ('LaoChang', 'WeldedPipe')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 数据回迁：特殊制造状态 Bright → 光亮管特殊牌号（倍数回写）
            migrationBuilder.Sql(
                @"UPDATE [PieceRateStandards]
SET [SpecialGrade] = 'BrightTube', [SpecialGradeRatio] = [SpecialStateRatio]
WHERE [SpecialState] = 'Bright'");

            migrationBuilder.DropColumn(
                name: "SpecialStateRatio",
                table: "PieceRateStandards");

            migrationBuilder.DropColumn(
                name: "SpecialState",
                table: "PieceRateStandards");

            // 产类回迁：ProductStatus → IsFinishedProduct bool（先加列回填，再删旧列）
            migrationBuilder.AddColumn<bool>(
                name: "IsFinishedProduct",
                table: "PieceRateStandards",
                type: "bit",
                nullable: true);

            migrationBuilder.Sql(
                @"UPDATE [PieceRateStandards]
SET [IsFinishedProduct] = CASE [ProductStatus] WHEN 'Finished' THEN 1 WHEN 'InProgress' THEN 0 ELSE NULL END");

            migrationBuilder.DropColumn(
                name: "ProductStatus",
                table: "PieceRateStandards");

            // 恢复 Category 空列
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "PieceRateStandards",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "SpecialGradeRatio",
                table: "PieceRateStandards",
                newName: "MaterialRatio");

            migrationBuilder.RenameColumn(
                name: "SpecialGrade",
                table: "PieceRateStandards",
                newName: "MaterialCategory");

            migrationBuilder.RenameColumn(
                name: "ProductRatio",
                table: "PieceRateStandards",
                newName: "FinishedProductRatio");

            migrationBuilder.RenameColumn(
                name: "DeviceCategory",
                table: "PieceRateStandards",
                newName: "MachineType");
        }
    }
}
