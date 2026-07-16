using Microsoft.EntityFrameworkCore;
using MES.Data;

namespace MES.Data.Seed;

/// <summary>
/// 数据库迁移策略：兼容既有 EnsureCreated 和 Migrate 模式的迁移执行器
/// </summary>
public static class DatabaseMigrator
{
    /// <summary>
    /// 执行数据库迁移，兼容历史遗留的 EnsureCreated 创建的数据库
    /// </summary>
    public static async Task ApplyMigrationsAsync(AppDbContext context)
    {
        // 检测 __EFMigrationsHistory 是否存在
        bool hasHistoryTable;
        try
        {
            _ = await context.Database.GetAppliedMigrationsAsync();
            hasHistoryTable = true;
        }
        catch
        {
            hasHistoryTable = false;
        }

        if (hasHistoryTable)
        {
            // 已有迁移历史，直接应用待处理迁移
            var pending = await context.Database.GetPendingMigrationsAsync();
            if (pending.Any())
                await context.Database.MigrateAsync();
        }
        else
        {
            // 数据库由 EnsureCreated 创建（无迁移历史）
            // 先创建 __EFMigrationsHistory 表并标记已存在的迁移
            await context.Database.ExecuteSqlRawAsync(@"
                IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
                BEGIN
                    CREATE TABLE [__EFMigrationsHistory] (
                        [MigrationId] nvarchar(150) NOT NULL,
                        [ProductVersion] nvarchar(32) NOT NULL,
                        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                    );
                    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                    VALUES ('20260521022534_AddOrderListSummary', '8.0.0');
                END
            ");
            // 然后应用新增迁移（AddWorkOrderReadModels）
            await context.Database.MigrateAsync();
        }
    }
}
