using MES.Core.DTOs.Payroll;

namespace MES.Core.Interfaces.Payroll;

/// <summary>
/// 杂辅工记录服务 — 登记员工每天做的杂项辅助工作（台账流水，行=一条任务登记）。
/// 金额为手工录入源头（保留小数，不做整元取整）；允许同一员工同一天多条，无唯一约束。
/// 本表仅登记，暂不参与员工完整月工资汇总（完整工资 = 各类工资 + 杂辅，属后续模块）。
/// </summary>
public interface IPayrollMiscWorkService
{
    /// <summary>按月读取杂辅台账：记录（补员工工号/姓名）+ 整月条数/总小时/总金额</summary>
    Task<MiscWorkMonthDto> GetMonthAsync(int year, int month);

    /// <summary>保存一条记录（Id=0 新增 / &gt;0 编辑更新，编辑不改员工），返回记录 Id</summary>
    Task<int> SaveRecordAsync(MiscWorkRecordInputDto input);

    /// <summary>删除一条记录</summary>
    Task DeleteRecordAsync(int id);
}
