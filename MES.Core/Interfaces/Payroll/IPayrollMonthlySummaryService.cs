using MES.Core.DTOs.Payroll;

namespace MES.Core.Interfaces.Payroll;

/// <summary>月工资汇总服务（员工某结算月完整应发/实发，由各子页已保存金额 + 考勤派生）</summary>
public interface IPayrollMonthlySummaryService
{
    /// <summary>
    /// 读取某月工资汇总（实时重算展示：IsActive 在册员工 ∪ 当月任一来源有行，按工号升序）。
    /// HasSaved = 本月是否已生成保存快照（打印须先保存本月，打印读快照）。
    /// </summary>
    Task<MonthlySummaryMonthDto> GetMonthAsync(int year, int month, string? keyword = null);

    /// <summary>整月保存：按派生口径重算整月并替换快照（每人每月一行），返回行数</summary>
    Task<int> SaveMonthAsync(int year, int month);

    /// <summary>全部打印：一张 A4 横向整表（读已保存快照；未保存抛业务异常提示先保存）</summary>
    Task<byte[]> PrintAllAsync(int year, int month);

    /// <summary>个人打印：每人一条带表头的两行带，便于裁剪发放（读已保存快照；未保存抛业务异常提示先保存）</summary>
    Task<byte[]> PrintPersonalAsync(int year, int month);
}
