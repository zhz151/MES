using MES.Shared.Constants;

namespace MES.Blazor.Shared;

/// <summary>
/// 权威菜单树（单一数据源）。结构与顺序以电脑版 MainLayout 菜单为准（2026-09-05 对齐），
/// 桌面与手机共用，杜绝手写双份漂移（历史：手机订单组残留「牌号对照」、缺理化检测/
/// 工资结算/用户管理等）。
///
/// 改动菜单只许改这里；新增/删除项请同步补 AppMenuTests 断言，防止回归。
/// 约定：Policy 用于组级或叶子级角色过滤；扫码组整组无 Policy（仅需登录），其下「工位/员工」单独带 ScanView。
/// </summary>
public static class AppMenu
{
    public static readonly IReadOnlyList<AppMenuNode> Root =
    [
        // ─── 首页（仅需登录即可见，精确匹配 /）───
        new() { Label = "首页", Href = "/", MatchAll = true },

        // ─── 订单管理 ───
        new() { Label = "订单管理", Policy = Roles.Policies.OrderMenu, Children =
        [
            new() { Label = "订单列表", Href = "/orders" },
            new() { Label = "客户管理", Href = "/customers" },
            new() { Label = "订单成品(在库)", Href = "/orders/pending-delivery" },
        ] },

        // ─── 工单管理 ───
        new() { Label = "工单管理", Policy = Roles.Policies.WorkOrderMenu, Children =
        [
            new() { Label = "工单首页", Href = "/workorders" },
            new() { Label = "用料计划总览", Href = "/material-plan-overview" },
            new() { Label = "工单需求调整", Href = "/workorders-demand-adjustment" },
            new() { Label = "工单执行状况", Href = "/workorder-execution" },
            new() { Label = "定尺工单定尺数据", Href = "/fixed-length-work-order-view" },
        ] },

        // ─── 计划排程 ───
        new() { Label = "计划排程", Policy = Roles.Policies.SchedulingMenu, Children =
        [
            new() { Label = "订单负荷总量", Href = "/plan-overview" },
            new() { Label = "工单排程", Href = "/scheduling-plans" },
            new() { Label = "冷轧排程", Href = "/cold-roll-plans" },
            new() { Label = "批次计划", Href = "/batch-plans" },
            new() { Label = "成检计划", Href = "/final-inspection-plan" },
        ] },

        // ─── 批次管理 ───
        new() { Label = "批次管理", Policy = Roles.Policies.BatchMenu, Children =
        [
            new() { Label = "批次首页", Href = "/batches" },
            new() { Label = "生产记录", Href = "/production-records" },
            new() { Label = "去油酸洗", Href = "/pickling-in-records" },
            new() { Label = "工段委外", Href = "/section-outsources" },
            new() { Label = "工艺卡打印", Href = "/process-card-print" },
        ] },

        // ─── 质量管理（含两级嵌套子组：炉号/化学、理化检测）───
        new() { Label = "质量管理", Policy = Roles.Policies.QualityMenu, Children =
        [
            new() { Label = "过程检验", Href = "/quality/process-inspection" },
            new() { Label = "成检到料", Href = "/quality/material-receive-checks" },
            new() { Label = "成品检验", Href = "/quality/final-inspection" },
            new() { Label = "成检追踪", Href = "/quality/process-tracking" },
            new() { Label = "不合格报告", Href = "/quality/ncr" },
            new() { Label = "炉号/化学", Children =
            [
                new() { Label = "炉号登记", Href = "/quality/furnace" },
            ] },
            new() { Label = "理化检测", Children =
            [
                new() { Label = "化学检验", Href = "/quality/chemical-analysis" },
                new() { Label = "硬度检验", Href = "/quality/hardness-test" },
                new() { Label = "晶粒度检验", Href = "/quality/grain-size-test" },
                new() { Label = "点腐蚀检验", Href = "/quality/pitting-corrosion-test" },
                new() { Label = "晶间腐蚀检验", Href = "/quality/intergranular-corrosion-test" },
                new() { Label = "室温拉伸检验", Href = "/quality/tensile-test" },
                new() { Label = "金相检验", Href = "/quality/metallographic-test" },
                new() { Label = "压扁检验", Href = "/quality/flattening-test" },
                new() { Label = "扩口检验", Href = "/quality/flaring-test" },
            ] },
            new() { Label = "质量证明书", Href = "/quality/certificates" },
        ] },

        // ─── 物料管理（含嵌套子组：圆棒穿孔）───
        new() { Label = "物料管理", Policy = Roles.Policies.MaterialMenu, Children =
        [
            new() { Label = "采购订单", Href = "/purchase-orders" },
            new() { Label = "圆棒穿孔", Children =
            [
                new() { Label = "圆棒穿孔", Href = "/subcontract-orders" },
                new() { Label = "子项查询", Href = "/subcontract-return-items" },
            ] },
            new() { Label = "供应商管理", Href = "/suppliers" },
        ] },

        // ─── 仓库管理 ───
        new() { Label = "仓库管理", Policy = Roles.Policies.WarehouseMenu, Children =
        [
            new() { Label = "原料库", Href = "/warehouse/raw" },
            new() { Label = "成品库", Href = "/warehouse/fg" },
            new() { Label = "在制品库", Href = "/warehouse/wip" },
            new() { Label = "次品库", Href = "/warehouse/defect" },
            new() { Label = "物料进出存报表", Href = "/warehouse/monthly-stock" },
        ] },

        // ─── 设备管理 ───
        new() { Label = "设备管理", Policy = Roles.Policies.EquipmentView, Children =
        [
            new() { Label = "设备台账", Href = "/equipment" },
            new() { Label = "维修工单", Href = "/repair-orders" },
            new() { Label = "保养工单", Href = "/maintenance-orders" },
            new() { Label = "点检记录", Href = "/inspection-records" },
        ] },

        // ─── 生产标准 ───
        new() { Label = "生产标准", Policy = Roles.Policies.StandardView, Children =
        [
            new() { Label = "标准号列表", Href = "/standard-registers" },
            new() { Label = "标准号检验项要求", Href = "/standard-inspection-requirements" },
            new() { Label = "子标准速览", Href = "/sub-standard-quick-views" },
            new() { Label = "牌号对照", Href = "/grade-mappings" },
            new() { Label = "标准牌号化学成分", Href = "/grade-chemical-compositions" },
            new() { Label = "牌号物理性能", Href = "/grade-physical-properties" },
            new() { Label = "工厂检验项要求", Href = "/factory-inspection-requirements" },
            new() { Label = "工厂牌号化学成分", Href = "/chemical-composition" },
            new() { Label = "工厂牌号化分验证", Href = "/chemical-validate" },
        ] },

        // ─── 报表系统 ───
        new() { Label = "报表系统", Policy = Roles.Policies.ReportView, Children =
        [
            new() { Label = "报表总览", Href = "/reports/overview" },
        ] },

        // ─── 数据工具（单项，非分组）───
        new() { Label = "数据工具", Href = "/data-exchange", Policy = Roles.Policies.DataToolView },

        // ─── 扫码管理（整组仅需登录；工位/员工单独 ScanView 档）───
        new() { Label = "扫码管理", Children =
        [
            new() { Label = "扫码报工", Href = "/mobile-report" },
            new() { Label = "设备扫码", Href = "/equipment-scan" },
            new() { Label = "工位管理", Href = "/workstations", Policy = Roles.Policies.ScanView },
            new() { Label = "员工管理", Href = "/employees", Policy = Roles.Policies.ScanView },
        ] },

        // ─── 工资结算 ───
        new() { Label = "工资结算", Policy = Roles.Policies.SalaryView, Children =
        [
            new() { Label = "生产计件类别", Href = "/payroll/piece-rate-categories" },
            new() { Label = "成检计件类别", Href = "/payroll/final-inspection-categories" },
            new() { Label = "考勤表", Href = "/payroll/attendance" },
            new() { Label = "杂辅工记录", Href = "/payroll/misc-work" },
            new() { Label = "集体计件评分", Href = "/payroll/collective-scores" },
            new() { Label = "津贴与处罚", Href = "/payroll/allowance" },
            new() { Label = "非计件工资", Href = "/payroll/wages/non-piece" },
            new() { Label = "个人计件工资", Href = "/payroll/wages/piece" },
            new() { Label = "集体计件月结", Href = "/payroll/collective-monthly" },
            new() { Label = "靠工计件月结", Href = "/payroll/attendance-monthly" },
            new() { Label = "月工资津贴汇总", Href = "/payroll/monthly-summary" },
        ] },

        // ─── 参数表 ───
        new() { Label = "参数表", Policy = Roles.Policies.ConfigurationView, Children =
        [
            new() { Label = "工序组定义(批次/工艺)", Href = "/process-definitions" },
            new() { Label = "工段工量天数(排程/用料)", Href = "/standard-work-days" },
            new() { Label = "交货状态附加天数(排程/用料)", Href = "/standard-work-day-delivery-states" },
            new() { Label = "规格日产预估(工单执行)", Href = "/daily-output-estimates" },
            new() { Label = "冷轧产能档案(冷轧排程)", Href = "/cold-roll-capacities" },
            new() { Label = "冷轧机台数配置(冷轧排程)", Href = "/cold-roll-machine-configs" },
            new() { Label = "冷轧机台组配置(冷轧排程)", Href = "/cold-roll-machine-group-configs" },
            new() { Label = "重点工段日产(生产总览)", Href = "/daily-production-capacities" },
            new() { Label = "段落日产配置(段落流转)", Href = "/section-paragraph-config-settings" },
            new() { Label = "枚举显示配置(全局显示)", Href = "/enum-display-definitions" },
            new() { Label = "字典显示配置(全局显示)", Href = "/dict-value-definitions" },
            new() { Label = "系统参数(全局参数)", Href = "/config-parameters" },
        ] },

        // ─── 用户管理（单项，非分组）───
        new() { Label = "用户管理", Href = "/admin/users", Policy = Roles.Policies.UserView },
    ];

    /// <summary>深度优先收集所有叶子节点（供测试与通用逻辑使用）。</summary>
    public static IEnumerable<AppMenuNode> AllLeaves()
        => AllLeaves(Root);

    private static IEnumerable<AppMenuNode> AllLeaves(IEnumerable<AppMenuNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsLeaf)
                yield return node;
            else
            {
                foreach (var leaf in AllLeaves(node.Children))
                    yield return leaf;
            }
        }
    }

    /// <summary>按 Label 精确查一组/叶子（供测试断言漂移防护）。</summary>
    public static AppMenuNode? Find(string label)
    {
        foreach (var node in Root)
        {
            if (node.Label == label)
                return node;
            if (!node.IsLeaf)
            {
                var hit = Find(node.Children, label);
                if (hit is not null)
                    return hit;
            }
        }
        return null;
    }

    private static AppMenuNode? Find(IEnumerable<AppMenuNode> nodes, string label)
    {
        foreach (var node in nodes)
        {
            if (node.Label == label)
                return node;
            if (!node.IsLeaf)
            {
                var hit = Find(node.Children, label);
                if (hit is not null)
                    return hit;
            }
        }
        return null;
    }
}
