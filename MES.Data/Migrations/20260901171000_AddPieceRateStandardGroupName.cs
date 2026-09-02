using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPieceRateStandardGroupName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupName",
                table: "PieceRateStandards",
                type: "nvarchar(20)",
                nullable: true);

            // 计件类别改名（英文 Key 变更，存量数据同步迁移）：拉机→冷拔、内轧→50冷轧
            migrationBuilder.Sql("UPDATE PieceRateStandards SET SectionName = 'ColdDraw' WHERE SectionName = 'DrawingMachine'");
            migrationBuilder.Sql("UPDATE PieceRateStandards SET SectionName = 'FiftyColdRoll' WHERE SectionName = 'InnerRolling'");

            // 焊管计件工段删除（56 行规则作废）
            migrationBuilder.Sql("DELETE FROM PieceRateStandards WHERE SectionName = 'WeldedPipe'");

            // 回填工段名称（GroupName = 计件类别所属工段）
            migrationBuilder.Sql("UPDATE PieceRateStandards SET GroupName = 'Pickling' WHERE SectionName IN ('CrudePickling','FinishedPickling')");
            migrationBuilder.Sql("UPDATE PieceRateStandards SET GroupName = 'Degrease' WHERE SectionName = 'Degrease'");
            migrationBuilder.Sql("UPDATE PieceRateStandards SET GroupName = 'OilPipeCut' WHERE SectionName = 'OilPipeCut'");
            migrationBuilder.Sql("UPDATE PieceRateStandards SET GroupName = 'Cut' WHERE SectionName IN ('CutFile','CrudeFace')");
            migrationBuilder.Sql("UPDATE PieceRateStandards SET GroupName = 'Straighten' WHERE SectionName IN ('Straighten','CrudeStraighten')");
            migrationBuilder.Sql("UPDATE PieceRateStandards SET GroupName = 'OuterPolish' WHERE SectionName = 'CrudePolish'");
            migrationBuilder.Sql("UPDATE PieceRateStandards SET GroupName = 'InnerGrinding' WHERE SectionName = 'CrudeInnerInspect'");
            migrationBuilder.Sql("UPDATE PieceRateStandards SET GroupName = 'Solution' WHERE SectionName = 'Solution'");
            migrationBuilder.Sql("UPDATE PieceRateStandards SET GroupName = 'ColdRollDraw' WHERE SectionName IN ('FiftyColdRoll','ColdDraw')");
            migrationBuilder.Sql("UPDATE PieceRateStandards SET GroupName = 'FinishedInspection' WHERE SectionName IN ('WaterPressure','SurfaceInspection')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 还原计件类别英文 Key（焊管已删除的数据不恢复）
            migrationBuilder.Sql("UPDATE PieceRateStandards SET SectionName = 'DrawingMachine' WHERE SectionName = 'ColdDraw'");
            migrationBuilder.Sql("UPDATE PieceRateStandards SET SectionName = 'InnerRolling' WHERE SectionName = 'FiftyColdRoll'");

            migrationBuilder.DropColumn(
                name: "GroupName",
                table: "PieceRateStandards");
        }
    }
}
