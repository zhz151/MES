using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessGroupFieldsToMaterialReceiveCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProcessGroupId",
                table: "MaterialReceiveCheck",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProcessName",
                table: "MaterialReceiveCheck",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "检验");

            migrationBuilder.AddColumn<int>(
                name: "SequenceNumber",
                table: "MaterialReceiveCheck",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // 回填现有记录的工序组信息，确保加 FK 约束前已有有效值
            // 匹配规则：ProcessGroup.ManufacturingSpec == ProductionBatch.Specification
            // 优先非"附加成检"，同优先级取工序组序号最小
            // SequenceNumber = pg.Inspection（检验工段在该工序组中的执行序号，非工序组序号）
            migrationBuilder.Sql(@"
                WITH MatchedGroups AS (
                    SELECT
                        rc.Id AS MaterialReceiveCheckId,
                        pg.Id AS ProcessGroupId,
                        pg.ProcessName,
                        pg.Inspection AS SequenceNumber,
                        ROW_NUMBER() OVER (
                            PARTITION BY rc.Id
                            ORDER BY CASE WHEN pg.ProcessName = N'附加成检' THEN 1 ELSE 0 END, pg.SequenceNumber ASC
                        ) AS rn
                    FROM MaterialReceiveCheck rc
                    INNER JOIN ProductionBatch pb ON rc.ProductionBatchId = pb.Id
                    INNER JOIN ProcessGroup pg ON pg.ProductionBatchId = pb.Id
                        AND pg.ManufacturingSpec = pb.Specification
                        AND pg.Inspection IS NOT NULL
                    WHERE rc.ProcessGroupId = 0
                )
                UPDATE rc
                SET
                    rc.ProcessGroupId = mg.ProcessGroupId,
                    rc.ProcessName = mg.ProcessName,
                    rc.SequenceNumber = mg.SequenceNumber
                FROM MaterialReceiveCheck rc
                INNER JOIN MatchedGroups mg ON mg.MaterialReceiveCheckId = rc.Id AND mg.rn = 1;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReceiveCheck_ProcessGroupId",
                table: "MaterialReceiveCheck",
                column: "ProcessGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialReceiveCheck_ProcessGroup_ProcessGroupId",
                table: "MaterialReceiveCheck",
                column: "ProcessGroupId",
                principalTable: "ProcessGroup",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaterialReceiveCheck_ProcessGroup_ProcessGroupId",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropIndex(
                name: "IX_MaterialReceiveCheck_ProcessGroupId",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "ProcessGroupId",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "ProcessName",
                table: "MaterialReceiveCheck");

            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                table: "MaterialReceiveCheck");
        }
    }
}
