using System.Collections.Generic;

namespace MES.Core.Constants;

/// <summary>
/// 字典值默认显示映射聚合：DictKey → Value → DisplayName。
/// 覆盖 8 个 string 字典 Keys 常量类（工段/工序/紧急度/产类/流转/关注目标/汇总行/责任类别），
/// 供 DictValueDefinitionService 的 GetDisplayMapAsync 兜底与 RestoreDefaultsAsync 恢复默认行使用。
/// DictKey 为该字典在 DictValueDefinition 表中的稳定标识（配置页按此分组管理）。
/// </summary>
public static class DictValueDefaults
{
    // ========== 字典标识（DictKey）常量 ==========
    /// <summary>工段（SectionKeys）</summary>
    public const string SectionKey = "SectionKey";

    /// <summary>工序（ProcessKeys）</summary>
    public const string ProcessKey = "ProcessKey";

    /// <summary>紧急度（UrgencyLevelKeys）</summary>
    public const string UrgencyLevelKey = "UrgencyLevelKey";

    /// <summary>产类（ProductStatuses）</summary>
    public const string ProductStatus = "ProductStatus";

    /// <summary>流转（ProductionFlowKeys）</summary>
    public const string ProductionFlowKey = "ProductionFlowKey";

    /// <summary>关注目标（FlowTargetKeys）</summary>
    public const string FlowTargetKey = "FlowTargetKey";

    /// <summary>汇总行（ProductionOverviewRowKeys）</summary>
    public const string ProductionOverviewRowKey = "ProductionOverviewRowKey";

    /// <summary>责任类别（LiabilityTypeKeys）</summary>
    public const string LiabilityTypeKey = "LiabilityTypeKey";

    /// <summary>全部字典标识有序列表（配置页下拉/列表用）</summary>
    public static readonly string[] DictKeys =
    [
        SectionKey, ProcessKey, UrgencyLevelKey, ProductStatus, ProductionFlowKey,
        FlowTargetKey, ProductionOverviewRowKey, LiabilityTypeKey
    ];

    /// <summary>全量：DictKey → Value → DisplayName（含 8 个字典全部内置值）</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> All =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            [SectionKey] = Copy(SectionKeys.KeyToChinese),
            [ProcessKey] = Copy(ProcessKeys.KeyToChinese),
            [UrgencyLevelKey] = Copy(UrgencyLevelKeys.KeyToChinese),
            [ProductStatus] = Copy(ProductStatuses.KeyToChinese),
            [ProductionFlowKey] = Copy(ProductionFlowKeys.KeyToChinese),
            [FlowTargetKey] = Copy(FlowTargetKeys.KeyToChinese),
            [ProductionOverviewRowKey] = Copy(ProductionOverviewRowKeys.KeyToChinese),
            [LiabilityTypeKey] = Copy(LiabilityTypeKeys.KeyToChinese),
        };

    private static IReadOnlyDictionary<string, string> Copy(IReadOnlyDictionary<string, string> source)
    {
        var dict = new Dictionary<string, string>(source.Count, StringComparer.Ordinal);
        foreach (var kvp in source)
            dict[kvp.Key] = kvp.Value;
        return dict;
    }
}
