using System.Text.Json;

namespace MES.Core.Helpers;

/// <summary>
/// 生产计件类别 4 键约束（工序/产类/作业阶段）JSON 数组的序列化与匹配纯函数（2026-09-02 重构引入）。
/// 存储规则：DB 存英文 Key 的 JSON 数组字符串；null（或空数组）语义 = 全选；
/// 「显式全列表」与「空」是两种等价形态，落库前须归一为 null（见 §3.1），否则禁交集会把「全选」与「显式全列」误判为不相交。
/// 一切内存比较使用 StringComparer.OrdinalIgnoreCase，禁止对 JSON 字符串做裸 Contains。
/// </summary>
public static class PieceRateJsonKeys
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>把 JSON 数组字符串解析为去重集合（忽略大小写）；null/空 → 空集合（语义=全选）。</summary>
    public static HashSet<string> Deserialize(string? json)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return set;
        try
        {
            var arr = JsonSerializer.Deserialize<string[]>(json, JsonOpts);
            if (arr == null) return set;
            foreach (var item in arr)
            {
                if (!string.IsNullOrWhiteSpace(item))
                    set.Add(item.Trim());
            }
        }
        catch (JsonException)
        {
            // 非 JSON 残值防御：原样单元素返回，避免吞掉数据
            set.Add(json.Trim());
        }
        return set;
    }

    /// <summary>
    /// 序列化并归一：空集合 → null（全选）；非空集合若与 fullDomain 全等（忽略大小写）→ null（显式全列表归一为全选）；
    /// 否则存为去重排序的 JSON 数组。返回 null 即「空=全选」。
    /// </summary>
    public static string? SerializeNormalized(IEnumerable<string>? keys, IEnumerable<string> fullDomain)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (keys != null)
        {
            foreach (var key in keys)
            {
                if (!string.IsNullOrWhiteSpace(key))
                    set.Add(key.Trim());
            }
        }

        // 空集合 = 全选
        if (set.Count == 0) return null;

        // 显式全列表 = 全选（须与空形态统一）
        var domain = fullDomain.Where(d => !string.IsNullOrWhiteSpace(d)).ToArray();
        if (domain.Length > 0)
        {
            var domainSet = new HashSet<string>(domain, StringComparer.OrdinalIgnoreCase);
            if (domainSet.Count == set.Count && domainSet.IsSubsetOf(set))
                return null;
        }

        var ordered = set.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        return JsonSerializer.Serialize(ordered);
    }

    /// <summary>
    /// 键约束是否包含某值：null/空集合 = 全选 → 恒 true；否则要求 value 非空且 OrdinalIgnoreCase 命中。
    /// </summary>
    public static bool ContainsKey(HashSet<string>? keys, string? value)
    {
        if (keys is null || keys.Count == 0) return true;
        return value != null && keys.Contains(value);
    }
}
