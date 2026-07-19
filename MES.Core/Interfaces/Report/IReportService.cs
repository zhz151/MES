using MES.Core.DTOs.Report;
using MES.Core.DTOs.Shared;
namespace MES.Core.Interfaces.Report;

/// <summary>
/// 报表服务接口 — 跨上下文聚合查询，只读操作
/// </summary>
public interface IReportService
{
    /// <summary>
    /// 获取产量报表数据（日期范围聚合）
    /// </summary>
    Task<DailyProductionReportResponse> GetDailyProductionReportAsync(DateTime fromDate, DateTime toDate);

    /// <summary>
    /// 产量报表打印 — 生成 PDF
    /// </summary>
    Task<byte[]> PrintDailyProductionReportAsync(DateTime fromDate, DateTime toDate, List<PrintColumnDef>? columns);
}
