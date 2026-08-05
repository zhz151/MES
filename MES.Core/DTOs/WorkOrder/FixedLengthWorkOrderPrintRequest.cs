using MES.Core.DTOs.Shared;
namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 定尺工单定尺数据打印请求（Mode B ⓪：前端已准备数据）
/// </summary>
public class FixedLengthWorkOrderPrintRequest
{
    /// <summary>标题</summary>
    public string Title { get; set; } = "定尺工单定尺数据";

    /// <summary>打印数据行（字典格式，枚举字段已解析为中文显示文本）</summary>
    public List<Dictionary<string, object>> Items { get; set; } = new();

    /// <summary>打印列定义</summary>
    public List<PrintColumnDef> Columns { get; set; } = new();
}
