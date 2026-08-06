using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 删除遗留表 ProcessGroupTemplate：
    /// 全代码库无实体/DbSet/Service/FK 引用（仅历史迁移注释提及），269 行遗留数据已确认无作用。
    /// 该表未映射到 EF 模型，故直接执行 DROP TABLE。
    /// </summary>
    public partial class DropProcessGroupTemplateLegacyTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessGroupTemplate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 数据已随表删除，无法精确还原；回滚仅重建空表结构（按原 29 列还原）
            migrationBuilder.CreateTable(
                name: "ProcessGroupTemplate",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    ProcessName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ManufacturingSpec = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OuterDiameterTolerance = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WallThicknessTolerance = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ManufacturingLength = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CuttingTreatment = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ManufacturingMultiple = table.Column<int>(type: "int", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ColdRollDraw = table.Column<int>(type: "int", nullable: true),
                    OilPipeCut = table.Column<int>(type: "int", nullable: true),
                    Degrease = table.Column<int>(type: "int", nullable: true),
                    Solution = table.Column<int>(type: "int", nullable: true),
                    Straighten = table.Column<int>(type: "int", nullable: true),
                    Cut = table.Column<int>(type: "int", nullable: true),
                    ThicknessMeasure = table.Column<int>(type: "int", nullable: true),
                    Pickle = table.Column<int>(type: "int", nullable: true),
                    OuterPolish = table.Column<int>(type: "int", nullable: true),
                    InnerGrinding = table.Column<int>(type: "int", nullable: true),
                    OuterSpotGrinding = table.Column<int>(type: "int", nullable: true),
                    Inspection = table.Column<int>(type: "int", nullable: true),
                    WeldingHead = table.Column<int>(type: "int", nullable: true),
                    Lubrication = table.Column<int>(type: "int", nullable: true),
                    Warehouse = table.Column<int>(type: "int", nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessGroupTemplate", x => x.Id);
                });
        }
    }
}
