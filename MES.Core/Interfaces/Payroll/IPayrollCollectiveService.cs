using MES.Core.DTOs.Payroll;

namespace MES.Core.Interfaces.Payroll;

/// <summary>
/// 集体计件月结服务 — 集体计件工资按月结算（集体=岗位，月终按 出勤×分值 权重分配）与月度评分的读写。
/// 月结按「岗位池 × w/Σw」分配：池来自当月 5 类产量源中该岗位集体成员写名行的成员份额；
/// 快照落库后历史月不随改价/改产漂移（历史月按结算时现行单价估算草稿）。
/// </summary>
public interface IPayrollCollectiveService
{
    /// <summary>
    /// 按月获取各岗位集体的结算卡片：成员集合（当前在册集体成员 ∪ 当月已有月结快照员工）、
    /// 岗位计件池、各成员出勤/分值/权重与引擎分配草稿、已保存金额。
    /// </summary>
    Task<CollectiveMonthDto> GetMonthAsync(int year, int month);

    /// <summary>整月保存：成员集合 upsert（金额 &gt;0 存/更新、空或 0 删除），返回变更记录数</summary>
    Task<int> SaveMonthAsync(SaveCollectiveMonthDto request);

    /// <summary>按月读取评分员工集（当前在册集体成员 ∪ 当月已有评分员工）与各自分值</summary>
    Task<CollectiveScoresDto> GetScoresAsync(int year, int month);

    /// <summary>整月评分保存：upsert（1–10 校验，null 删除），返回变更记录数</summary>
    Task<int> SaveScoresAsync(SaveCollectiveScoresDto request);
}
