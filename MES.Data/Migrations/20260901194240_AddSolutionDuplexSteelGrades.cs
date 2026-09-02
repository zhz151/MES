using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSolutionDuplexSteelGrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 固溶工段补配双相钢特殊牌号行（22051/22052/25073 ×1.5）。
            // 固溶生产记录含双相钢（22051/22052/25073），原配置仅 31000/34700 特殊行漏配双相钢，
            // 双相钢件按普通 17 元结算。补配后固溶双相钢 ×1.5 = 25.50，与 31000/34700 一致。
            // 字段复制自固溶 31000 行（BasePrice=17/TotalRatio=1/SpecialGradeRatio=1.5）。
            migrationBuilder.Sql("""
                INSERT INTO [PieceRateStandards] (
                    [GroupName], [SectionName], [DeviceCategory],
                    [OuterDiameterRangeText], [OuterDiameterMin], [OuterDiameterMax], [OuterDiameterRatio],
                    [WallThicknessRangeText], [WallThicknessMin], [WallThicknessMax], [WallThicknessRatio],
                    [LengthRangeText], [LengthMin], [LengthMax], [LengthRatio],
                    [CutRateRangeText], [CutRateMin], [CutRateMax], [CutRateRatio],
                    [FixedLengthCountRangeText], [FixedLengthCountMin], [FixedLengthCountMax], [FixedLengthCountRatio],
                    [ProductStatus], [ProductRatio], [TotalRatio], [BasePrice], [UnitPrice], [Unit],
                    [SpecialGrade], [SpecialGradeRatio], [SpecialState], [SpecialStateRatio],
                    [PieceRateType], [EffectiveDate], [IsActive], [Remark],
                    [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy]
                )
                SELECT [GroupName], [SectionName], [DeviceCategory],
                    [OuterDiameterRangeText], [OuterDiameterMin], [OuterDiameterMax], [OuterDiameterRatio],
                    [WallThicknessRangeText], [WallThicknessMin], [WallThicknessMax], [WallThicknessRatio],
                    [LengthRangeText], [LengthMin], [LengthMax], [LengthRatio],
                    [CutRateRangeText], [CutRateMin], [CutRateMax], [CutRateRatio],
                    [FixedLengthCountRangeText], [FixedLengthCountMin], [FixedLengthCountMax], [FixedLengthCountRatio],
                    [ProductStatus], [ProductRatio], [TotalRatio], [BasePrice], [UnitPrice], [Unit],
                    '22051', [SpecialGradeRatio], [SpecialState], [SpecialStateRatio],
                    [PieceRateType], [EffectiveDate], [IsActive], '双相钢1.5倍',
                    [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy]
                FROM [PieceRateStandards] WHERE [SectionName] = 'Solution' AND [SpecialGrade] = '31000'
                UNION ALL
                SELECT [GroupName], [SectionName], [DeviceCategory],
                    [OuterDiameterRangeText], [OuterDiameterMin], [OuterDiameterMax], [OuterDiameterRatio],
                    [WallThicknessRangeText], [WallThicknessMin], [WallThicknessMax], [WallThicknessRatio],
                    [LengthRangeText], [LengthMin], [LengthMax], [LengthRatio],
                    [CutRateRangeText], [CutRateMin], [CutRateMax], [CutRateRatio],
                    [FixedLengthCountRangeText], [FixedLengthCountMin], [FixedLengthCountMax], [FixedLengthCountRatio],
                    [ProductStatus], [ProductRatio], [TotalRatio], [BasePrice], [UnitPrice], [Unit],
                    '22052', [SpecialGradeRatio], [SpecialState], [SpecialStateRatio],
                    [PieceRateType], [EffectiveDate], [IsActive], '双相钢1.5倍',
                    [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy]
                FROM [PieceRateStandards] WHERE [SectionName] = 'Solution' AND [SpecialGrade] = '31000'
                UNION ALL
                SELECT [GroupName], [SectionName], [DeviceCategory],
                    [OuterDiameterRangeText], [OuterDiameterMin], [OuterDiameterMax], [OuterDiameterRatio],
                    [WallThicknessRangeText], [WallThicknessMin], [WallThicknessMax], [WallThicknessRatio],
                    [LengthRangeText], [LengthMin], [LengthMax], [LengthRatio],
                    [CutRateRangeText], [CutRateMin], [CutRateMax], [CutRateRatio],
                    [FixedLengthCountRangeText], [FixedLengthCountMin], [FixedLengthCountMax], [FixedLengthCountRatio],
                    [ProductStatus], [ProductRatio], [TotalRatio], [BasePrice], [UnitPrice], [Unit],
                    '25073', [SpecialGradeRatio], [SpecialState], [SpecialStateRatio],
                    [PieceRateType], [EffectiveDate], [IsActive], '双相钢1.5倍',
                    [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy]
                FROM [PieceRateStandards] WHERE [SectionName] = 'Solution' AND [SpecialGrade] = '31000';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚：删除固溶双相钢三行
            migrationBuilder.Sql("""
                DELETE FROM [PieceRateStandards]
                WHERE [SectionName] = 'Solution' AND [SpecialGrade] IN ('22051','22052','25073');
                """);
        }
    }
}
