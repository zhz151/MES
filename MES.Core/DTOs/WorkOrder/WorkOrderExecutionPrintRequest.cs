using MES.Core.DTOs.Shared;
namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 工单执行状况打印请求（打印选中）
/// </summary>
public class WorkOrderExecutionPrintRequest
{
    /// <summary>标题</summary>
    public string Title { get; set; } = "工单执行状况";

    /// <summary>打印数据行（字典格式，枚举字段已解析为中文显示文本）</summary>
    public List<Dictionary<string, object>> Items { get; set; } = new();

    /// <summary>打印列定义</summary>
    public List<PrintColumnDef> Columns { get; set; } = new();
}

