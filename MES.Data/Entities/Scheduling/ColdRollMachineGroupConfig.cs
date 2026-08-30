namespace MES.Data.Entities.Scheduling;

/// <summary>
/// 冷轧机台组配置表 —— 冷轧工序（ProcessKeys 英文 Key）归组配置（排程建议/排机估算引擎机台类型组归并输入）。
/// 供需链由 <see cref="SupplyTargetGroupKey"/> 显式表达（2026-08-29 方案 A，组角色字段已移除）：
/// 配置了供给目标组 = 供给方，被别的组指向 = 需求方，每个组天然可同时为供给方与需求方（多级链中间节点），
/// 引擎据此推导，无需独立组角色字段。服务层校验链合法性（供给目标须存在、链无环）。
/// </summary>
public class ColdRollMachineGroupConfig : BaseEntity
{
    /// <summary>组稳定 Key（字母开头仅字母数字下划线，唯一；如 5060/2030/ThreeRoll/Draw）</summary>
    public string GroupKey { get; set; } = "";

    /// <summary>组显示名（如 冷轧5060）</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>组内工序 ProcessKeys（逗号分隔字符串，如 "ColdRoll50,ColdRoll60"；工序全局唯一归属一组）</summary>
    public string? ProcessKeys { get; set; }

    /// <summary>显示顺序（升序，小排前）</summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// 供给目标组 Key（可空）：本组为供给方时指向的下游需求组（如 5060 → "2030"）。
    /// 空 = 非供给方（需求末端或独立池）；允许多条并行链、多级链（A→B→C 时 B 既被指向又指向 C）。
    /// </summary>
    public string? SupplyTargetGroupKey { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}
