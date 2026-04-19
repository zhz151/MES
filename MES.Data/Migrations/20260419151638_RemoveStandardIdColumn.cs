using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStandardIdColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. 删除外键约束（如果存在）
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_ProductRequirement_ProductionStandard_StandardId')
                BEGIN
                    ALTER TABLE [ProductRequirement] DROP CONSTRAINT [FK_ProductRequirement_ProductionStandard_StandardId];
                END
            ");

            // 2. 删除索引（如果存在）
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProductRequirement_StandardId')
                BEGIN
                    DROP INDEX [IX_ProductRequirement_StandardId] ON [ProductRequirement];
                END
            ");

            // 3. 删除列
            migrationBuilder.DropColumn(
                name: "StandardId",
                table: "ProductRequirement");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚时重新添加列（允许 NULL）
            migrationBuilder.AddColumn<int>(
                name: "StandardId",
                table: "ProductRequirement",
                type: "int",
                nullable: true);

            // 可选：重新创建索引和外键（根据业务需要）
            // 如果回滚时需要恢复外键，可取消注释以下代码（请根据实际外键名和主表调整）
            // migrationBuilder.Sql("CREATE INDEX [IX_ProductRequirement_StandardId] ON [ProductRequirement] ([StandardId])");
            // migrationBuilder.AddForeignKey(
            //     name: "FK_ProductRequirement_ProductionStandard_StandardId",
            //     table: "ProductRequirement",
            //     column: "StandardId",
            //     principalTable: "ProductionStandard",
            //     principalColumn: "Id",
            //     onDelete: ReferentialAction.SetNull);
        }
    }
}
