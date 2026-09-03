using MES.Core.DTOs.Payroll;

namespace MES.Core.Interfaces.Payroll;

/// <summary>津贴与处罚服务（月度金额录入表，宽表固定 9 列，每人每月一行）</summary>
public interface IPayrollAllowanceService
{
    /// <summary>读取某月津贴网格（IsActive 在册员工 ∪ 当月已有记录员工，按工号升序）</summary>
    Task<AllowanceMonthDto> GetMonthAsync(int year, int month);

    /// <summary>整月保存（每人每月一行 upsert；全空行=删除该员工当月行；空 Rows=清空整月），返回已保存（新增/更新）员工行数</summary>
    Task<int> SaveMonthAsync(int year, int month, IReadOnlyList<AllowanceRowInputDto> rows);
}
