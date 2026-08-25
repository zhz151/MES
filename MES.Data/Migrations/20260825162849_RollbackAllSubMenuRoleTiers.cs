using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RollbackAllSubMenuRoleTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ======================================================================
            // 二级菜单权限全面回退·残留清理（2026-08-26）：
            //   ① 删除 20260825160000_AddAllSubMenuRoleTiers 曾写入真库的 192 个
            //      页面级二级角色（{Prefix}{SubKey}{Viewer/Editor/Full}），仅保留
            //      纯一级 43 角色（Admin + 14 菜单 × 3 档）。
            //   ② 从 __EFMigrationsHistory 移除 20260825160000 记录——该迁移本地
            //      文件已删除，同步历史与代码库一致（避免 EF 迁移链悬挂）。
            // 说明：
            //   - 残留角色已确认无任何用户关联（AspNetUserRoles 0 条）、无 RoleClaims，
            //     属纯死数据，删除安全。
            //   - 用「保留名单 NOT IN」反向删除：白名单即当前 Roles.GetAllRoles() 的
            //     43 个有效角色，其余全部清除，杜绝漏删。
            // ======================================================================
            migrationBuilder.Sql("""
                -- ① 删除非 43 个有效角色的用户关联（先删关联再删角色，FK 约束）
                DELETE ur
                FROM [AspNetUserRoles] ur
                JOIN [AspNetRoles] r ON ur.[RoleId] = r.[Id]
                WHERE r.[Name] NOT IN (
                    N'Admin',
                    N'BatchViewer',             N'BatchEditor',             N'BatchFull',
                    N'ConfigurationViewer',     N'ConfigurationEditor',     N'ConfigurationFull',
                    N'DataToolViewer',          N'DataToolEditor',          N'DataToolFull',
                    N'EquipmentViewer',         N'EquipmentEditor',         N'EquipmentFull',
                    N'MaterialViewer',          N'MaterialEditor',          N'MaterialFull',
                    N'OrderViewer',             N'OrderEditor',             N'OrderFull',
                    N'QualityViewer',           N'QualityEditor',           N'QualityFull',
                    N'ReportViewer',            N'ReportEditor',            N'ReportFull',
                    N'ScanViewer',              N'ScanEditor',              N'ScanFull',
                    N'SchedulingViewer',        N'SchedulingEditor',        N'SchedulingFull',
                    N'StandardViewer',          N'StandardEditor',          N'StandardFull',
                    N'UserViewer',              N'UserEditor',              N'UserFull',
                    N'WarehouseViewer',         N'WarehouseEditor',         N'WarehouseFull',
                    N'WorkOrderViewer',         N'WorkOrderEditor',         N'WorkOrderFull');

                -- ② 删除非 43 个有效角色（含 RoleClaims 一并清，防残留）
                DELETE rc
                FROM [AspNetRoleClaims] rc
                JOIN [AspNetRoles] r ON rc.[RoleId] = r.[Id]
                WHERE r.[Name] NOT IN (
                    N'Admin',
                    N'BatchViewer',             N'BatchEditor',             N'BatchFull',
                    N'ConfigurationViewer',     N'ConfigurationEditor',     N'ConfigurationFull',
                    N'DataToolViewer',          N'DataToolEditor',          N'DataToolFull',
                    N'EquipmentViewer',         N'EquipmentEditor',         N'EquipmentFull',
                    N'MaterialViewer',          N'MaterialEditor',          N'MaterialFull',
                    N'OrderViewer',             N'OrderEditor',             N'OrderFull',
                    N'QualityViewer',           N'QualityEditor',           N'QualityFull',
                    N'ReportViewer',            N'ReportEditor',            N'ReportFull',
                    N'ScanViewer',              N'ScanEditor',              N'ScanFull',
                    N'SchedulingViewer',        N'SchedulingEditor',        N'SchedulingFull',
                    N'StandardViewer',          N'StandardEditor',          N'StandardFull',
                    N'UserViewer',              N'UserEditor',              N'UserFull',
                    N'WarehouseViewer',         N'WarehouseEditor',         N'WarehouseFull',
                    N'WorkOrderViewer',         N'WorkOrderEditor',         N'WorkOrderFull');

                DELETE FROM [AspNetRoles] WHERE [Name] NOT IN (
                    N'Admin',
                    N'BatchViewer',             N'BatchEditor',             N'BatchFull',
                    N'ConfigurationViewer',     N'ConfigurationEditor',     N'ConfigurationFull',
                    N'DataToolViewer',          N'DataToolEditor',          N'DataToolFull',
                    N'EquipmentViewer',         N'EquipmentEditor',         N'EquipmentFull',
                    N'MaterialViewer',          N'MaterialEditor',          N'MaterialFull',
                    N'OrderViewer',             N'OrderEditor',             N'OrderFull',
                    N'QualityViewer',           N'QualityEditor',           N'QualityFull',
                    N'ReportViewer',            N'ReportEditor',            N'ReportFull',
                    N'ScanViewer',              N'ScanEditor',              N'ScanFull',
                    N'SchedulingViewer',        N'SchedulingEditor',        N'SchedulingFull',
                    N'StandardViewer',          N'StandardEditor',          N'StandardFull',
                    N'UserViewer',              N'UserEditor',              N'UserFull',
                    N'WarehouseViewer',         N'WarehouseEditor',         N'WarehouseFull',
                    N'WorkOrderViewer',         N'WorkOrderEditor',         N'WorkOrderFull');

                -- ③ 同步迁移历史：移除本地已删除的 20260825160000（防止迁移链悬挂）
                DELETE FROM [__EFMigrationsHistory]
                WHERE [MigrationId] = N'20260825160000_AddAllSubMenuRoleTiers';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 反向：恢复迁移历史记录（角色行无法还原——原 20260825160000 已删除，
            // 其 192 个页面级角色需依赖该迁移重新应用，此处仅恢复历史标记）。
            migrationBuilder.Sql("""
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (N'20260825160000_AddAllSubMenuRoleTiers', N'8.0.0');
                """);
        }
    }
}
