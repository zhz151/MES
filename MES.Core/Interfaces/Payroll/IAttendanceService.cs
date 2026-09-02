using MES.Core.DTOs.Payroll;

namespace MES.Core.Interfaces.Payroll;

/// <summary>
/// 考勤服务 — 考勤表（工资结算上下文出勤基础数据）按月的读取与整月保存
/// </summary>
public interface IAttendanceService
{
    /// <summary>按月获取启用员工 + 当月出勤数据（月视图网格）；keyword 过滤工号/姓名</summary>
    Task<AttendanceMonthDto> GetMonthAsync(int year, int month, string? keyword);

    /// <summary>整月保存：有值 upsert，清空删除（同员工同日期唯一），返回变更记录数</summary>
    Task<int> SaveMonthAsync(SaveAttendanceDto request);
}
