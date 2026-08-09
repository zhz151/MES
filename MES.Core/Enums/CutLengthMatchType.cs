namespace MES.Core.Enums;

/// <summary>
/// 定尺切割长度匹配标识
/// FullMatch   = 完全匹配：成品长度 = 本工单号（订单+主号+次号）的定尺长度
/// MainNoMatch = 主号匹配：成品长度 ∈ 订单+主号 的定尺长度，但非本工单号（补料尾批/返整批跨次号流转）
/// null（不适用）：非成品切割 / 非定尺 / 预成切 / 无成品长度 / 定尺集合为空
/// </summary>
public enum CutLengthMatchType
{
    /// <summary>完全匹配：成品长度 = 本工单号（订单+主号+次号）的定尺长度</summary>
    FullMatch,

    /// <summary>主号匹配：成品长度 ∈ 订单+主号 的定尺长度，但非本工单号</summary>
    MainNoMatch
}
