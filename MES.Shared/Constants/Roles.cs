namespace MES.Shared.Constants;

public static class Roles
{
    public const string Admin = "Admin";

    // ======================================================================
    // 角色模型：15 主菜单 × 3 档（Viewer=查 / Editor=查增改 / Full=查增改删）+ Admin（隐式全权）
    // 纯一级模型：每个主菜单一个档位，角色 = {菜单前缀}{档位}，共 46 个。
    // 2026-08-26 用户决策：取消全部二级菜单权限，回到纯一级。
    // 2026-09-01 新增「工资结算」主菜单（Salary 三档，考勤表一期）。
    // ======================================================================

    /// <summary>
    /// 14 主菜单 × 3 档角色名（存储于 AspNetRoles / JWT claim）
    /// </summary>
    public static class Menus
    {
        // 订单管理
        public const string OrderViewer = "OrderViewer";
        public const string OrderEditor = "OrderEditor";
        public const string OrderFull = "OrderFull";
        // 工单管理
        public const string WorkOrderViewer = "WorkOrderViewer";
        public const string WorkOrderEditor = "WorkOrderEditor";
        public const string WorkOrderFull = "WorkOrderFull";
        // 计划排程（独立三档）
        public const string SchedulingViewer = "SchedulingViewer";
        public const string SchedulingEditor = "SchedulingEditor";
        public const string SchedulingFull = "SchedulingFull";
        // 批次管理
        public const string BatchViewer = "BatchViewer";
        public const string BatchEditor = "BatchEditor";
        public const string BatchFull = "BatchFull";
        // 质量管理
        public const string QualityViewer = "QualityViewer";
        public const string QualityEditor = "QualityEditor";
        public const string QualityFull = "QualityFull";
        // 物料管理
        public const string MaterialViewer = "MaterialViewer";
        public const string MaterialEditor = "MaterialEditor";
        public const string MaterialFull = "MaterialFull";
        // 仓库管理
        public const string WarehouseViewer = "WarehouseViewer";
        public const string WarehouseEditor = "WarehouseEditor";
        public const string WarehouseFull = "WarehouseFull";
        // 设备管理
        public const string EquipmentViewer = "EquipmentViewer";
        public const string EquipmentEditor = "EquipmentEditor";
        public const string EquipmentFull = "EquipmentFull";
        // 生产标准
        public const string StandardViewer = "StandardViewer";
        public const string StandardEditor = "StandardEditor";
        public const string StandardFull = "StandardFull";
        // 报表系统
        public const string ReportViewer = "ReportViewer";
        public const string ReportEditor = "ReportEditor";
        public const string ReportFull = "ReportFull";
        // 数据工具
        public const string DataToolViewer = "DataToolViewer";
        public const string DataToolEditor = "DataToolEditor";
        public const string DataToolFull = "DataToolFull";
        // 扫码管理（独立三档）
        public const string ScanViewer = "ScanViewer";
        public const string ScanEditor = "ScanEditor";
        public const string ScanFull = "ScanFull";
        // 工资结算（独立三档，考勤表一期）
        public const string SalaryViewer = "SalaryViewer";
        public const string SalaryEditor = "SalaryEditor";
        public const string SalaryFull = "SalaryFull";
        // 参数表
        public const string ConfigurationViewer = "ConfigurationViewer";
        public const string ConfigurationEditor = "ConfigurationEditor";
        public const string ConfigurationFull = "ConfigurationFull";
        // 用户管理
        public const string UserViewer = "UserViewer";
        public const string UserEditor = "UserEditor";
        public const string UserFull = "UserFull";
    }

    /// <summary>
    /// 常用角色组合策略 — 统一管理 [Authorize(Roles = "...")] 中的字符串。
    /// 三档模型：View（查）/ Edit（查增改，含审批/刷新）/ Delete（查增改删）。
    /// Admin 隐式全权，所有策略尾部固定 ",Admin"。
    /// 6 个报表数据域（Order/WorkOrder/Batch/Quality/Material/Warehouse/Scheduling）端点读含 Report 角色，
    /// 菜单门控用 XxxMenu（不含 Report 角色），避免报表用户看到数据域菜单。
    /// </summary>
    public static class Policies
    {
        // ========== 报表数据域（端点读含 Report 角色） ==========

        public const string OrderView = "OrderViewer,OrderEditor,OrderFull,ReportViewer,ReportEditor,ReportFull,Admin";
        public const string OrderEdit = "OrderEditor,OrderFull,Admin";
        public const string OrderDelete = "OrderFull,Admin";
        public const string OrderMenu = "OrderViewer,OrderEditor,OrderFull,Admin";

        public const string WorkOrderView = "WorkOrderViewer,WorkOrderEditor,WorkOrderFull,ReportViewer,ReportEditor,ReportFull,Admin";
        public const string WorkOrderEdit = "WorkOrderEditor,WorkOrderFull,Admin";
        public const string WorkOrderDelete = "WorkOrderFull,Admin";
        public const string WorkOrderMenu = "WorkOrderViewer,WorkOrderEditor,WorkOrderFull,Admin";

        public const string BatchView = "BatchViewer,BatchEditor,BatchFull,ReportViewer,ReportEditor,ReportFull,Admin";
        public const string BatchEdit = "BatchEditor,BatchFull,Admin";
        public const string BatchDelete = "BatchFull,Admin";
        public const string BatchMenu = "BatchViewer,BatchEditor,BatchFull,Admin";

        public const string QualityView = "QualityViewer,QualityEditor,QualityFull,ReportViewer,ReportEditor,ReportFull,Admin";
        public const string QualityEdit = "QualityEditor,QualityFull,Admin";
        public const string QualityDelete = "QualityFull,Admin";
        public const string QualityMenu = "QualityViewer,QualityEditor,QualityFull,Admin";

        public const string MaterialView = "MaterialViewer,MaterialEditor,MaterialFull,ReportViewer,ReportEditor,ReportFull,Admin";
        public const string MaterialEdit = "MaterialEditor,MaterialFull,Admin";
        public const string MaterialDelete = "MaterialFull,Admin";
        public const string MaterialMenu = "MaterialViewer,MaterialEditor,MaterialFull,Admin";

        public const string WarehouseView = "WarehouseViewer,WarehouseEditor,WarehouseFull,ReportViewer,ReportEditor,ReportFull,Admin";
        public const string WarehouseEdit = "WarehouseEditor,WarehouseFull,Admin";
        public const string WarehouseDelete = "WarehouseFull,Admin";
        public const string WarehouseMenu = "WarehouseViewer,WarehouseEditor,WarehouseFull,Admin";

        public const string SchedulingView = "SchedulingViewer,SchedulingEditor,SchedulingFull,ReportViewer,ReportEditor,ReportFull,Admin";
        public const string SchedulingEdit = "SchedulingEditor,SchedulingFull,Admin";
        public const string SchedulingDelete = "SchedulingFull,Admin";
        public const string SchedulingMenu = "SchedulingViewer,SchedulingEditor,SchedulingFull,Admin";

        // ========== 非报表数据域（端点读不含 Report 角色） ==========

        public const string EquipmentView = "EquipmentViewer,EquipmentEditor,EquipmentFull,Admin";
        public const string EquipmentEdit = "EquipmentEditor,EquipmentFull,Admin";
        public const string EquipmentDelete = "EquipmentFull,Admin";

        public const string StandardView = "StandardViewer,StandardEditor,StandardFull,Admin";
        public const string StandardEdit = "StandardEditor,StandardFull,Admin";
        public const string StandardDelete = "StandardFull,Admin";

        public const string ConfigurationView = "ConfigurationViewer,ConfigurationEditor,ConfigurationFull,Admin";
        public const string ConfigurationEdit = "ConfigurationEditor,ConfigurationFull,Admin";
        public const string ConfigurationDelete = "ConfigurationFull,Admin";

        // ========== 独立菜单 ==========
        public const string ReportView = "ReportViewer,ReportEditor,ReportFull,Admin";
        public const string ReportEdit = "ReportEditor,ReportFull,Admin";
        public const string ReportDelete = "ReportFull,Admin";

        public const string DataToolView = "DataToolViewer,DataToolEditor,DataToolFull,Admin";
        public const string DataToolEdit = "DataToolEditor,DataToolFull,Admin";
        public const string DataToolDelete = "DataToolFull,Admin";

        public const string ScanView = "ScanViewer,ScanEditor,ScanFull,Admin";
        public const string ScanEdit = "ScanEditor,ScanFull,Admin";
        public const string ScanDelete = "ScanFull,Admin";

        public const string SalaryView = "SalaryViewer,SalaryEditor,SalaryFull,Admin";
        public const string SalaryEdit = "SalaryEditor,SalaryFull,Admin";
        public const string SalaryDelete = "SalaryFull,Admin";

        public const string UserView = "UserViewer,UserEditor,UserFull,Admin";
        public const string UserEdit = "UserEditor,UserFull,Admin";
        public const string UserDelete = "UserFull,Admin";

        // ========== 跨域组合 ==========
        /// <summary>订单成品(实时库存)（订单 + 质量 数据源并集；list/header-options 供质保书创建页 Quality 角色调用）</summary>
        public const string PendingDeliveryView = "OrderViewer,OrderEditor,OrderFull,QualityViewer,QualityEditor,QualityFull,Admin";
        /// <summary>冷轧机台数配置（参数表 Configuration 域 + 计划排程 Scheduling 域 并集：冷轧排程页内嵌维护，排程人员可随时改机台数/估算日产）</summary>
        public const string ColdRollMachineConfigView = "ConfigurationViewer,ConfigurationEditor,ConfigurationFull,SchedulingViewer,SchedulingEditor,SchedulingFull,ReportViewer,ReportEditor,ReportFull,Admin";
        /// <summary>冷轧机台数配置新增/更新（ConfigurationEditor/Full + SchedulingEditor/Full）</summary>
        public const string ColdRollMachineConfigEdit = "ConfigurationEditor,ConfigurationFull,SchedulingEditor,SchedulingFull,Admin";
        /// <summary>冷轧机台数配置删除（ConfigurationFull + SchedulingFull）</summary>
        public const string ColdRollMachineConfigDelete = "ConfigurationFull,SchedulingFull,Admin";
        /// <summary>冷轧机台组配置查看（参数表 Configuration 域 + 计划排程 Scheduling 域 + 报表 并集：冷轧排程引擎归组配置，排程人员可维护工序归组）</summary>
        public const string ColdRollMachineGroupConfigView = "ConfigurationViewer,ConfigurationEditor,ConfigurationFull,SchedulingViewer,SchedulingEditor,SchedulingFull,ReportViewer,ReportEditor,ReportFull,Admin";
        /// <summary>冷轧机台组配置新增/更新（ConfigurationEditor/Full + SchedulingEditor/Full）</summary>
        public const string ColdRollMachineGroupConfigEdit = "ConfigurationEditor,ConfigurationFull,SchedulingEditor,SchedulingFull,Admin";
        /// <summary>冷轧机台组配置删除（ConfigurationFull + SchedulingFull）</summary>
        public const string ColdRollMachineGroupConfigDelete = "ConfigurationFull,SchedulingFull,Admin";
        /// <summary>批次计划汇总/月度/委外在产（批次域生产记录+工段委外页 + 排程批次计划页 + 报表总览）</summary>
        public const string BatchPlanSummaryView = "BatchViewer,BatchEditor,BatchFull,SchedulingViewer,SchedulingEditor,SchedulingFull,ReportViewer,ReportEditor,ReportFull,Admin";
    }

    public static string[] GetAllRoles()
    {
        return new[]
        {
            Admin,
            // 订单管理
            Menus.OrderViewer, Menus.OrderEditor, Menus.OrderFull,
            // 工单管理
            Menus.WorkOrderViewer, Menus.WorkOrderEditor, Menus.WorkOrderFull,
            // 计划排程
            Menus.SchedulingViewer, Menus.SchedulingEditor, Menus.SchedulingFull,
            // 批次管理
            Menus.BatchViewer, Menus.BatchEditor, Menus.BatchFull,
            // 质量管理
            Menus.QualityViewer, Menus.QualityEditor, Menus.QualityFull,
            // 物料管理
            Menus.MaterialViewer, Menus.MaterialEditor, Menus.MaterialFull,
            // 仓库管理
            Menus.WarehouseViewer, Menus.WarehouseEditor, Menus.WarehouseFull,
            // 设备管理
            Menus.EquipmentViewer, Menus.EquipmentEditor, Menus.EquipmentFull,
            // 生产标准
            Menus.StandardViewer, Menus.StandardEditor, Menus.StandardFull,
            // 报表系统
            Menus.ReportViewer, Menus.ReportEditor, Menus.ReportFull,
            // 数据工具
            Menus.DataToolViewer, Menus.DataToolEditor, Menus.DataToolFull,
            // 扫码管理
            Menus.ScanViewer, Menus.ScanEditor, Menus.ScanFull,
            // 工资结算
            Menus.SalaryViewer, Menus.SalaryEditor, Menus.SalaryFull,
            // 参数表
            Menus.ConfigurationViewer, Menus.ConfigurationEditor, Menus.ConfigurationFull,
            // 用户管理
            Menus.UserViewer, Menus.UserEditor, Menus.UserFull,
        };
    }
}
