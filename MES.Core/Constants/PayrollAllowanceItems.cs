namespace MES.Core.Constants;

/// <summary>
/// 「津贴与处罚」9 个金额项目列元数据（Key=实体字段名，Title=中文，顺序与 Excel《津贴与处罚.xlsx》一致）。
/// 宽表固定 9 列，列语义强固定；加列需迁移（用户已接受该取舍）。纯前端消费（津贴月结页网格列渲染）。
/// </summary>
public static class PayrollAllowanceItems
{
    public sealed record Item(string Key, string Title);

    public static readonly Item[] All =
    {
        new("FullAttendanceBonus", "满勤奖"),
        new("SeniorityBonus", "工龄奖"),
        new("NightShiftAllowance", "夜班津贴"),
        new("PositionAllowance", "岗位补贴"),
        new("HighTempAllowance", "高温费"),
        new("InjurySubsidy", "工伤补贴"),
        new("LeadBonus", "带班费"),
        new("Penalty", "处罚"),
        new("SocialSecurity", "代缴社保"),
    };
}
