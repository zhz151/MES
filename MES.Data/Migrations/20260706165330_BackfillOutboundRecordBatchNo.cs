using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillOutboundRecordBatchNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 回填现有出库记录的批次号（从 InventoryBatch 冗余）
            migrationBuilder.Sql(@"
                UPDATE o
                SET o.BatchNo = b.BatchNo
                FROM OutboundRecord o
                INNER JOIN InventoryBatch b ON b.Id = o.InventoryBatchId
                WHERE o.BatchNo IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
