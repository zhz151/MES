using MES.Core.DTOs.Shared;
namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 工段待产量打印请求
/// </summary>
public class SectionProductionStatusPrintRequest
{
    public string Title { get; set; } = "工段待产量";
    public List<Dictionary<string, object>> Items { get; set; } = new();
    public List<PrintColumnDef> Columns { get; set; } = new();
}
