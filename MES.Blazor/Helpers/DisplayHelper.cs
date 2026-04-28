using MudBlazor;

namespace MES.Blazor.Helpers;

/// <summary>
/// 显示帮助类，提供格式化、枚举文本转换等通用方法
/// </summary>
public static class DisplayHelper
{
    /// <summary>
    /// 格式化 decimal 值，去除末尾无效零
    /// </summary>
    public static string FormatDecimal(decimal value) => value.ToString("G29");

    /// <summary>
    /// 格式化可空 decimal 值，去除末尾无效零
    /// </summary>
    public static string FormatNullableDecimal(decimal? value) => value?.ToString("G29") ?? "";

    /// <summary>
    /// 格式化规格（外径*壁厚），去除数值末尾无效零
    /// </summary>
    public static string FormatSpecification(string specification)
    {
        if (string.IsNullOrEmpty(specification)) return "";
        var parts = specification.Split('*');
        if (parts.Length != 2) return specification;
        var od = decimal.TryParse(parts[0], out var odValue) ? odValue.ToString("G29") : parts[0];
        var wt = decimal.TryParse(parts[1], out var wtValue) ? wtValue.ToString("G29") : parts[1];
        return $"{od}*{wt}";
    }

    /// <summary>
    /// 获取长度状态中文文本
    /// </summary>
    public static string GetLengthStatusText(string status)
    {
        return status switch
        {
            "Fixed" => "定尺",
            "Range" => "范围尺",
            "NonFixed" => "非定尺",
            _ => status
        };
    }

    /// <summary>
    /// 获取交货状态中文文本
    /// </summary>
    public static string GetDeliveryStateText(string state)
    {
        return state switch
        {
            "SolutionAnnealedAndPickled" => "固溶酸洗",
            "SolutionAnnealedAndPickledUTube" => "固溶酸洗-U型管",
            "SolutionAnnealedAndPickledExternalPolished" => "固溶酸洗-外抛光",
            "SolutionAnnealedAndPickledInternalPolished" => "固溶酸洗-内抛光",
            "SolutionAnnealedAndPickledBothPolished" => "固溶酸洗-内外抛光",
            "SolutionAnnealedAndPickledCoiled" => "固溶酸洗-盘管",
            "Bright" => "光亮",
            "BrightUTube" => "光亮-U型管",
            "BrightCoiled" => "光亮-盘管",
            "Hard" => "硬态",
            _ => state
        };
    }

    /// <summary>
    /// 获取物料名称中文文本
    /// </summary>
    public static string GetMaterialNameText(string materialName)
    {
        return materialName switch
        {
            "SeamlessPipe" => "无缝管",
            "WeldedPipe" => "焊管",
            _ => materialName
        };
    }

    /// <summary>
    /// 获取结算方式中文文本
    /// </summary>
    public static string GetSettlementMethodText(string method)
    {
        return method switch
        {
            "Theoretical" => "理算",
            "Weighing" => "过磅",
            "WeighingNegative" => "过磅-负",
            _ => method
        };
    }

    /// <summary>
    /// 获取工单状态对应的颜色
    /// </summary>
    public static Color GetWorkOrderStatusColor(int status)
    {
        return status switch
        {
            0 => Color.Default,
            1 => Color.Success,
            2 => Color.Warning,
            3 => Color.Error,
            _ => Color.Default
        };
    }

    /// <summary>
    /// 获取工单状态中文文本
    /// </summary>
    public static string GetWorkOrderStatusText(int status)
    {
        return status switch
        {
            0 => "未编制",
            1 => "已确定",
            2 => "待修正",
            3 => "已取消",
            _ => "未知"
        };
    }
}
