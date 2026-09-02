using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPieceRateBaseRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 为存量计件类别补齐 Base 基准行（BasePrice = 该类组合行众数），使维度因子模型立即可用：
            // 存量组合行已转 OnePrice 保留原价（优先命中），Base 行仅用于「新增维度档因子连乘」与固定单价匹配。
            // 幂等：已存在 Base 行的类别跳过；CreatedBy 打标便于 Down 回滚。
            migrationBuilder.Sql("""
                INSERT INTO PieceRateStandards
                    (SectionName, GroupName, DimensionKey, BasePrice, TotalRatio, UnitPrice, Unit, PieceRateType,
                     IsActive, EffectiveDate, CreatedTime, CreatedBy, UpdatedTime, UpdatedBy,
                     OuterDiameterRatio, WallThicknessRatio, LengthRatio, CutRateRatio, FixedLengthCountRatio,
                     ProductRatio, SpecialGradeRatio, SpecialStateRatio)
                SELECT src.SectionName,
                       COALESCE((SELECT TOP 1 g.GroupName FROM PieceRateStandards g
                                 WHERE g.SectionName = src.SectionName AND g.GroupName IS NOT NULL), src.SectionName),
                       'Base', src.BasePrice, 1, src.BasePrice, src.Unit, src.PieceRateType,
                       1, NULL, GETUTCDATE(), 'migration-base-20260902', GETUTCDATE(), 'migration-base-20260902',
                       1, 1, 1, 1, 1, 1, 1, 1
                FROM (
                    SELECT SectionName, Unit, PieceRateType, BasePrice,
                           ROW_NUMBER() OVER (PARTITION BY SectionName, Unit, PieceRateType
                                              ORDER BY COUNT(*) DESC, BasePrice) AS rn
                    FROM PieceRateStandards
                    WHERE DimensionKey = 'OnePrice'
                    GROUP BY SectionName, Unit, PieceRateType, BasePrice
                ) src
                WHERE src.rn = 1
                  AND NOT EXISTS (
                      SELECT 1 FROM PieceRateStandards b
                      WHERE b.SectionName = src.SectionName AND b.DimensionKey = 'Base'
                        AND b.IsActive = 1 AND b.Unit = src.Unit AND b.PieceRateType = src.PieceRateType
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM PieceRateStandards WHERE CreatedBy = 'migration-base-20260902';
                """);
        }
    }
}
