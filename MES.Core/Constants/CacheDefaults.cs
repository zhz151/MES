namespace MES.Core.Constants;

/// <summary>
/// 内存缓存默认参数集中定义（各 Service IMemoryCache 统一引用，一处调优全局生效）
/// </summary>
public static class CacheDefaults
{
    /// <summary>内存缓存默认过期时长：筛选上下文等低频数据（原散落 TimeSpan.FromMinutes(5) 统一收口）</summary>
    public static readonly TimeSpan MemoryCacheExpiry = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 定尺联通视图列表缓存兜底过期时长（60 秒，2026-09-08）：
    /// 数据源写路径已即时失效（断切生产记录/正式尺寸成检/工单 FixedLength 行重建/订单类成品入库），
    /// 60 秒仅兜底「执行摘要快照(WorkOrderExecutionSummary)/批次计划(ProductionBatches 理论量·CutRequirement)」等未接失效源的漏网窗口，
    /// 缩短默认 5 分钟以免漏网点造成回页长期旧值；代价为离开页面超 60 秒后回看会触发一次全量聚合重查。
    /// </summary>
    public static readonly TimeSpan FixedLengthListExpiry = TimeSpan.FromSeconds(60);
}
