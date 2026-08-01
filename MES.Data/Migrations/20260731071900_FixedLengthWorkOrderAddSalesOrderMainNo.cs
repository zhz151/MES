using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixedLengthWorkOrderAddSalesOrderMainNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductionMainNo",
                table: "FixedLengthWorkOrder",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SalesOrderNo",
                table: "FixedLengthWorkOrder",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // 回填：从 WorkOrder 补齐订单号/主号（新增列默认空串，历史数据需回填）
            migrationBuilder.Sql("""
                UPDATE f
                SET f.SalesOrderNo = w.SalesOrderNo,
                    f.ProductionMainNo = w.ProductionMainNo
                FROM FixedLengthWorkOrder f
                INNER JOIN WorkOrder w ON f.WorkOrderId = w.Id
                WHERE f.SalesOrderNo = '' OR f.ProductionMainNo = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_FixedLengthWorkOrder_SalesOrderMainNoLength",
                table: "FixedLengthWorkOrder",
                columns: new[] { "SalesOrderNo", "ProductionMainNo", "Length" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FixedLengthWorkOrder_SalesOrderMainNoLength",
                table: "FixedLengthWorkOrder");

            migrationBuilder.DropColumn(
                name: "ProductionMainNo",
                table: "FixedLengthWorkOrder");

            migrationBuilder.DropColumn(
                name: "SalesOrderNo",
                table: "FixedLengthWorkOrder");
        }
    }
}
