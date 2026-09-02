using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPieceRateDimensionKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DimensionKey",
                table: "PieceRateStandards",
                type: "nvarchar(max)",
                nullable: true);

            // ===== 存量拆解（2026-09-02 维度因子模型）：旧组合行按行语义分类标记，价格 100% 保留 =====
            // 判定依据：真库 5032 启用行全为旧组合行（DimensionKey 空）。
            // 组合维全空 + 无特殊 → Base 基准行（Solution/ColdDraw 等固定单价基准）；
            // 组合维全空 + 仅特殊牌号/状态 → SpecialGrade/SpecialState 特殊倍数行；
            // 有组合维（逐格报价，TotalRatio 全库 1217 种不可无损拆解）→ OnePrice 例外价行保留原绝对价。
            // 各分支顺序执行，末尾兜底所有未标记行转 OnePrice。
            migrationBuilder.Sql("""
                -- 1) Base 基准行：组合维全空 + 无特殊
                UPDATE PieceRateStandards SET DimensionKey = 'Base', TotalRatio = 1, UnitPrice = BasePrice,
                    SpecialGrade = NULL, SpecialGradeRatio = 1, SpecialState = NULL, SpecialStateRatio = 1
                WHERE DimensionKey IS NULL
                  AND ISNULL(OuterDiameterRangeText,'')='' AND ISNULL(WallThicknessRangeText,'')=''
                  AND ISNULL(LengthRangeText,'')='' AND ISNULL(CutRateRangeText,'')=''
                  AND ISNULL(FixedLengthCountRangeText,'')='' AND ISNULL(ProductStatus,'')=''
                  AND ISNULL(DeviceCategory,'')='' AND ISNULL(SpecialGrade,'')='' AND ISNULL(SpecialState,'')='';

                -- 2) 特殊牌号倍数行：组合维全空 + 仅特殊牌号
                UPDATE PieceRateStandards SET DimensionKey = 'SpecialGrade', SpecialState = NULL, SpecialStateRatio = 1,
                    TotalRatio = 1, UnitPrice = BasePrice * SpecialGradeRatio
                WHERE DimensionKey IS NULL
                  AND ISNULL(OuterDiameterRangeText,'')='' AND ISNULL(WallThicknessRangeText,'')=''
                  AND ISNULL(LengthRangeText,'')='' AND ISNULL(CutRateRangeText,'')=''
                  AND ISNULL(FixedLengthCountRangeText,'')='' AND ISNULL(ProductStatus,'')=''
                  AND ISNULL(DeviceCategory,'')='' AND ISNULL(SpecialGrade,'')<>'' AND ISNULL(SpecialState,'')='';

                -- 3) 特殊制造状态倍数行：组合维全空 + 仅特殊状态
                UPDATE PieceRateStandards SET DimensionKey = 'SpecialState', SpecialGrade = NULL, SpecialGradeRatio = 1,
                    TotalRatio = 1, UnitPrice = BasePrice * SpecialStateRatio
                WHERE DimensionKey IS NULL
                  AND ISNULL(OuterDiameterRangeText,'')='' AND ISNULL(WallThicknessRangeText,'')=''
                  AND ISNULL(LengthRangeText,'')='' AND ISNULL(CutRateRangeText,'')=''
                  AND ISNULL(FixedLengthCountRangeText,'')='' AND ISNULL(ProductStatus,'')=''
                  AND ISNULL(DeviceCategory,'')='' AND ISNULL(SpecialGrade,'')='' AND ISNULL(SpecialState,'')<>'';

                -- 4) 组合维全空 + 双特殊（存量无此类，保险兜底转 OnePrice 保留原价）
                UPDATE PieceRateStandards SET DimensionKey = 'OnePrice',
                    UnitPrice = CASE WHEN UnitPrice > 0 THEN UnitPrice ELSE BasePrice * TotalRatio * SpecialGradeRatio * SpecialStateRatio END
                WHERE DimensionKey IS NULL
                  AND ISNULL(OuterDiameterRangeText,'')='' AND ISNULL(WallThicknessRangeText,'')=''
                  AND ISNULL(LengthRangeText,'')='' AND ISNULL(CutRateRangeText,'')=''
                  AND ISNULL(FixedLengthCountRangeText,'')='' AND ISNULL(ProductStatus,'')=''
                  AND ISNULL(DeviceCategory,'')='' AND ISNULL(SpecialGrade,'')<>'' AND ISNULL(SpecialState,'')<>'';

                -- 5) 兜底：其余（含组合维的逐格报价行）→ OnePrice 保留原绝对价
                UPDATE PieceRateStandards SET DimensionKey = 'OnePrice',
                    TotalRatio = CASE WHEN ISNULL(TotalRatio,0) = 0 THEN 1 ELSE TotalRatio END,
                    UnitPrice = CASE WHEN UnitPrice > 0 THEN UnitPrice ELSE BasePrice * TotalRatio * SpecialGradeRatio * SpecialStateRatio END
                WHERE DimensionKey IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DimensionKey",
                table: "PieceRateStandards");
        }
    }
}
