using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MES.Data.Migrations
{
    /// <inheritdoc />
    public partial class RoleModelThreeTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ======================================================================
            // 角色权限模型重构（2026-08-25）：14 主菜单 × 3 档（Viewer/Editor/Full）+ Admin
            // ① 8 业务域角色改名：Staff→Editor（查增改）、Director→Full（查增改删）
            // ② 插入 26 新角色：8 业务域 Viewer + 6 独立菜单（计划排程/报表/数据工具/扫码/参数表/用户）× 3 档
            // ③ 存量报表可见用户（原 ReportOverview 并集角色持有者）补发 ReportViewer
            //    注意：NormalizedName 必须同步 UPPER(Name)，否则 RoleManager 判定 RoleExists 失真导致重复建角
            // ======================================================================
            migrationBuilder.Sql("""
                -- ① 8 业务域角色改名
                UPDATE [AspNetRoles] SET [Name] = REPLACE([Name], N'Staff',    N'Editor') WHERE [Name] LIKE N'%Staff';
                UPDATE [AspNetRoles] SET [Name] = REPLACE([Name], N'Director', N'Full')   WHERE [Name] LIKE N'%Director';
                UPDATE [AspNetRoles] SET [NormalizedName] = UPPER([Name]);

                -- ② 插入 26 新角色（8 业务域 Viewer + 6 独立菜单 × 3 档）
                INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
                VALUES
                    -- 8 业务域 Viewer（查）
                    (NEWID(), N'OrderViewer',       N'ORDERVIEWER',        NEWID()),
                    (NEWID(), N'WorkOrderViewer',   N'WORKORDERVIEWER',     NEWID()),
                    (NEWID(), N'BatchViewer',       N'BATCHVIEWER',         NEWID()),
                    (NEWID(), N'QualityViewer',     N'QUALITYVIEWER',       NEWID()),
                    (NEWID(), N'MaterialViewer',    N'MATERIALVIEWER',      NEWID()),
                    (NEWID(), N'WarehouseViewer',   N'WAREHOUSEVIEWER',     NEWID()),
                    (NEWID(), N'EquipmentViewer',   N'EQUIPMENTVIEWER',     NEWID()),
                    (NEWID(), N'StandardViewer',    N'STANDARDVIEWER',      NEWID()),
                    -- 计划排程（独立三档）
                    (NEWID(), N'SchedulingViewer',  N'SCHEDULINGVIEWER',    NEWID()),
                    (NEWID(), N'SchedulingEditor',  N'SCHEDULINGEDITOR',    NEWID()),
                    (NEWID(), N'SchedulingFull',    N'SCHEDULINGFULL',      NEWID()),
                    -- 报表系统（独立三档）
                    (NEWID(), N'ReportViewer',      N'REPORTVIEWER',        NEWID()),
                    (NEWID(), N'ReportEditor',      N'REPORTEDITOR',        NEWID()),
                    (NEWID(), N'ReportFull',        N'REPORTFULL',          NEWID()),
                    -- 数据工具（独立三档）
                    (NEWID(), N'DataToolViewer',    N'DATATOOLVIEWER',      NEWID()),
                    (NEWID(), N'DataToolEditor',    N'DATATOOLEDITOR',      NEWID()),
                    (NEWID(), N'DataToolFull',      N'DATATOOLFULL',        NEWID()),
                    -- 扫码管理（独立三档）
                    (NEWID(), N'ScanViewer',        N'SCANVIEWER',          NEWID()),
                    (NEWID(), N'ScanEditor',        N'SCANEDITOR',          NEWID()),
                    (NEWID(), N'ScanFull',          N'SCANFULL',            NEWID()),
                    -- 参数表（独立三档）
                    (NEWID(), N'ConfigurationViewer',   N'CONFIGURATIONVIEWER',   NEWID()),
                    (NEWID(), N'ConfigurationEditor',   N'CONFIGURATIONEDITOR',   NEWID()),
                    (NEWID(), N'ConfigurationFull',     N'CONFIGURATIONFULL',     NEWID()),
                    -- 用户管理（独立三档，非 Admin 可授权含提权）
                    (NEWID(), N'UserViewer',        N'USERVIEWER',          NEWID()),
                    (NEWID(), N'UserEditor',        N'USEREDITOR',          NEWID()),
                    (NEWID(), N'UserFull',          N'USERFULL',            NEWID());

                -- ③ 存量报表可见用户补发 ReportViewer（原 ReportOverview 并集，改名后 = 6 Full + QualityEditor；Admin 隐式全权无需补发）
                INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
                SELECT ur.[UserId], rv.[Id]
                FROM [AspNetUserRoles] ur
                JOIN [AspNetRoles] r  ON ur.[RoleId] = r.[Id]
                JOIN [AspNetRoles] rv ON rv.[Name] = N'ReportViewer'
                WHERE r.[Name] IN (N'OrderFull', N'WorkOrderFull', N'BatchFull', N'MaterialFull', N'WarehouseFull', N'QualityFull', N'QualityEditor')
                  AND NOT EXISTS (
                      SELECT 1 FROM [AspNetUserRoles] ur2
                      JOIN [AspNetRoles] r2 ON ur2.[RoleId] = r2.[Id]
                      WHERE ur2.[UserId] = ur.[UserId] AND r2.[Name] = N'ReportViewer'
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- ③ 撤销 ReportViewer 补发（该角色此前不存在，全部来自本迁移，可直接清）
                DELETE ur
                FROM [AspNetUserRoles] ur
                JOIN [AspNetRoles] r ON ur.[RoleId] = r.[Id]
                WHERE r.[Name] = N'ReportViewer';

                -- ② 删除 26 新角色
                DELETE FROM [AspNetRoles] WHERE [Name] IN (
                    N'OrderViewer', N'WorkOrderViewer', N'BatchViewer', N'QualityViewer', N'MaterialViewer', N'WarehouseViewer', N'EquipmentViewer', N'StandardViewer',
                    N'SchedulingViewer', N'SchedulingEditor', N'SchedulingFull',
                    N'ReportViewer', N'ReportEditor', N'ReportFull',
                    N'DataToolViewer', N'DataToolEditor', N'DataToolFull',
                    N'ScanViewer', N'ScanEditor', N'ScanFull',
                    N'ConfigurationViewer', N'ConfigurationEditor', N'ConfigurationFull',
                    N'UserViewer', N'UserEditor', N'UserFull');

                -- ① 8 业务域角色改回：Editor→Staff、Full→Director
                UPDATE [AspNetRoles] SET [Name] = REPLACE([Name], N'Editor', N'Staff') WHERE [Name] LIKE N'%Editor';
                UPDATE [AspNetRoles] SET [Name] = REPLACE([Name], N'Full',   N'Director') WHERE [Name] LIKE N'%Full';
                UPDATE [AspNetRoles] SET [NormalizedName] = UPPER([Name]);
                """);
        }
    }
}
