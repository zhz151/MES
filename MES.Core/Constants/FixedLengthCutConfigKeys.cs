namespace MES.Core.Constants;

/// <summary>
/// 定尺联通视图主号「切割偏差」阈值配置键（ConfigParameter 类目 FixedLengthCutRatio）。
/// 主号为汇总数据（多批次聚合），偏差阈值取 3.5%（比批次单批次 CutDoubtRatio 5% 更严）；
/// 参数表保存后由 ConfigParameterService 即时失效定尺列表缓存实时生效（2026-09-08 起由硬编码 5% 改为配置驱动）。
/// </summary>
public static class FixedLengthCutConfigKeys
{
    public const string Category = "FixedLengthCutRatio";

    public const string CutDeviationRatioKey = "CutDeviationRatio";

    /// <summary>默认主号切割偏差阈值（3.5%）：|实切支数−理论已切|/理论已切 超过则判「异常」</summary>
    public const decimal CutDeviationRatioDefault = 0.035m;
}
