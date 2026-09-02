namespace MES.Core.Helpers;

/// <summary>
/// 生产计件类别「自动组合名」纯函数（2026-09-02 重构引入，§3.2）。
/// 展示名 = 工段中文 ｜ 产类中文集(空=全部产类) ｜ 工序中文集(空=全部工序) ｜ 阶段中文集(空=全部阶段)。
/// 多值用「·」连接。入参为「已解析的中文值」，调用方（服务/前端）各自负责 Key→中文解析
/// （服务端用配置表/常量兜底，前端用 SectionDisplayHelper/ProcessDisplayHelper 的 OverrideMap）。
/// 不落库，仅作 UI/报表标识。
/// </summary>
public static class CategoryDisplayNameHelper
{
    private const string AllProducts = "全部产类";
    private const string AllProcesses = "全部工序";
    private const string AllStages = "全部阶段";

    /// <summary>
    /// 组合展示名。任一中文集传 null/空集合即按「全选」显示（全部产类/工序/阶段）。
    /// 多值顺序按入参集合顺序拼接（调用方需保证稳定顺序）。
    /// </summary>
    public static string Build(
        string sectionChinese,
        IReadOnlyCollection<string>? productStatusChinese,
        IReadOnlyCollection<string>? processChinese,
        IReadOnlyCollection<string>? stageChinese)
    {
        var prods = Join(productStatusChinese, AllProducts);
        var procs = Join(processChinese, AllProcesses);
        var stages = Join(stageChinese, AllStages);
        return $"{sectionChinese}｜{prods}｜{procs}｜{stages}";
    }

    private static string Join(IReadOnlyCollection<string>? chinese, string allText)
    {
        if (chinese is null || chinese.Count == 0) return allText;
        return string.Join("·", chinese);
    }
}
