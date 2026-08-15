namespace MES.Core.Constants;

/// <summary>
/// 生产关注工序特殊值英文稳定 Key 常量。区别于 <see cref="ProcessKeys"/>（9 个工序英文 Key），
/// 本类仅表达关注工序的非工序特殊值：变形工序完成（DeformedProcessCompleted==是，即 rollingSum==0）
/// 时生产关注工序取「生产收尾」（与成品检验衔接的收尾状态，不属于生产工序）。
/// 存储层与后端匹配一律使用英文 Key（"ProductionFinish"），显示层使用中文（生产收尾）。
/// </summary>
public static class ProductionAttentionKeys
{
    /// <summary>生产收尾（变形工序完成后的收尾状态，非工序）</summary>
    public const string Finish = "ProductionFinish";

    /// <summary>Key → 规范中文（显示兜底）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Finish] = "生产收尾",
        };

    /// <summary>是否为关注工序特殊值 Key（Ordinal）</summary>
    public static bool IsKey(string? value)
        => value == Finish;

    /// <summary>归一为显示中文：Key → 中文；未知返回 null。</summary>
    public static string? ToChinese(string? value)
        => value == Finish ? KeyToChinese[value] : null;
}
