using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixProductionRecordAndSectionOutsourceSequenceNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ========== 批量修正 ProductionRecord.SequenceNumber ==========
            // 根据 ProcessGroup 的工段顺序字段重算正确的值
            migrationBuilder.Sql(@"
UPDATE pr
SET pr.SequenceNumber =
    CASE pr.SectionName
        WHEN N'冷轧拔' THEN ISNULL(pg.ColdRollDraw, 0)
        WHEN N'油管断' THEN ISNULL(pg.OilPipeCut, 0)
        WHEN N'去油'   THEN ISNULL(pg.Degrease, 0)
        WHEN N'固溶'   THEN ISNULL(pg.Solution, 0)
        WHEN N'矫直'   THEN ISNULL(pg.Straighten, 0)
        WHEN N'断切'   THEN ISNULL(pg.Cut, 0)
        WHEN N'测壁厚' THEN ISNULL(pg.ThicknessMeasure, 0)
        WHEN N'酸洗'   THEN ISNULL(pg.Pickle, 0)
        WHEN N'外抛光' THEN ISNULL(pg.OuterPolish, 0)
        WHEN N'内修磨' THEN ISNULL(pg.InnerGrinding, 0)
        WHEN N'外点磨' THEN ISNULL(pg.OuterSpotGrinding, 0)
        WHEN N'检验'   THEN ISNULL(pg.Inspection, 0)
        WHEN N'打焊头' THEN ISNULL(pg.WeldingHead, 0)
        WHEN N'润滑'   THEN ISNULL(pg.Lubrication, 0)
        WHEN N'入库'   THEN ISNULL(pg.Warehouse, 0)
        ELSE 0
    END
FROM [ProductionRecord] pr
INNER JOIN [ProcessGroup] pg ON pr.ProcessGroupId = pg.Id;
");

            // ========== 批量修正 SectionOutsource.SequenceNumber ==========
            migrationBuilder.Sql(@"
UPDATE so
SET so.SequenceNumber =
    CASE so.SectionName
        WHEN N'冷轧拔' THEN ISNULL(pg.ColdRollDraw, 0)
        WHEN N'油管断' THEN ISNULL(pg.OilPipeCut, 0)
        WHEN N'去油'   THEN ISNULL(pg.Degrease, 0)
        WHEN N'固溶'   THEN ISNULL(pg.Solution, 0)
        WHEN N'矫直'   THEN ISNULL(pg.Straighten, 0)
        WHEN N'断切'   THEN ISNULL(pg.Cut, 0)
        WHEN N'测壁厚' THEN ISNULL(pg.ThicknessMeasure, 0)
        WHEN N'酸洗'   THEN ISNULL(pg.Pickle, 0)
        WHEN N'外抛光' THEN ISNULL(pg.OuterPolish, 0)
        WHEN N'内修磨' THEN ISNULL(pg.InnerGrinding, 0)
        WHEN N'外点磨' THEN ISNULL(pg.OuterSpotGrinding, 0)
        WHEN N'检验'   THEN ISNULL(pg.Inspection, 0)
        WHEN N'打焊头' THEN ISNULL(pg.WeldingHead, 0)
        WHEN N'润滑'   THEN ISNULL(pg.Lubrication, 0)
        WHEN N'入库'   THEN ISNULL(pg.Warehouse, 0)
        ELSE 0
    END
FROM [SectionOutsource] so
INNER JOIN [ProcessGroup] pg ON so.ProcessGroupId = pg.Id;
");

            // ========== 删除废弃字段 ==========
            migrationBuilder.DropColumn(
                name: "IsQualified",
                table: "OutsourceRecovery");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 无法恢复旧数据，此处仅反向重建 IsQualified 列
            migrationBuilder.AddColumn<bool>(
                name: "IsQualified",
                table: "OutsourceRecovery",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }
    }
}
