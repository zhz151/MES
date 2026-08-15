using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedFactoryInspectionRequirementFromStandard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 按"标准号检验项要求"的数据复制到"工厂检验项要求"，新增 6 字段填默认值（表检/尺寸=必检，PMI检验/内窥/水下气压/端口着色=按需）
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [FactoryInspectionRequirement])
BEGIN
    INSERT INTO [FactoryInspectionRequirement]
        ([StandardNo], [ChemicalComposition],
         [PmiInspection], [SurfaceInspection], [Dimension], [Endoscopy],
         [HydrostaticTest], [UnderwaterPressure], [EddyCurrent], [UltrasonicTest], [PortColoring], [RadiographicTest],
         [HardnessRockwell], [HardnessBrinell], [HardnessVickers], [TensileRoomTemp], [TensileHighTemp], [WeldJointTensile],
         [ImpactTest], [WeldJointImpact], [FlatteningTest], [FlaringTest], [ExpandingTest], [BendTest], [WeldJointBend],
         [GrainSize], [IntergranularCorrosion], [PittingCorrosion], [FerriteContent], [Macrostructure],
         [CreatedTime], [CreatedBy], [UpdatedTime], [UpdatedBy])
    SELECT
         [StandardNo], [ChemicalComposition],
         N'按需', N'必检', N'必检', N'按需',
         [HydrostaticTest], N'按需', [EddyCurrent], [UltrasonicTest], N'按需', [RadiographicTest],
         [HardnessRockwell], [HardnessBrinell], [HardnessVickers], [TensileRoomTemp], [TensileHighTemp], [WeldJointTensile],
         [ImpactTest], [WeldJointImpact], [FlatteningTest], [FlaringTest], [ExpandingTest], [BendTest], [WeldJointBend],
         [GrainSize], [IntergranularCorrosion], [PittingCorrosion], [FerriteContent], [Macrostructure],
         SYSDATETIMEOFFSET(), N'System', SYSDATETIMEOFFSET(), N'System'
    FROM [StandardInspectionRequirement];
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 撤销：删除由本迁移复制生成的数据
            migrationBuilder.Sql(@"
DELETE FROM [FactoryInspectionRequirement] WHERE [CreatedBy] = N'System';
");
        }
    }
}
