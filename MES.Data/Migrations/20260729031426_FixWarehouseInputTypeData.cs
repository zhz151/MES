using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixWarehouseInputTypeData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 将有仓库来源但 InputType 被误标记为 SplitFromNumber 的批次修正为 Warehouse
            migrationBuilder.Sql(@"UPDATE pb SET InputType = 'Warehouse'
                FROM ProductionBatch pb
                WHERE pb.InputType = 'SplitFromNumber'
                AND (
                    pb.SourceBatchNo IS NOT NULL
                    OR EXISTS (SELECT 1 FROM ProductionBatchInventory pbi WHERE pbi.ProductionBatchId = pb.Id)
                )");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
