using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class PieceRateGradeToPlantGrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 特殊牌号语义迁移：钢种类别 Key → 工厂牌号 PlantGrade（牌号对照表 StandardGradeMapping 基准）。
            // Alloy2520→31000（06Cr25Ni20）、Alloy347→34700（06Cr18Ni11Nb）；
            // DuplexSteel 双相钢按牌号对照表工厂牌号拆行：22051/22052/25073（继承原倍数）。
            migrationBuilder.Sql("""
                UPDATE [PieceRateStandards] SET [SpecialGrade] = '31000' WHERE [SpecialGrade] = 'Alloy2520';
                UPDATE [PieceRateStandards] SET [SpecialGrade] = '34700' WHERE [SpecialGrade] = 'Alloy347';

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
                    [PieceRateType], [EffectiveDate], [IsActive], [Remark],
                    [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy]
                FROM [PieceRateStandards] WHERE [SpecialGrade] = 'DuplexSteel'
                UNION ALL
                SELECT [GroupName], [SectionName], [DeviceCategory],
                    [OuterDiameterRangeText], [OuterDiameterMin], [OuterDiameterMax], [OuterDiameterRatio],
                    [WallThicknessRangeText], [WallThicknessMin], [WallThicknessMax], [WallThicknessRatio],
                    [LengthRangeText], [LengthMin], [LengthMax], [LengthRatio],
                    [CutRateRangeText], [CutRateMin], [CutRateMax], [CutRateRatio],
                    [FixedLengthCountRangeText], [FixedLengthCountMin], [FixedLengthCountMax], [FixedLengthCountRatio],
                    [ProductStatus], [ProductRatio], [TotalRatio], [BasePrice], [UnitPrice], [Unit],
                    '22052', [SpecialGradeRatio], [SpecialState], [SpecialStateRatio],
                    [PieceRateType], [EffectiveDate], [IsActive], [Remark],
                    [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy]
                FROM [PieceRateStandards] WHERE [SpecialGrade] = 'DuplexSteel'
                UNION ALL
                SELECT [GroupName], [SectionName], [DeviceCategory],
                    [OuterDiameterRangeText], [OuterDiameterMin], [OuterDiameterMax], [OuterDiameterRatio],
                    [WallThicknessRangeText], [WallThicknessMin], [WallThicknessMax], [WallThicknessRatio],
                    [LengthRangeText], [LengthMin], [LengthMax], [LengthRatio],
                    [CutRateRangeText], [CutRateMin], [CutRateMax], [CutRateRatio],
                    [FixedLengthCountRangeText], [FixedLengthCountMin], [FixedLengthCountMax], [FixedLengthCountRatio],
                    [ProductStatus], [ProductRatio], [TotalRatio], [BasePrice], [UnitPrice], [Unit],
                    '25073', [SpecialGradeRatio], [SpecialState], [SpecialStateRatio],
                    [PieceRateType], [EffectiveDate], [IsActive], [Remark],
                    [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy]
                FROM [PieceRateStandards] WHERE [SpecialGrade] = 'DuplexSteel';

                DELETE FROM [PieceRateStandards] WHERE [SpecialGrade] = 'DuplexSteel';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚：22051/22052/25073 三行合并回 DuplexSteel（取 22051 为代表，三行倍数同源），再还原 31000/34700。
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
                    'DuplexSteel', [SpecialGradeRatio], [SpecialState], [SpecialStateRatio],
                    [PieceRateType], [EffectiveDate], [IsActive], [Remark],
                    [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy]
                FROM [PieceRateStandards] WHERE [SpecialGrade] = '22051';

                DELETE FROM [PieceRateStandards] WHERE [SpecialGrade] IN ('22051','22052','25073');
                UPDATE [PieceRateStandards] SET [SpecialGrade] = 'Alloy2520' WHERE [SpecialGrade] = '31000';
                UPDATE [PieceRateStandards] SET [SpecialGrade] = 'Alloy347' WHERE [SpecialGrade] = '34700';
                """);
        }
    }
}
