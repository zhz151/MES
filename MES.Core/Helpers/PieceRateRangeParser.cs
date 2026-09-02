using System.Globalization;
using System.Text.RegularExpressions;

namespace MES.Core.Helpers;

/// <summary>
/// 计件标准维度区间解析器 — 把 Excel 的区间文本（如 "3000≥D>820"、"&lt;2000"、"&gt;16000"、
/// "2001-5000"、"1"、"&gt;3"、"10.5&gt;D"）解析为数值边界 (min, max)。
/// 边界按闭区间处理（≥/＞ mm 级差异业务可忽略）；仅双数字区间与单数字带方向判断。
/// </summary>
public static class PieceRateRangeParser
{
    /// <summary>提取全部数字（含小数）</summary>
    private static readonly Regex NumberRegex = new(@"\d+(?:\.\d+)?", RegexOptions.Compiled);

    /// <summary>纯单向区间：&gt;16000 / &lt;2000</summary>
    private static readonly Regex OpenOpNumber = new(@"^\s*(?<op>[<>≤≥])\s*(?<num>\d+(?:\.\d+)?)\s*$", RegexOptions.Compiled);

    /// <summary>数字在前的单向：10.5&gt;D = D&lt;10.5 → max</summary>
    private static readonly Regex NumberOpLetter = new(@"^\s*(?<num>\d+(?:\.\d+)?)\s*[>≥]\s*[A-Za-z]", RegexOptions.Compiled);

    /// <summary>字母在前的单向：D&lt;13.5 → max；D&gt;52 → min</summary>
    private static readonly Regex LetterOpNumber = new(@"^\s*[A-Za-z]\s*(?<op>[<>≤≥])\s*(?<num>\d+(?:\.\d+)?)\s*$", RegexOptions.Compiled);

    /// <summary>纯数字：断切率 "1"/"2"/"3" → min=max</summary>
    private static readonly Regex PureNumber = new(@"^\s*(?<num>\d+(?:\.\d+)?)\s*$", RegexOptions.Compiled);

    /// <summary>
    /// 解析区间文本为数值边界。返回 false 表示无可解析数值（空/无数字 → 维度未启用）。
    /// </summary>
    public static bool TryParseRange(string? text, out decimal? min, out decimal? max)
    {
        min = null;
        max = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var nums = NumberRegex.Matches(text)
            .Select(m => decimal.Parse(m.Value, CultureInfo.InvariantCulture))
            .ToList();
        if (nums.Count == 0) return false;

        // 双数字及以上 → 双向区间（取最小/最大，闭区间）
        if (nums.Count >= 2)
        {
            min = nums.Min();
            max = nums.Max();
            return true;
        }

        var num = nums[0];

        var m = OpenOpNumber.Match(text);
        if (m.Success)
        {
            if (m.Groups["op"].Value is ">" or "≥") min = num;
            else max = num;
            return true;
        }

        m = NumberOpLetter.Match(text);
        if (m.Success)
        {
            max = num; // 10.5>D → D<10.5
            return true;
        }

        m = LetterOpNumber.Match(text);
        if (m.Success)
        {
            if (m.Groups["op"].Value is ">" or "≥") min = num;
            else max = num;
            return true;
        }

        m = PureNumber.Match(text);
        if (m.Success)
        {
            min = num;
            max = num;
            return true;
        }

        return false;
    }
}
