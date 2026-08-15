namespace MES.Core.Constants;

/// <summary>
/// 组合归类表通配哨兵 — (工序组, 工段, 产类)三维组合中，工序组/工段支持"全部"通配任意；
/// 产类另有 <see cref="ProductStatuses.AllStatus"/>（不限定产类）。前后端/种子/迁移共用。
/// </summary>
public static class CombinationWildcards
{
    /// <summary>通配任意工序组/工段（存组合归类表 ProcessGroupName/SectionName）</summary>
    public const string All = "全部";
}
