using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 工段 SectionName 存储值 中文 → 英文 Key（方案 B 彻底解耦）。
    /// 覆盖 9 处存储位置：ProductionRecord / SectionOutsource / PicklingInRecord /
    /// PicklingOutRecord / ProcessInspection / Workstations / SectionFlowCategoryItems 的 SectionName，
    /// ProductionBatch 的 CurrentSectionName / NextSectionName。
    /// 含别名归一：切管→OilPipeCut、脱脂→Degrease、测厚→ThicknessMeasure、外抛→OuterPolish、
    /// 内磨→InnerGrinding、探伤→Inspection、焊头/打焊头→WeldingHead、喷砂丸→SandBlasting。
    /// 未知值保留原值（ELSE 原列）。
    /// </summary>
    public partial class SectionNameChineseToEnglishKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"UPDATE ProductionRecord SET SectionName = {BuildCase("SectionName")};");
            migrationBuilder.Sql($"UPDATE SectionOutsource SET SectionName = {BuildCase("SectionName")};");
            migrationBuilder.Sql($"UPDATE PicklingInRecord SET SectionName = {BuildCase("SectionName")};");
            migrationBuilder.Sql($"UPDATE PicklingOutRecord SET SectionName = {BuildCase("SectionName")};");
            migrationBuilder.Sql($"UPDATE ProcessInspection SET SectionName = {BuildCase("SectionName")};");
            migrationBuilder.Sql($"UPDATE Workstations SET SectionName = {BuildCase("SectionName")};");
            migrationBuilder.Sql($"UPDATE SectionFlowCategoryItems SET SectionName = {BuildCase("SectionName")};");
            migrationBuilder.Sql($"UPDATE ProductionBatch SET CurrentSectionName = {BuildCase("CurrentSectionName")};");
            migrationBuilder.Sql($"UPDATE ProductionBatch SET NextSectionName = {BuildCase("NextSectionName")};");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"UPDATE ProductionRecord SET SectionName = {BuildReverseCase("SectionName")};");
            migrationBuilder.Sql($"UPDATE SectionOutsource SET SectionName = {BuildReverseCase("SectionName")};");
            migrationBuilder.Sql($"UPDATE PicklingInRecord SET SectionName = {BuildReverseCase("SectionName")};");
            migrationBuilder.Sql($"UPDATE PicklingOutRecord SET SectionName = {BuildReverseCase("SectionName")};");
            migrationBuilder.Sql($"UPDATE ProcessInspection SET SectionName = {BuildReverseCase("SectionName")};");
            migrationBuilder.Sql($"UPDATE Workstations SET SectionName = {BuildReverseCase("SectionName")};");
            migrationBuilder.Sql($"UPDATE SectionFlowCategoryItems SET SectionName = {BuildReverseCase("SectionName")};");
            migrationBuilder.Sql($"UPDATE ProductionBatch SET CurrentSectionName = {BuildReverseCase("CurrentSectionName")};");
            migrationBuilder.Sql($"UPDATE ProductionBatch SET NextSectionName = {BuildReverseCase("NextSectionName")};");
        }

        /// <summary>中文（规范 + 别名）→ 英文 Key 的 CASE 表达式</summary>
        private static string BuildCase(string column)
            => $@"CASE {column}
    WHEN N'冷轧拔' THEN 'ColdRollDraw'
    WHEN N'油管断' THEN 'OilPipeCut'
    WHEN N'切管' THEN 'OilPipeCut'
    WHEN N'去油' THEN 'Degrease'
    WHEN N'脱脂' THEN 'Degrease'
    WHEN N'乳液浸洗' THEN 'EmulsionWash'
    WHEN N'超声浸洗' THEN 'UltrasonicWash'
    WHEN N'打布' THEN 'ClothPolish'
    WHEN N'光亮退火' THEN 'BrightAnnealing'
    WHEN N'固溶' THEN 'Solution'
    WHEN N'矫直' THEN 'Straighten'
    WHEN N'断切' THEN 'Cut'
    WHEN N'测壁厚' THEN 'ThicknessMeasure'
    WHEN N'测厚' THEN 'ThicknessMeasure'
    WHEN N'酸洗' THEN 'Pickle'
    WHEN N'外抛光' THEN 'OuterPolish'
    WHEN N'外抛' THEN 'OuterPolish'
    WHEN N'内抛' THEN 'InnerPolish'
    WHEN N'内修磨' THEN 'InnerGrinding'
    WHEN N'内磨' THEN 'InnerGrinding'
    WHEN N'外点磨' THEN 'OuterSpotGrinding'
    WHEN N'喷砂' THEN 'SandBlasting'
    WHEN N'喷砂丸' THEN 'SandBlasting'
    WHEN N'喷丸' THEN 'ShotBlasting'
    WHEN N'检验' THEN 'Inspection'
    WHEN N'探伤' THEN 'Inspection'
    WHEN N'焊头' THEN 'WeldingHead'
    WHEN N'打焊头' THEN 'WeldingHead'
    WHEN N'打头' THEN 'Welding'
    WHEN N'润滑' THEN 'Lubrication'
    WHEN N'包装' THEN 'Packing'
    WHEN N'入库' THEN 'Warehouse'
    WHEN N'备用1' THEN 'Extra1'
    WHEN N'备用2' THEN 'Extra2'
    ELSE {column} END";

        /// <summary>英文 Key → 规范中文 的 CASE 表达式（Down 反向；别名不可逆，仅恢复规范中文）</summary>
        private static string BuildReverseCase(string column)
            => $@"CASE {column}
    WHEN 'ColdRollDraw' THEN N'冷轧拔'
    WHEN 'OilPipeCut' THEN N'油管断'
    WHEN 'Degrease' THEN N'去油'
    WHEN 'EmulsionWash' THEN N'乳液浸洗'
    WHEN 'UltrasonicWash' THEN N'超声浸洗'
    WHEN 'ClothPolish' THEN N'打布'
    WHEN 'BrightAnnealing' THEN N'光亮退火'
    WHEN 'Solution' THEN N'固溶'
    WHEN 'Straighten' THEN N'矫直'
    WHEN 'Cut' THEN N'断切'
    WHEN 'ThicknessMeasure' THEN N'测壁厚'
    WHEN 'Pickle' THEN N'酸洗'
    WHEN 'OuterPolish' THEN N'外抛光'
    WHEN 'InnerPolish' THEN N'内抛'
    WHEN 'InnerGrinding' THEN N'内修磨'
    WHEN 'OuterSpotGrinding' THEN N'外点磨'
    WHEN 'SandBlasting' THEN N'喷砂'
    WHEN 'ShotBlasting' THEN N'喷丸'
    WHEN 'Inspection' THEN N'检验'
    WHEN 'WeldingHead' THEN N'焊头'
    WHEN 'Welding' THEN N'打头'
    WHEN 'Lubrication' THEN N'润滑'
    WHEN 'Packing' THEN N'包装'
    WHEN 'Warehouse' THEN N'入库'
    WHEN 'Extra1' THEN N'备用1'
    WHEN 'Extra2' THEN N'备用2'
    ELSE {column} END";
    }
}
