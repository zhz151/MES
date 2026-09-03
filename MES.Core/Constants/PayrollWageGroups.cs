using MES.Core.Enums;

namespace MES.Core.Constants;

/// <summary>
/// 每日工资表的分组（决定页面路由、接口 group 参数与该组包含的薪酬归口）。
/// 仅两种：非计件工资表（计小时 + 计日期）与 个人计件工资表（个人计件）。
/// 集体计件 / 靠工计件 不按日出每日工资，业务另行规划，不进入这两张表。
/// </summary>
public enum PayrollWageGroup
{
    /// <summary>非计件工资（Hourly 计小时 + Daily 计日期；Fixed 固定月薪不按日出，不进表）</summary>
    NonPiece,

    /// <summary>个人计件工资（PieceIndividual 个人计件）</summary>
    IndividualPiece
}

/// <summary>每日工资表分组常量（Key 与页面路由段、接口 group 参数一致）</summary>
public static class PayrollWageGroups
{
    /// <summary>非计件工资 路由段 / 接口 group 参数</summary>
    public const string NonPieceKey = "non-piece";

    /// <summary>个人计件工资 路由段 / 接口 group 参数</summary>
    public const string IndividualPieceKey = "piece";

    /// <summary>分组 → 路由段（URL Key）</summary>
    public static string GetKey(this PayrollWageGroup group)
        => group == PayrollWageGroup.IndividualPiece ? IndividualPieceKey : NonPieceKey;

    /// <summary>路由段 / group 参数 → 分组；空或未知默认 非计件</summary>
    public static PayrollWageGroup ParseKey(string? key)
        => string.Equals(key, IndividualPieceKey, StringComparison.OrdinalIgnoreCase)
            ? PayrollWageGroup.IndividualPiece
            : PayrollWageGroup.NonPiece;

    /// <summary>该组归口的薪酬模式集合（决定该组表显示哪些员工 + 引擎对哪些员工自动带出）</summary>
    public static IReadOnlyList<SalaryMode> SalaryModes(this PayrollWageGroup group)
        => group == PayrollWageGroup.NonPiece
            ? new[] { SalaryMode.Hourly, SalaryMode.Daily }
            : new[] { SalaryMode.PieceIndividual };

    /// <summary>是否某薪酬归口 Key（枚举英文名）属于该组（OrdinalIgnoreCase 对字符串 Key 兼容）</summary>
    public static bool ContainsModeKey(this PayrollWageGroup group, string? modeKey)
    {
        if (string.IsNullOrEmpty(modeKey)) return false;
        foreach (var m in group.SalaryModes())
            if (string.Equals(m.ToString(), modeKey, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>分组显示标题（前端页面标题）</summary>
    public static string GetTitle(this PayrollWageGroup group)
        => group == PayrollWageGroup.IndividualPiece ? "个人计件工资" : "非计件工资";
}
