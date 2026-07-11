using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductStatusToRemainingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ==========================================
            // 1. 添加 ProductStatus 列（幂等：列已存在时跳过）
            // ==========================================
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[SectionOutsource]') AND name = 'ProductStatus')
                    ALTER TABLE [SectionOutsource] ADD [ProductStatus] nvarchar(20) NULL;
            ");
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ProcessInspection]') AND name = 'ProductStatus')
                    ALTER TABLE [ProcessInspection] ADD [ProductStatus] nvarchar(20) NULL;
            ");
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[PicklingOutRecord]') AND name = 'ProductStatus')
                    ALTER TABLE [PicklingOutRecord] ADD [ProductStatus] nvarchar(20) NULL;
            ");
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[PicklingInRecord]') AND name = 'ProductStatus')
                    ALTER TABLE [PicklingInRecord] ADD [ProductStatus] nvarchar(20) NULL;
            ");

            // ==========================================
            // 2. 先清空 ProductStatus（兼容列已存在且被错误填充的场景）
            // ==========================================
            migrationBuilder.Sql("UPDATE [PicklingInRecord] SET [ProductStatus] = NULL;");
            migrationBuilder.Sql("UPDATE [PicklingOutRecord] SET [ProductStatus] = NULL;");
            migrationBuilder.Sql("UPDATE [SectionOutsource] SET [ProductStatus] = NULL;");
            migrationBuilder.Sql("UPDATE [ProcessInspection] SET [ProductStatus] = NULL;");

            // ==========================================
            // 3. 回填 PicklingInRecord — 荒管/在制
            // ==========================================
            // 3a. ProcessName='荒管处理' → 荒管
            migrationBuilder.Sql(@"
                UPDATE [PicklingInRecord]
                SET [ProductStatus] = N'荒管'
                WHERE [ProductStatus] IS NULL AND [ProcessName] = N'荒管处理';
            ");
            // 3b. IsFinished=1 → 成品（IsFinished 列可能已被旧迁移删除，故加 IF EXISTS）
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[PicklingInRecord]') AND name = 'IsFinished')
                BEGIN
                    UPDATE [PicklingInRecord] SET [ProductStatus] = N'成品' WHERE [ProductStatus] IS NULL AND [IsFinished] = 1;
                END
            ");
            // 3c. 剩余 → 在制
            migrationBuilder.Sql(@"
                UPDATE [PicklingInRecord] SET [ProductStatus] = N'在制' WHERE [ProductStatus] IS NULL;
            ");

            // ==========================================
            // 4. 回填 PicklingOutRecord — 通过关联入缸记录判断
            // ==========================================
            // 4a. 关联入缸记录中 ProcessName='荒管处理' → 荒管
            migrationBuilder.Sql(@"
                UPDATE por
                SET por.[ProductStatus] = N'荒管'
                FROM [PicklingOutRecord] por
                INNER JOIN [PicklingInRecord] pir ON por.[PicklingInRecordId] = pir.[Id]
                WHERE por.[ProductStatus] IS NULL AND pir.[ProcessName] = N'荒管处理';
            ");
            // 4b. IsFinished=1 → 成品
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[PicklingOutRecord]') AND name = 'IsFinished')
                BEGIN
                    UPDATE [PicklingOutRecord] SET [ProductStatus] = N'成品' WHERE [ProductStatus] IS NULL AND [IsFinished] = 1;
                END
            ");
            // 4c. 剩余 → 在制
            migrationBuilder.Sql(@"
                UPDATE [PicklingOutRecord] SET [ProductStatus] = N'在制' WHERE [ProductStatus] IS NULL;
            ");

            // ==========================================
            // 5. 回填 SectionOutsource — 通过工序名判断
            // ==========================================
            // 5a. ProcessName='荒管处理' → 荒管
            migrationBuilder.Sql(@"
                UPDATE [SectionOutsource]
                SET [ProductStatus] = N'荒管'
                WHERE [ProductStatus] IS NULL AND [ProcessName] = N'荒管处理';
            ");
            // 5b. 剩余 → 在制
            migrationBuilder.Sql(@"
                UPDATE [SectionOutsource] SET [ProductStatus] = N'在制' WHERE [ProductStatus] IS NULL;
            ");

            // ==========================================
            // 6. 回填 ProcessInspection — 通过工序名判断
            // ==========================================
            // 6a. ProcessName='荒管处理' → 荒管
            migrationBuilder.Sql(@"
                UPDATE [ProcessInspection]
                SET [ProductStatus] = N'荒管'
                WHERE [ProductStatus] IS NULL AND [ProcessName] = N'荒管处理';
            ");
            // 6b. 剩余 → 在制
            migrationBuilder.Sql(@"
                UPDATE [ProcessInspection] SET [ProductStatus] = N'在制' WHERE [ProductStatus] IS NULL;
            ");

            // ==========================================
            // 7. 删除旧的 IsFinished 列（幂等）
            // ==========================================
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[PicklingOutRecord]') AND name = 'IsFinished')
                    ALTER TABLE [PicklingOutRecord] DROP COLUMN [IsFinished];
            ");
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[PicklingInRecord]') AND name = 'IsFinished')
                    ALTER TABLE [PicklingInRecord] DROP COLUMN [IsFinished];
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 幂等回退：恢复 IsFinished（如不存在才添加），删除 ProductStatus
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[PicklingOutRecord]') AND name = 'IsFinished')
                    ALTER TABLE [PicklingOutRecord] ADD [IsFinished] bit NOT NULL DEFAULT 0;
            ");
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[PicklingInRecord]') AND name = 'IsFinished')
                    ALTER TABLE [PicklingInRecord] ADD [IsFinished] bit NOT NULL DEFAULT 0;
            ");
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[SectionOutsource]') AND name = 'ProductStatus')
                    ALTER TABLE [SectionOutsource] DROP COLUMN [ProductStatus];
            ");
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[ProcessInspection]') AND name = 'ProductStatus')
                    ALTER TABLE [ProcessInspection] DROP COLUMN [ProductStatus];
            ");
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[PicklingOutRecord]') AND name = 'ProductStatus')
                    ALTER TABLE [PicklingOutRecord] DROP COLUMN [ProductStatus];
            ");
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[PicklingInRecord]') AND name = 'ProductStatus')
                    ALTER TABLE [PicklingInRecord] DROP COLUMN [ProductStatus];
            ");
        }
    }
}
