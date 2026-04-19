using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWorkOrderIdColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 删除索引（如果存在）
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OrderItem_WorkOrderId')
                BEGIN
                    DROP INDEX [IX_OrderItem_WorkOrderId] ON [OrderItem];
                END
            ");

            // 删除 WorkOrderId 列
            migrationBuilder.DropColumn(
                name: "WorkOrderId",
                table: "OrderItem");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 重新添加 WorkOrderId 列（允许 NULL）
            migrationBuilder.AddColumn<int>(
                name: "WorkOrderId",
                table: "OrderItem",
                type: "int",
                nullable: true);

            // 重新创建索引（可选）
            migrationBuilder.CreateIndex(
                name: "IX_OrderItem_WorkOrderId",
                table: "OrderItem",
                column: "WorkOrderId");
        }
    }
}
