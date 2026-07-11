namespace MES.Core.DTOs.Report;

/// <summary>
/// 产量报表打印请求
/// </summary>
public class DailyProductionReportPrintRequest
{
    public string FromDate { get; set; } = "";
    public string ToDate { get; set; } = "";
    public List<MES.Core.DTOs.Shared.PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 产量报表响应 — 包含所有列定义和数据行
/// </summary>
public class DailyProductionReportResponse
{
    /// <summary>
    /// 所有列名称（按展示顺序），包含固定列（投料荒管/过程检验/成品入库）+ 动态工段列
    /// </summary>
    public List<string> SectionColumns { get; set; } = new();

    /// <summary>
    /// 数据行（每个日期一行）
    /// </summary>
    public List<DailyProductionReportRow> Rows { get; set; } = new();
}

/// <summary>
/// 产量报表数据行
/// </summary>
public class DailyProductionReportRow
{
    /// <summary>
    /// 日期
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// 显示用日期标签，如 "07-01(周一)"
    /// </summary>
    public string DisplayDate { get; set; } = "";

    /// <summary>
    /// 各列重量值（Key=SectionColumns 中的列名，Value=总重量kg）
    /// </summary>
    public Dictionary<string, decimal> Values { get; set; } = new();
}
