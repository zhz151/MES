using System.Collections.Generic;

namespace MES.Core.Constants;

/// <summary>
/// 字典值默认显示映射聚合：DictKey → Value → DisplayName。
/// 覆盖 string 字典 Keys 常量类（紧急度/产类/流转/关注目标/汇总行/责任类别/NCR责任类别/原锁备注/生产关注/岗位/岗位类别），
/// 供 DictValueDefinitionService 的 GetDisplayMapAsync 兜底与 RestoreDefaultsAsync 恢复默认行使用。
/// DictKey 为该字典在 DictValueDefinition 表中的稳定标识（配置页按此分组管理）。
/// 注意：工段/工序由各自专门配置表（StandardWorkDays/ProcessDefinitions）管理，其映射保留在 All 中
/// 仅供 GetText/display-map 兜底，但不在 DictKeys（配置页下拉）暴露，避免与专门表双入口。
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

    /// <summary>NCR 责任类别（NcrResponsibilityKeys）</summary>
    public const string NcrResponsibilityKey = "NcrResponsibilityKey";

    /// <summary>原锁备注（RawMaterialLockRemarkKeys）</summary>
    public const string RawMaterialLockRemarkKey = "RawMaterialLockRemarkKey";

    /// <summary>生产关注工序特殊值（ProductionAttentionKeys）</summary>
    public const string ProductionAttentionKey = "ProductionAttentionKey";

    /// <summary>岗位（PositionKeys，员工岗位字典化，计件工资按岗位切分）</summary>
    public const string PositionKey = "PositionKey";

    /// <summary>岗位类别（PositionCategoryKeys，员工 Department 字段字典化，集体计件按类别切分岗位工资）</summary>
    public const string PositionCategoryKey = "PositionCategoryKey";

    /// <summary>
    /// 全部字典标识有序列表（配置页下拉/列表用）。
    /// 不含工段/工序（由专门配置表管理，见类注释）。
    /// </summary>
    public static readonly string[] DictKeys =
    [
        UrgencyLevelKey, ProductStatus, ProductionFlowKey, FlowTargetKey,
        ProductionOverviewRowKey, LiabilityTypeKey, NcrResponsibilityKey,
        RawMaterialLockRemarkKey, ProductionAttentionKey, PositionKey, PositionCategoryKey
    ];

    /// <summary>全量：DictKey → Value → DisplayName（工段/工序专门表 + 9 个可配置字典全部内置值）</summary>
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
            [NcrResponsibilityKey] = Copy(NcrResponsibilityKeys.KeyToChinese),
            [RawMaterialLockRemarkKey] = Copy(RawMaterialLockRemarkKeys.KeyToChinese),
            [ProductionAttentionKey] = Copy(ProductionAttentionKeys.KeyToChinese),
            [PositionKey] = Copy(PositionKeys.KeyToChinese),
            [PositionCategoryKey] = Copy(PositionCategoryKeys.KeyToChinese),
        };

    private static IReadOnlyDictionary<string, string> Copy(IReadOnlyDictionary<string, string> source)
    {
        var dict = new Dictionary<string, string>(source.Count, StringComparer.Ordinal);
        foreach (var kvp in source)
            dict[kvp.Key] = kvp.Value;
        return dict;
    }
}
