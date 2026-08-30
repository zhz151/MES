namespace MES.Core.Constants;

/// <summary>
/// 内存缓存默认参数集中定义（各 Service IMemoryCache 统一引用，一处调优全局生效）
/// </summary>
public static class CacheDefaults
{
    /// <summary>内存缓存默认过期时长：筛选上下文等低频数据（原散落 TimeSpan.FromMinutes(5) 统一收口）</summary>
    public static readonly TimeSpan MemoryCacheExpiry = TimeSpan.FromMinutes(5);
}
