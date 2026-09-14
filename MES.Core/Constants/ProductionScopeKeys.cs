namespace MES.Core.Constants;

/// <summary>
/// 投料产出总况报表的「生产类型范围」口径常量。投料来源按生产批次 ProductionType 归类，报表提供两类口径：
/// 1) 合计档：全部（All）= 荒管 + 在制 + 库存 + 外购；纯生产（Pure）= 荒管 + 在制；
/// 2) 单一档：直接沿用 <see cref="ProductionTypeKeys"/> 的四个值（RoughTube / InProcess / Inventory /
///    OutsourcedPurchased），单选某一个生产类型看其投料产出。
/// 与 <see cref="ProductionTypeKeys.All"/> 的区别：后者为全部合法值（含返整/委外生产/对外加工），
/// 报表口径一律排除这三类（与订单进度树「生产投料」叶同一拍板）。
/// </summary>
public static class ProductionScopeKeys
{
    /// <summary>全部四种生产类型（荒管 + 在制 + 库存 + 外购）——默认口径</summary>
    public const string All = "All";

    /// <summary>纯生产两种生产类型（荒管 + 在制）</summary>
    public const string Pure = "Pure";

    /// <summary>「全部」口径的生产类型集合（DB 字符串比较）</summary>
    public static readonly string[] AllTypes =
    [
        ProductionTypeKeys.RoughTube,
        ProductionTypeKeys.InProcess,
        ProductionTypeKeys.Inventory,
        ProductionTypeKeys.OutsourcedPurchased,
    ];

    /// <summary>「纯生产」口径的生产类型集合（DB 字符串比较）</summary>
    public static readonly string[] PureTypes =
    [
        ProductionTypeKeys.RoughTube,
        ProductionTypeKeys.InProcess,
    ];

    /// <summary>单一生产类型口径集合（四个合法值，与 <see cref="AllTypes"/> 同集）</summary>
    public static readonly string[] SingleTypes = AllTypes;

    /// <summary>是否为合法口径 Key（Ordinal：两个合计档或四个单一生产类型）</summary>
    public static bool IsKey(string? scope)
        => scope == All || scope == Pure || Array.IndexOf(AllTypes, scope) >= 0;

    /// <summary>归一为合法 Key：空值/未知值一律回退「全部」</summary>
    public static string Resolve(string? scope)
        => IsKey(scope) ? scope! : All;

    /// <summary>取口径对应的生产类型集合（单一档返回单元素集合）</summary>
    public static string[] TypesOf(string? scope)
    {
        var key = Resolve(scope);
        if (key == Pure) return PureTypes;
        if (key != All) return [key];
        return AllTypes;
    }
}
