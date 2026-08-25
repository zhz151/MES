using System.Collections.Generic;
using MES.Core.Constants;

namespace MES.Core.Helpers;

/// <summary>
/// 字典值显示辅助（前后端共享）：把存储层英文 Key 归一为中文显示。
/// 优先使用配置表加载的 OverrideMap（DictValueDefinition.DictKey，可随配置改名），
/// 否则回退各 Keys 常量类规范中文；空值返回 null（调用方可用 ?? 兜底）；未知值原样返回（不崩）。
/// 工段/工序有各自专门配置表与 Helper（SectionDisplayHelper/ProcessDisplayHelper）；
/// 责任类别已并入字典表（DictValueDefinitions，DictKey=LiabilityTypeKey），本类统一管辖无专门表的字典，
/// 但对 11 个 DictKey 均提供兜底解析以保持通用。
/// 覆盖由启动方注入：API 启动/前端 MainLayout 从 DictValueDefinitionService.GetDisplayMapAsync() 填充 OverrideMap。
/// </summary>
public static class DictValueDisplayHelper
{
    /// <summary>DictKey → Value → DisplayName（由配置表加载，全局共享）</summary>
    public static Dictionary<string, Dictionary<string, string>>? OverrideMap { get; set; }

    /// <summary>字典 Key → 显示中文（配置表优先 → Keys 常量类兜底 → 原样）；空值返回 null</summary>
    public static string? GetText(string dictKey, string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;

        if (OverrideMap != null
            && OverrideMap.TryGetValue(dictKey, out var inner)
            && inner.TryGetValue(value, out var cn))
            return cn;

        return dictKey switch
        {
            DictValueDefaults.SectionKey => SectionKeys.ToChinese(value),
            DictValueDefaults.ProcessKey => ProcessKeys.ToChinese(value),
            DictValueDefaults.UrgencyLevelKey => UrgencyLevelKeys.ToChinese(value),
            DictValueDefaults.ProductStatus => ProductStatuses.ToChinese(value),
            DictValueDefaults.ProductionFlowKey => ProductionFlowKeys.ToChinese(value),
            DictValueDefaults.FlowTargetKey => FlowTargetKeys.ToChinese(value),
            DictValueDefaults.ProductionOverviewRowKey => ProductionOverviewRowKeys.ToChinese(value),
            DictValueDefaults.LiabilityTypeKey => LiabilityTypeKeys.ToChinese(value),
            DictValueDefaults.NcrResponsibilityKey => NcrResponsibilityKeys.ToChinese(value),
            DictValueDefaults.RawMaterialLockRemarkKey => RawMaterialLockRemarkKeys.ToChinese(value),
            DictValueDefaults.ProductionAttentionKey => ProductionAttentionKeys.ToChinese(value),
            _ => value
        } ?? value;
    }
}
