using MudBlazor;

namespace MES.Blazor.Shared;

/// <summary>
/// 「全量重算（清历史）」模式 2 的双重确认骨架（2026-09-03）。
/// 破坏性重算的确认流程在 每日工资两表（MonthlyWages）/ 集体计件月结（CollectiveMonthly）共用；
/// 未来「靠工计件」做同款重算时直接复用，保证防误操作口径一致。
/// 语义：先 Info 说明影响范围并提示「保留历史请用引擎重算」，再 Error 终确认；任一步取消返回 false。
/// 文案统一用「人员」通称（日结=员工、集体=成员），页标题参数化区分。
/// </summary>
public static class PayrollFullRecalcDialogs
{
    /// <summary>
    /// 双重确认：Info 说明 → Error 终确认。两重都确认返回 true；任一步取消返回 false（调用方直接中止）。
    /// </summary>
    /// <param name="dialogService">页面注入的 IDialogService。</param>
    /// <param name="title">页面/组标题，如「非计件工资」「个人计件工资」「集体计件月结」。</param>
    /// <param name="year">结算年份。</param>
    /// <param name="month">结算月份。</param>
    /// <param name="covered">在册（引擎覆盖）人数。</param>
    /// <param name="history">已转出/离职的历史归口（历史快照）人数。</param>
    public static async Task<bool> ConfirmFullRecalcAsync(
        IDialogService dialogService,
        string title, int year, int month,
        int covered, int history)
    {
        // 第一重：说明影响范围与替代方案（Info）
        var info = dialogService.Show<ConfirmDialog>("全量重算（清历史）说明", new DialogParameters
        {
            ["ContentText"] = $"将把 {year}年{month}月「{title}」整月全部人员按引擎草稿覆盖：\n"
                              + $"· 在册 {covered} 人 → 引擎结果；\n"
                              + (history > 0
                                  ? $"· 已转出/离职的历史归口 {history} 人 → 引擎无草稿，当月记录将清空；点「保存本月」后其当月已保存旧记录将被删除、不可找回。\n"
                                  : "· 当前无历史归口人员，仅把在册人员覆盖为草稿。\n")
                              + "只想重算在册人员并保留历史已保存金额？请取消，改用「引擎重算」。",
            ["ConfirmText"] = "继续",
            ["Color"] = Color.Info
        });
        if ((await info.Result).Canceled) return false;

        // 第二重：终确认（Error）
        var final = dialogService.Show<ConfirmDialog>("最后确认", new DialogParameters
        {
            ["ContentText"] = history > 0
                ? $"再次确认：全量重算后将清空 {history} 名已不在当月归口范围人员的历史已保存金额，保存后其当月记录将被删除且不可恢复。确定继续？"
                : $"再次确认：将把 {covered} 名在册人员整月覆盖为引擎草稿（保存后生效）。确定继续？",
            ["ConfirmText"] = "确定全量重算",
            ["Color"] = Color.Error
        });
        return !(await final.Result).Canceled;
    }
}
