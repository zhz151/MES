using MES.Core.Constants;
using MES.Core.Enums;
using MES.Data.Entities.Batch;

namespace MES.Services.Helpers;

/// <summary>
/// 产类计算帮助类，统一"生产记录""去油酸洗""工单委外""过程检验"的 ProductStatus 计算逻辑。
/// </summary>
public static class ProductStatusHelper
{
    /// <summary>
    /// 计算产类（荒管/在制/成品）
    /// </summary>
    /// <param name="finishedSpec">成品规格（= 批次 Specification）。成品判定标准：制造规格 == 成品规格。</param>
    public static string Calculate(
        string processName,
        string? manufacturingSpec,
        string? batchManufacturingItem,
        List<ProcessGroup> processGroups,
        string? finishedSpec = null)
    {
        // 确定最后一个工序名（仅供"在制修检→荒管"判定使用）
        var lastProcessName = processGroups
            .OrderByDescending(pg => pg.SequenceNumber)
            .Select(pg => pg.ProcessName)
            .FirstOrDefault();

        // 成品（优先级最高）：制造规格 == 成品规格 且 制造物品含"成品"含义
        // 注意：此检查必须在荒管检查之前。
        // 成品工序组 = ManufacturingSpec == 成品规格 的工序组（可能多个，含"附加成检"），不再按 SequenceNumber 最大判定
        if (finishedSpec != null && string.Equals(manufacturingSpec, finishedSpec, StringComparison.OrdinalIgnoreCase)
            && IsFinishedManufacturingItem(batchManufacturingItem))
            return "成品";

        // 荒管：工序名称为"荒管处理"
        if (processName == ProcessNames.RoughTubeProcessing)
            return "荒管";

        // 荒管：工序名称为"在制修检"，非末道工序，且批次有荒管处理工序组，规格匹配
        // 注意：末道工序绝不可能是"荒管"（规则3），所以必须排除 lastProcessName
        if (processName == ProcessNames.InProcessRepair
            && processName != lastProcessName)
        {
            var hasRoughTube = processGroups.Any(pg => pg.ProcessName == ProcessNames.RoughTubeProcessing);
            if (hasRoughTube)
            {
                var roughTubeSpec = processGroups
                    .Where(pg => pg.ProcessName == ProcessNames.RoughTubeProcessing)
                    .Select(pg => pg.ManufacturingSpec)
                    .FirstOrDefault();
                if (roughTubeSpec != null && manufacturingSpec == roughTubeSpec)
                    return "荒管";
            }
        }

        // 默认：在制
        return "在制";
    }

    /// <summary>
    /// 判断制造物品是否属于"成品"类别（OrderFinished/Finished/SpecialDeliveryStatus）
    /// </summary>
    public static bool IsFinishedManufacturingItem(string? manufacturingItem) => manufacturingItem switch
    {
        nameof(MaterialType.OrderFinished) => true,
        nameof(MaterialType.Finished) => true,
        nameof(MaterialType.CriticalFinished) => true,
        nameof(MaterialType.SpecialDeliveryStatus) => true,
        _ => false
    };
}
