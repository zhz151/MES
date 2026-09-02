using MES.Core.DTOs.Configuration;

namespace MES.Core.Helpers;

/// <summary>
/// 操作人候选匹配：员工「生产工段」+「工序组（GroupName 存工序英文 Key 逗号串）」双条件过滤。
/// 与后端 BuildCommaListContains（逗号边界任一元素精确匹配，OrdinalIgnoreCase）语义一致，
/// 扫码端与 PC 手工录入端统一走此匹配，避免各页私有副本漂移。
/// </summary>
public static class OperatorMatchHelper
{
    /// <summary>员工 SectionName 逗号串任一元素匹配目标工段（员工未配工段不匹配）</summary>
    public static bool MatchesSection(EmployeeDto e, string? sectionName)
        => !string.IsNullOrWhiteSpace(e.SectionName)
            && e.SectionName.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Any(x => string.Equals(x.Trim(), sectionName, StringComparison.OrdinalIgnoreCase));

    /// <summary>员工工序组（GroupName 工序 Key 逗号串）空=全工序组通配；非空须任一命中目标工序 Key</summary>
    public static bool MatchesProcessGroup(EmployeeDto e, string? processName)
        => string.IsNullOrWhiteSpace(e.GroupName)
            || e.GroupName.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Any(x => string.Equals(x.Trim(), processName, StringComparison.OrdinalIgnoreCase));
}
