// 文件路径: MES.Blazor/Helpers/OrderOverviewFormatter.cs
using Microsoft.AspNetCore.Components;

namespace MES.Blazor.Helpers;

/// <summary>
/// 业务总况（订单接单·出库及现负荷汇总 + 订单交期预估两小表）单元格格式化。
/// 金额口径：订单项次总价折算（结算分治），元，显示换算万元。
/// 无小数整值展示：单/吨/万 全部四舍五入取整，0 值侧显示 0；三者皆 0 显示「-」。
/// 颜色区分（内联样式保证打印 getTableHtml 与屏幕一致）：单=蓝 / 吨=绿 / 万=橙。
/// 报表-业务总况 与 订单列表页 同款小表共用本类，防两处观感漂移。
/// </summary>
public static class OrderOverviewFormatter
{
    private const string CountColor = "#1565C0";   // 蓝：单数
    private const string WeightColor = "#2E7D32";  // 绿：重量吨
    private const string AmountColor = "#E65100";  // 橙：金额万元

    /// <summary>主表格：x吨/y万（重量 kg → 吨取整，金额元 → 万元取整）。</summary>
    public static MarkupString RenderInOutCell(decimal weightKg, decimal amountYuan)
    {
        if (weightKg == 0m && amountYuan == 0m)
            return new MarkupString("-");
        return Build(WeightToken(weightKg / 1000m), AmountToken(amountYuan));
    }

    /// <summary>交期预估两小表格：z单/x吨/y万（Weight 已是吨、Amount 为元）。</summary>
    public static MarkupString RenderEstimateCell(int count, decimal weightTons, decimal amountYuan)
    {
        if (count <= 0 && weightTons == 0m && amountYuan == 0m)
            return new MarkupString("-");
        return Build(CountToken(count), WeightToken(weightTons), AmountToken(amountYuan));
    }

    // ========== 往来统计格（客户/供应商/委外 列表页 ② 列 + 报表「往来数据」卡共用） ==========
    // 语义：0 成分省略；全 0 显「—」；单/吨/万 四舍五入取整（Math.Round AwayFromZero）；
    // 单=蓝 / 吨=绿 / 万=橙（与 ⑤ 色板一致，保证屏幕与 DOM 打印同观感）。
    // weightKg 为 kg、amountYuan 为元；withCount=false 时忽略 count（无单数成分列，如 到货/待收/回收）。

    /// <summary>往来统计格富文本（z单/x吨/y万 三色，吨/万取整，0 成分省略，全 0 显「—」）。</summary>
    public static MarkupString RenderTradeMarkup(int count, decimal weightKg, decimal amountYuan, bool withCount = true)
    {
        var tokens = new List<string>();
        if (withCount && count > 0) tokens.Add(CountToken(count));
        if (weightKg > 0) tokens.Add(WeightToken(weightKg / 1000m));
        if (amountYuan > 0) tokens.Add(AmountToken(amountYuan));
        return tokens.Count > 0 ? Build(tokens.ToArray()) : new MarkupString("—");
    }

    /// <summary>往来统计格纯文本（同 RenderTradeMarkup 数值口径，供 tooltip/合计/打印文本）。</summary>
    public static string RenderTradeText(int count, decimal weightKg, decimal amountYuan, bool withCount = true)
    {
        var parts = new List<string>();
        if (withCount && count > 0) parts.Add($"{count}单");
        if (weightKg > 0) parts.Add($"{Whole(weightKg / 1000m)}吨");
        if (amountYuan > 0) parts.Add($"{Whole(amountYuan / 10000m)}万");
        return parts.Count > 0 ? string.Join("/", parts) : "—";
    }

    private static MarkupString Build(params string[] tokens)
    {
        var html = string.Join("<span style=\"color:#9e9e9e;\">/</span>", tokens.Where(t => !string.IsNullOrEmpty(t)));
        return new MarkupString(html);
    }

    private static string CountToken(int count)
        => Span($"{count}单", CountColor);

    private static string WeightToken(decimal tons)
        => Span($"{Whole(tons)}吨", WeightColor);

    private static string AmountToken(decimal yuan)
        => Span($"{Whole(yuan / 10000m)}万", AmountColor);

    private static string Whole(decimal value)
        => Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString();

    private static string Span(string text, string color)
        => $"<span style=\"color:{color};font-weight:600;\">{text}</span>";
}
