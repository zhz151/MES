namespace MES.Data.Entities.Configuration;

/// <summary>
/// 字典值配置表：管理所有 string 存储的字典字段（工段/工序/紧急度/产类/流转/关注目标/汇总行/责任类别）
/// 的中文显示名、排序、隐藏与可加值。
/// DictKey = 字典标识，Value = 英文稳定 Key。显示层"配置表优先 → 各 Keys 常量类兜底"。
/// IsEnabled=false 表示隐藏（下拉/筛选不出现，存量数据仍可正确显示映射，不丢数据）。
/// 可新增行（新 Value + DisplayName）实现"加值"，后端业务判定用编译期常量，新 Key 可存/可显示/可筛选。
/// </summary>
public class DictValueDefinition : BaseEntity
{
    /// <summary>字典标识（SectionKey/ProcessKey/UrgencyLevelKey/ProductStatus/ProductionFlowKey/FlowTargetKey/ProductionOverviewRowKey/LiabilityTypeKey）</summary>
    public string DictKey { get; set; } = string.Empty;

    /// <summary>英文稳定 Key（如 "ColdRollDraw"），存储层/后端匹配用，不受改名影响</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>可改名中文显示</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>显示顺序（升序，1 排最前）</summary>
    public int DisplayOrder { get; set; }

    /// <summary>是否启用（false 表示隐藏，下拉/筛选不出现）</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>说明</summary>
    public string? Remark { get; set; }
}
