namespace MES.Core.DTOs;

/// <summary>
/// 成检计划打印请求
/// </summary>
public class FinalInspectionPlanPrintRequest
{
    /// <summary>标题</summary>
    public string Title { get; set; } = "成检计划";

    /// <summary>打印数据行</summary>
    public List<Dictionary<string, object>> Items { get; set; } = new();

    /// <summary>打印列定义</summary>
    public List<PrintColumnDef> Columns { get; set; } = new();
}
