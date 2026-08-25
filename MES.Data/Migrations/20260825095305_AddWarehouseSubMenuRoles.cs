using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseSubMenuRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ======================================================================
            // 二级菜单权限（仓库试点 2026-08-25）：6 个二级角色（无 Tier 后缀，档位继承一级）
            // ① 插入 6 个二级角色：WarehouseRaw/Fg/Wip/Defect/MonthlyStock/PendingDelivery
            //    注意：NormalizedName 必须同步 UPPER(Name)，否则 RoleManager 判定 RoleExists 失真导致重复建角
            // ② 存量回填：有一级仓库权限（WarehouseViewer/Editor/Full）的用户默认继承全部 6 个二级角色
            //    （默认继承语义：有一级菜单权限默认获得其下全部二级权限，管理员可单独取消）
            // ======================================================================
            migrationBuilder.Sql("""
                -- ① 插入 6 个二级角色
                INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
                VALUES
                    (NEWID(), N'WarehouseRaw',             N'WAREHOUSERAW',             NEWID()),
                    (NEWID(), N'WarehouseFg',              N'WAREHOUSEFG',              NEWID()),
                    (NEWID(), N'WarehouseWip',             N'WAREHOUSEWIP',             NEWID()),
                    (NEWID(), N'WarehouseDefect',          N'WAREHOUSEDEFECT',          NEWID()),
                    (NEWID(), N'WarehouseMonthlyStock',    N'WAREHOUSEMONTHLYSTOCK',    NEWID()),
                    (NEWID(), N'WarehousePendingDelivery', N'WAREHOUSEPENDINGDELIVERY', NEWID());

                -- ② 存量回填：有一级仓库权限的用户全部获得 6 个二级角色（默认继承）
                INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
                SELECT ur.[UserId], sv.[Id]
                FROM [AspNetUserRoles] ur
                JOIN [AspNetRoles] rv ON ur.[RoleId] = rv.[Id]
                    AND rv.[Name] IN (N'WarehouseViewer', N'WarehouseEditor', N'WarehouseFull')
                JOIN [AspNetRoles] sv ON sv.[Name] IN (
                    N'WarehouseRaw', N'WarehouseFg', N'WarehouseWip', N'WarehouseDefect',
                    N'WarehouseMonthlyStock', N'WarehousePendingDelivery')
                WHERE NOT EXISTS (
                    SELECT 1 FROM [AspNetUserRoles] ur2
                    JOIN [AspNetRoles] r2 ON ur2.[RoleId] = r2.[Id]
                    WHERE ur2.[UserId] = ur.[UserId] AND r2.[Name] = sv.[Name]
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- ② 撤销存量回填：清掉 6 个二级角色的用户关联
                DELETE ur
                FROM [AspNetUserRoles] ur
                JOIN [AspNetRoles] r ON ur.[RoleId] = r.[Id]
                WHERE r.[Name] IN (
                    N'WarehouseRaw', N'WarehouseFg', N'WarehouseWip', N'WarehouseDefect',
                    N'WarehouseMonthlyStock', N'WarehousePendingDelivery');

                -- ① 删除 6 个二级角色
                DELETE FROM [AspNetRoles] WHERE [Name] IN (
                    N'WarehouseRaw', N'WarehouseFg', N'WarehouseWip', N'WarehouseDefect',
                    N'WarehouseMonthlyStock', N'WarehousePendingDelivery');
                """);
        }
    }
}
