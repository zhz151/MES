using MES.Core.DTOs.Payroll;

namespace MES.Core.Interfaces.Payroll;

/// <summary>
/// 靠工计件月结服务 — 靠工计件工资按月结算的读写。
/// 靠工工资（月）= 靠工岗位当月平均小时工资 × 本人当月实出勤小时 × 靠工系数；
/// 平均小时工资 = 选中岗位（个人计件 + 集体计件并集，不分档）当月计件总工资 ÷ 同批岗位计件人员总出勤小时
/// （分子分母各自合并成一个总平均，不逐岗重复计酬）。快照落库后历史月不随改产/改薪漂移。
/// </summary>
public interface IPayrollAttendanceService
{
    /// <summary>
    /// 按月获取靠工员工结算行：员工集合（当前在册靠工计件员工 ∪ 当月已有月结快照员工）、
    /// 各人靠工岗位/出勤/系数、选中岗位合并平均小时工资与引擎月得草稿、已保存金额。
    /// </summary>
    Task<AttendanceWageMonthDto> GetMonthAsync(int year, int month);

    /// <summary>整月保存：员工集合 upsert（金额 &gt;0 存/更新、空或 0 删除），返回变更记录数</summary>
    Task<int> SaveMonthAsync(SaveAttendanceWageDto request);
}
