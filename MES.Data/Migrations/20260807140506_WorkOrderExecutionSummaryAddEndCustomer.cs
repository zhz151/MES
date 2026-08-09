using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrderExecutionSummaryAddEndCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EndCustomer",
                table: "WorkOrderExecutionSummary",
                type: "nvarchar(max)",
                nullable: true);

            // 存量回填：按订单号从 SalesOrder.EndCustomer 快照回填（133 订单全部有值）
            migrationBuilder.Sql("""
                UPDATE wes
                SET wes.EndCustomer = so.EndCustomer
                FROM WorkOrderExecutionSummary wes
                INNER JOIN SalesOrder so ON so.OrderNumber = wes.SalesOrderNo
                WHERE so.EndCustomer IS NOT NULL AND so.EndCustomer <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndCustomer",
                table: "WorkOrderExecutionSummary");
        }
    }
}
