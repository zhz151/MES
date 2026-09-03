using MES.Core.Constants;
using MES.Core.DTOs.Payroll;

namespace MES.Core.Interfaces.Payroll;

/// <summary>
/// 每日工资服务 — 非计件工资 / 个人计件工资 两表的按月读取与整月保存。
/// 单元格=每日工资额，由计件引擎自动带出草稿 + 人工可改，保存落库为按归口快照。
/// </summary>
public interface IPayrollDailyWageService
{
    /// <summary>
    /// 按月获取该组的员工集合（归口属该组的启用员工 ∪ 当月已有记录且快照归口属该组的员工）
    /// 与每员工逐日已保存值 + 引擎自动带出草稿。
    /// </summary>
    Task<DailyWageMonthDto> GetMonthAsync(int year, int month, PayrollWageGroup group, string? keyword);

    /// <summary>整月保存：该组员工集合 upsert（金额 &gt;0 存/更新、空或 0 删除），返回变更记录数</summary>
    Task<int> SaveMonthAsync(SaveDailyWageDto request);
}
