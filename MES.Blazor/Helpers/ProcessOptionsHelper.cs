using MES.Blazor.Services;
using MES.Core.Constants;
using MES.Core.DTOs.Configuration;

namespace MES.Blazor.Helpers;

/// <summary>
/// 工序下拉选项辅助：配置表驱动（ProcessDefinitionService.GetEnabledProcessesAsync），
/// 失败/空时兜底预置 9 工序。选项 Value=英文 Key，Text=中文（ProcessName）。
/// </summary>
public static class ProcessOptionsHelper
{
    /// <summary>
    /// 加载启用工序选项（IsEnabled=true，按 DisplayOrder 排序）。
    /// 配置表失败或空时降级为预置 9 工序（ProcessKeys + ProcessNames）。
    /// </summary>
    public static async Task<List<ProcessInfoDto>> LoadAsync(ProcessDefinitionService svc)
    {
        var r = await svc.GetEnabledProcessesAsync();
        if (r.Success && r.Data != null && r.Data.Count > 0)
            return r.Data;
        return GetFallbackOptions();
    }

    /// <summary>预置 9 工序兜底选项（顺序同 ProcessKeys.All）</summary>
    public static List<ProcessInfoDto> GetFallbackOptions()
    {
        var list = new List<ProcessInfoDto>(ProcessKeys.All.Length);
        for (int i = 0; i < ProcessKeys.All.Length; i++)
        {
            var key = ProcessKeys.All[i];
            list.Add(new ProcessInfoDto
            {
                ProcessKey = key,
                ProcessName = ProcessKeys.ToChinese(key) ?? key,
                DisplayOrder = i + 1,
                IsEnabled = true,
                IsColdRoll = ProcessKeys.IsColdRoll(key),
                IsColdDraw = key == ProcessKeys.ColdDraw,
                DefaultSections = _defaultSectionsByKey.TryGetValue(key, out var secs) ? secs.ToList() : null
            });
        }
        return list;
    }

    /// <summary>
    /// 预置工序默认工段（SectionKey 列表，与 DbInitializer 种子一致，供配置表加载失败时兜底驱动计划页默认工段）。
    /// AdditionalFinalInspection 无默认工段。
    /// </summary>
    private static readonly Dictionary<string, string[]> _defaultSectionsByKey = new(StringComparer.OrdinalIgnoreCase)
    {
        [ProcessKeys.RoughTubeProcessing] = ["Straighten", "Cut", "Pickle", "OuterPolish", "OuterSpotGrinding", "Inspection"],
        [ProcessKeys.InProcessRepair] = ["Solution", "Straighten", "Cut", "Pickle", "Inspection"],
        [ProcessKeys.ColdRoll60] = ["ColdRollDraw", "OilPipeCut", "Degrease", "Solution", "Straighten", "Cut", "Pickle", "Inspection"],
        [ProcessKeys.ColdRoll50] = ["ColdRollDraw", "OilPipeCut", "Degrease", "Solution", "Straighten", "Cut", "Pickle", "Inspection"],
        [ProcessKeys.ColdRoll30] = ["ColdRollDraw", "OilPipeCut", "Degrease", "Solution", "Straighten", "Cut", "Pickle", "Inspection"],
        [ProcessKeys.ColdRoll20] = ["ColdRollDraw", "OilPipeCut", "Degrease", "Solution", "Straighten", "Cut", "Pickle", "Inspection"],
        [ProcessKeys.ThreeRollColdRoll] = ["ColdRollDraw", "OilPipeCut", "Degrease", "Solution", "Straighten", "Cut", "Pickle", "Inspection"],
        [ProcessKeys.ColdDraw] = ["ColdRollDraw", "Solution", "Straighten", "Cut", "Pickle", "Inspection"],
    };
}
