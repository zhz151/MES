namespace MES.Core.DTOs;

/// <summary>
/// 工段流转分析打印请求
/// </summary>
public class SectionFlowAnalysisPrintRequest
{
    public string Title { get; set; } = "工段流转分析";
    public List<Dictionary<string, object>> Items { get; set; } = new();
    public List<PrintColumnDef> Columns { get; set; } = new();
}
