using MES.Core.DTOs.Shared;
namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 段落流转分析打印请求
/// </summary>
public class SectionParagraphFlowAnalysisPrintRequest
{
    public string Title { get; set; } = "段落流转分析";
    public List<Dictionary<string, object>> Items { get; set; } = new();
    public List<PrintColumnDef> Columns { get; set; } = new();
}
