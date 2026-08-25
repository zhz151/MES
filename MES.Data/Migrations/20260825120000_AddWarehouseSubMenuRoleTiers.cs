using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseSubMenuRoleTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ======================================================================
            // 二级菜单权限升级（2026-08-25）：二进制「有无」→ 每个二级项各自选档位
            // ① 插入 18 个三档二级角色：{前缀}{Key}{档位}（WarehouseRawViewer/Editor/Full …）
            // ② 转换回填：存量 6 个二进制二级角色关联 → 按用户一级仓库档位展开三档
            //    一级 Viewer→仅 Viewer；Editor→Viewer+Editor；Full→Viewer+Editor+Full
            // ③ 删除 6 个二进制二级角色（含 AspNetUserRoles 关联）
            // 注意：NormalizedName 必须同步 UPPER(Name)，否则 RoleManager 判定失真
            // ======================================================================
            migrationBuilder.Sql("""
                -- ① 插入 18 个三档二级角色
                INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
                VALUES
                    (NEWID(), N'WarehouseRawViewer',             N'WAREHOUSERAWVIEWER',             NEWID()),
                    (NEWID(), N'WarehouseRawEditor',             N'WAREHOUSERAWEDITOR',             NEWID()),
                    (NEWID(), N'WarehouseRawFull',               N'WAREHOUSERAWFULL',               NEWID()),
                    (NEWID(), N'WarehouseFgViewer',              N'WAREHOUSEFGVIEWER',              NEWID()),
                    (NEWID(), N'WarehouseFgEditor',              N'WAREHOUSEFGEDITOR',              NEWID()),
                    (NEWID(), N'WarehouseFgFull',                N'WAREHOUSEFGFULL',                NEWID()),
                    (NEWID(), N'WarehouseWipViewer',             N'WAREHOUSEWIPVIEWER',             NEWID()),
                    (NEWID(), N'WarehouseWipEditor',             N'WAREHOUSEWIPEDITOR',             NEWID()),
                    (NEWID(), N'WarehouseWipFull',               N'WAREHOUSEWIPFULL',               NEWID()),
                    (NEWID(), N'WarehouseDefectViewer',          N'WAREHOUSEDEFECTVIEWER',          NEWID()),
                    (NEWID(), N'WarehouseDefectEditor',          N'WAREHOUSEDEFECTEDITOR',          NEWID()),
                    (NEWID(), N'WarehouseDefectFull',            N'WAREHOUSEDEFECTFULL',            NEWID()),
                    (NEWID(), N'WarehouseMonthlyStockViewer',    N'WAREHOUSEMONTHLYSTOCKVIEWER',    NEWID()),
                    (NEWID(), N'WarehouseMonthlyStockEditor',    N'WAREHOUSEMONTHLYSTOCKEDITOR',    NEWID()),
                    (NEWID(), N'WarehouseMonthlyStockFull',      N'WAREHOUSEMONTHLYSTOCKFULL',      NEWID()),
                    (NEWID(), N'WarehousePendingDeliveryViewer', N'WAREHOUSEPENDINGDELIVERYVIEWER', NEWID()),
                    (NEWID(), N'WarehousePendingDeliveryEditor', N'WAREHOUSEPENDINGDELIVERYEDITOR', NEWID()),
                    (NEWID(), N'WarehousePendingDeliveryFull',   N'WAREHOUSEPENDINGDELIVERYFULL',   NEWID());

                -- ② 转换回填 Viewer 档：存量二进制二级角色关联 + 一级 ≥Viewer → 对应三档 Viewer
                INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
                SELECT DISTINCT ur.[UserId], rv.[Id]
                FROM [AspNetUserRoles] ur
                JOIN [AspNetRoles] r ON ur.[RoleId] = r.[Id]
                    AND r.[Name] IN (
                        N'WarehouseRaw', N'WarehouseFg', N'WarehouseWip', N'WarehouseDefect',
                        N'WarehouseMonthlyStock', N'WarehousePendingDelivery')
                JOIN [AspNetRoles] rv ON rv.[Name] = r.[Name] + N'Viewer'
                JOIN [AspNetUserRoles] fl ON fl.[UserId] = ur.[UserId]
                JOIN [AspNetRoles] fr ON fl.[RoleId] = fr.[Id]
                    AND fr.[Name] IN (N'WarehouseViewer', N'WarehouseEditor', N'WarehouseFull')
                WHERE NOT EXISTS (
                    SELECT 1 FROM [AspNetUserRoles] ur2
                    JOIN [AspNetRoles] r2 ON ur2.[RoleId] = r2.[Id]
                    WHERE ur2.[UserId] = ur.[UserId] AND r2.[Name] = rv.[Name]
                );

                -- ② 转换回填 Editor 档：一级 ≥Editor
                INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
                SELECT DISTINCT ur.[UserId], rv.[Id]
                FROM [AspNetUserRoles] ur
                JOIN [AspNetRoles] r ON ur.[RoleId] = r.[Id]
                    AND r.[Name] IN (
                        N'WarehouseRaw', N'WarehouseFg', N'WarehouseWip', N'WarehouseDefect',
                        N'WarehouseMonthlyStock', N'WarehousePendingDelivery')
                JOIN [AspNetRoles] rv ON rv.[Name] = r.[Name] + N'Editor'
                JOIN [AspNetUserRoles] fl ON fl.[UserId] = ur.[UserId]
                JOIN [AspNetRoles] fr ON fl.[RoleId] = fr.[Id]
                    AND fr.[Name] IN (N'WarehouseEditor', N'WarehouseFull')
                WHERE NOT EXISTS (
                    SELECT 1 FROM [AspNetUserRoles] ur2
                    JOIN [AspNetRoles] r2 ON ur2.[RoleId] = r2.[Id]
                    WHERE ur2.[UserId] = ur.[UserId] AND r2.[Name] = rv.[Name]
                );

                -- ② 转换回填 Full 档：一级 Full
                INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
                SELECT DISTINCT ur.[UserId], rv.[Id]
                FROM [AspNetUserRoles] ur
                JOIN [AspNetRoles] r ON ur.[RoleId] = r.[Id]
                    AND r.[Name] IN (
                        N'WarehouseRaw', N'WarehouseFg', N'WarehouseWip', N'WarehouseDefect',
                        N'WarehouseMonthlyStock', N'WarehousePendingDelivery')
                JOIN [AspNetRoles] rv ON rv.[Name] = r.[Name] + N'Full'
                JOIN [AspNetUserRoles] fl ON fl.[UserId] = ur.[UserId]
                JOIN [AspNetRoles] fr ON fl.[RoleId] = fr.[Id]
                    AND fr.[Name] = N'WarehouseFull'
                WHERE NOT EXISTS (
                    SELECT 1 FROM [AspNetUserRoles] ur2
                    JOIN [AspNetRoles] r2 ON ur2.[RoleId] = r2.[Id]
                    WHERE ur2.[UserId] = ur.[UserId] AND r2.[Name] = rv.[Name]
                );

                -- ③ 删除 6 个二进制二级角色（先删用户关联再删角色）
                DELETE ur
                FROM [AspNetUserRoles] ur
                JOIN [AspNetRoles] r ON ur.[RoleId] = r.[Id]
                WHERE r.[Name] IN (
                    N'WarehouseRaw', N'WarehouseFg', N'WarehouseWip', N'WarehouseDefect',
                    N'WarehouseMonthlyStock', N'WarehousePendingDelivery');

                DELETE FROM [AspNetRoles] WHERE [Name] IN (
                    N'WarehouseRaw', N'WarehouseFg', N'WarehouseWip', N'WarehouseDefect',
                    N'WarehouseMonthlyStock', N'WarehousePendingDelivery');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- ① 插回 6 个二进制二级角色
                INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
                VALUES
                    (NEWID(), N'WarehouseRaw',             N'WAREHOUSERAW',             NEWID()),
                    (NEWID(), N'WarehouseFg',              N'WAREHOUSEFG',              NEWID()),
                    (NEWID(), N'WarehouseWip',             N'WAREHOUSEWIP',             NEWID()),
                    (NEWID(), N'WarehouseDefect',          N'WAREHOUSEDEFECT',          NEWID()),
                    (NEWID(), N'WarehouseMonthlyStock',    N'WAREHOUSEMONTHLYSTOCK',    NEWID()),
                    (NEWID(), N'WarehousePendingDelivery', N'WAREHOUSEPENDINGDELIVERY', NEWID());

                -- ② 收缩回填：三档二级角色关联 → 二进制（任一档位即获得二进制「有无」）
                INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
                SELECT DISTINCT ur.[UserId], rb.[Id]
                FROM [AspNetUserRoles] ur
                JOIN [AspNetRoles] r ON ur.[RoleId] = r.[Id]
                    AND r.[Name] IN (
                        N'WarehouseRawViewer', N'WarehouseRawEditor', N'WarehouseRawFull',
                        N'WarehouseFgViewer', N'WarehouseFgEditor', N'WarehouseFgFull',
                        N'WarehouseWipViewer', N'WarehouseWipEditor', N'WarehouseWipFull',
                        N'WarehouseDefectViewer', N'WarehouseDefectEditor', N'WarehouseDefectFull',
                        N'WarehouseMonthlyStockViewer', N'WarehouseMonthlyStockEditor', N'WarehouseMonthlyStockFull',
                        N'WarehousePendingDeliveryViewer', N'WarehousePendingDeliveryEditor', N'WarehousePendingDeliveryFull')
                JOIN [AspNetRoles] rb ON rb.[Name] =
                    REPLACE(REPLACE(REPLACE(r.[Name], N'Viewer', N''), N'Editor', N''), N'Full', N'')
                WHERE NOT EXISTS (
                    SELECT 1 FROM [AspNetUserRoles] ur2
                    JOIN [AspNetRoles] r2 ON ur2.[RoleId] = r2.[Id]
                    WHERE ur2.[UserId] = ur.[UserId] AND r2.[Name] = rb.[Name]
                );

                -- ③ 删除 18 个三档二级角色（先删用户关联再删角色）
                DELETE ur
                FROM [AspNetUserRoles] ur
                JOIN [AspNetRoles] r ON ur.[RoleId] = r.[Id]
                WHERE r.[Name] IN (
                    N'WarehouseRawViewer', N'WarehouseRawEditor', N'WarehouseRawFull',
                    N'WarehouseFgViewer', N'WarehouseFgEditor', N'WarehouseFgFull',
                    N'WarehouseWipViewer', N'WarehouseWipEditor', N'WarehouseWipFull',
                    N'WarehouseDefectViewer', N'WarehouseDefectEditor', N'WarehouseDefectFull',
                    N'WarehouseMonthlyStockViewer', N'WarehouseMonthlyStockEditor', N'WarehouseMonthlyStockFull',
                    N'WarehousePendingDeliveryViewer', N'WarehousePendingDeliveryEditor', N'WarehousePendingDeliveryFull');

                DELETE FROM [AspNetRoles] WHERE [Name] IN (
                    N'WarehouseRawViewer', N'WarehouseRawEditor', N'WarehouseRawFull',
                    N'WarehouseFgViewer', N'WarehouseFgEditor', N'WarehouseFgFull',
                    N'WarehouseWipViewer', N'WarehouseWipEditor', N'WarehouseWipFull',
                    N'WarehouseDefectViewer', N'WarehouseDefectEditor', N'WarehouseDefectFull',
                    N'WarehouseMonthlyStockViewer', N'WarehouseMonthlyStockEditor', N'WarehouseMonthlyStockFull',
                    N'WarehousePendingDeliveryViewer', N'WarehousePendingDeliveryEditor', N'WarehousePendingDeliveryFull');
                """);
        }
    }
}
