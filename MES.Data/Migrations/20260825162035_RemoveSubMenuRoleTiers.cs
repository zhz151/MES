using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSubMenuRoleTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ======================================================================
            // 二级菜单权限全面回退（2026-08-26）：删除 20260825120000 引入的
            // 18 个三档二级角色 {Warehouse}{Raw/Fg/Wip/Defect/MonthlyStock/PendingDelivery}{Viewer/Editor/Full}
            // 授权模型回到纯一级：14 菜单 × 3 档 + Admin = 43 角色
            // 说明：
            //  ① 纯数据迁移（模型无变化），仅删除角色行
            //  ② 先删 AspNetUserRoles 关联再删 AspNetRoles（FK 约束）
            //  ③ 一级角色 WarehouseViewer/Editor/Full 与 Report 三档保留，不受影响
            // ======================================================================
            migrationBuilder.Sql("""
                -- ① 删除用户与 18 个二级角色的关联
                DELETE ur
                FROM [AspNetUserRoles] ur
                JOIN [AspNetRoles] r ON ur.[RoleId] = r.[Id]
                WHERE r.[Name] IN (
                    N'WarehouseRawViewer',             N'WarehouseRawEditor',             N'WarehouseRawFull',
                    N'WarehouseFgViewer',              N'WarehouseFgEditor',              N'WarehouseFgFull',
                    N'WarehouseWipViewer',             N'WarehouseWipEditor',             N'WarehouseWipFull',
                    N'WarehouseDefectViewer',          N'WarehouseDefectEditor',          N'WarehouseDefectFull',
                    N'WarehouseMonthlyStockViewer',    N'WarehouseMonthlyStockEditor',    N'WarehouseMonthlyStockFull',
                    N'WarehousePendingDeliveryViewer', N'WarehousePendingDeliveryEditor', N'WarehousePendingDeliveryFull');

                -- ② 删除 18 个二级角色
                DELETE FROM [AspNetRoles] WHERE [Name] IN (
                    N'WarehouseRawViewer',             N'WarehouseRawEditor',             N'WarehouseRawFull',
                    N'WarehouseFgViewer',              N'WarehouseFgEditor',              N'WarehouseFgFull',
                    N'WarehouseWipViewer',             N'WarehouseWipEditor',             N'WarehouseWipFull',
                    N'WarehouseDefectViewer',          N'WarehouseDefectEditor',          N'WarehouseDefectFull',
                    N'WarehouseMonthlyStockViewer',    N'WarehouseMonthlyStockEditor',    N'WarehouseMonthlyStockFull',
                    N'WarehousePendingDeliveryViewer', N'WarehousePendingDeliveryEditor', N'WarehousePendingDeliveryFull');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 反向：插回 18 个三档二级角色（用户关联无法还原，仅恢复角色行）
            migrationBuilder.Sql("""
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
                """);
        }
    }
}
