using System.Text.RegularExpressions;

namespace MES.Core.Helpers;

/// <summary>
/// 启用员工快照：批量校验预加载一次，避免逐行 N+1 查询。
/// 姓名/工号均大小写不敏感（SQL Server 默认 collation 也不敏感，内存比对需对齐）。
/// </summary>
public sealed class ActiveEmployeeSet
{
    /// <summary>启用员工姓名集合（去重）</summary>
    public HashSet<string> Names { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>工号(Code) → 姓名(Name)</summary>
    public Dictionary<string, string> ByCode { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 操作人显示串 拆分/解析/匹配/格式化 纯函数（无 DB 依赖，便于单元测试）。
/// 存储格式与扫码一致：多人用「、」连接，每人「姓名(工号)」半角括号。
/// </summary>
public static class OperatorNameHelper
{
    /// <summary>匹配「姓名(工号)」，工号内不允许再含括号；允许尾部空格</summary>
    private static readonly Regex SegmentRegex = new(@"^(.*?)\(([^()]+)\)\s*$", RegexOptions.Compiled);

    /// <summary>
    /// 按「、」「,」「，」拆分操作人串，剔除空白段；空串/纯空白返回空列表（非空才校验）。
    /// </summary>
    public static List<string> Split(string? operatorText)
    {
        if (string.IsNullOrWhiteSpace(operatorText)) return new List<string>();
        return operatorText
            .Split(new[] { '、', ',', '，' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    /// <summary>
    /// 解析单段。返回格式：姓名(工号) → (name, code)；纯姓名 → (name, null)；空白段返回 false。
    /// 全角括号「（）」不匹配正则 → 按纯姓名处理。
    /// </summary>
    public static bool TryParseSegment(string segment, out string? name, out string? code)
    {
        name = null;
        code = null;
        if (string.IsNullOrWhiteSpace(segment)) return false;
        var s = segment.Trim();
        var m = SegmentRegex.Match(s);
        if (m.Success)
        {
            name = m.Groups[1].Value.Trim();
            code = m.Groups[2].Value.Trim();
            return name.Length > 0;
        }
        name = s;
        return true;
    }

    /// <summary>统一格式化为「姓名(工号)」半角括号（与扫码一致）</summary>
    public static string Format(string name, string code) => $"{name}({code})";

    /// <summary>
    /// 返回操作人串中未命中启用员工的段列表（全命中返回空列表）。
    /// 规则：姓名(工号) 要求工号命中启用员工 且 姓名与工号归属同一员工（防串号）；纯姓名要求姓名命中启用员工。
    /// </summary>
    public static List<string> FindUnmatched(ActiveEmployeeSet active, string? operatorText)
    {
        var unmatched = new List<string>();
        foreach (var seg in Split(operatorText))
        {
            if (!TryParseSegment(seg, out var name, out var code)) continue;
            bool ok;
            if (code != null)
            {
                // 「姓名(工号)」：工号必须命中启用员工，且姓名与工号归属一致（防串号）
                ok = active.ByCode.TryGetValue(code, out var realName)
                     && string.Equals(realName, name, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                // 纯姓名：必须命中启用员工姓名
                ok = name != null && active.Names.Contains(name);
            }
            if (!ok) unmatched.Add(seg);
        }
        return unmatched;
    }

    /// <summary>
    /// 操作人显示串 → 纯姓名串（去掉工号）：「张燕平(10086)、赵路陈」→「张燕平、赵路陈」。
    /// 列表/卡片展示专用，保留「、」分隔；纯姓名段原样保留。
    /// </summary>
    public static string ToNamesOnly(string? operatorText)
    {
        if (string.IsNullOrWhiteSpace(operatorText)) return "";
        return string.Join("、", Split(operatorText)
            .Select(seg => TryParseSegment(seg, out var name, out _) && !string.IsNullOrEmpty(name) ? name! : seg));
    }
}
