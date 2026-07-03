namespace MES.Core.DTOs;

/// <summary>
/// 工单计划打印请求
/// </summary>
public class WorkOrderSchedulePrintRequest
{
    /// <summary>标题</summary>
    public string Title { get; set; } = "工单计划";

    /// <summary>打印数据行（字典格式，枚举字段已解析为中文显示文本）</summary>
    public List<Dictionary<string, object>> Items { get; set; } = new();

    /// <summary>打印列定义</summary>
    public List<PrintColumnDef> Columns { get; set; } = new();
}
