using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSubcontractOrderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 安全删除 SubcontractOrder.SourceWorkOrderNo（仅当存在时）
            migrationBuilder.Sql(@"
                DECLARE @cn NVARCHAR(200);
                SELECT @cn = d.name FROM sys.default_constraints d
                JOIN sys.columns c ON d.parent_object_id = c.object_id AND d.parent_column_id = c.column_id
                WHERE d.parent_object_id = OBJECT_ID(N'[SubcontractOrder]') AND c.name = 'SourceWorkOrderNo';
                IF @cn IS NOT NULL EXEC('ALTER TABLE [SubcontractOrder] DROP CONSTRAINT [' + @cn + ']');
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[SubcontractOrder]') AND name = 'SourceWorkOrderNo')
                    ALTER TABLE [SubcontractOrder] DROP COLUMN [SourceWorkOrderNo];
            ");

            // 安全添加 SubcontractReturnItem.SourceWorkOrderNo（仅当不存在时）
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[SubcontractReturnItem]') AND name = 'SourceWorkOrderNo')
                BEGIN
                    ALTER TABLE [SubcontractReturnItem] ADD [SourceWorkOrderNo] nvarchar(50) NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[SubcontractReturnItem]') AND name = 'SourceWorkOrderNo')
                BEGIN
                    ALTER TABLE [SubcontractReturnItem] DROP COLUMN [SourceWorkOrderNo];
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[SubcontractOrder]') AND name = 'SourceWorkOrderNo')
                BEGIN
                    ALTER TABLE [SubcontractOrder] ADD [SourceWorkOrderNo] nvarchar(50) NULL;
                END
            ");
        }
    }
}
