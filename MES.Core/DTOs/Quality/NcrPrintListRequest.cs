using MES.Core.DTOs.Shared;

namespace MES.Core.DTOs.Quality;

/// <summary>
/// 不合格报告列表打印请求（打印选中列表：按当前可见列渲染列表 PDF，Mode A 前端已准备数据）
/// </summary>
public class NcrPrintListRequest
{
    /// <summary>标题</summary>
    public string Title { get; set; } = "不合格报告列表";

    /// <summary>打印数据行（字典格式，枚举/日期/数值已解析为表格显示文本）</summary>
    public List<Dictionary<string, object>> Items { get; set; } = new();

    /// <summary>打印列定义（对应当前可见列）</summary>
    public List<PrintColumnDef> Columns { get; set; } = new();
}
