namespace MES.Core.DTOs.Batch;

/// <summary>
/// 工艺流转卡列定义（BlockKey=区块标识, Key=字段名, Label=显示名, Visible=是否启用,
/// RowIndex=所属行（区块内局部 1 起，工序组恒 1）, ColumnIndex=区块内列顺序, ColumnWeight=列宽权重）
/// </summary>
public class ProcessCardColumnDef
{
    /// <summary>区块标识：BatchInfo / Quality / Warehouse / WorkOrder / ProcessGroup</summary>
    public string BlockKey { get; set; } = "";
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public bool Visible { get; set; } = true;

    /// <summary>所属行（区块内局部，1 起；工序组无行概念恒 1）</summary>
    public int RowIndex { get; set; } = 1;

    /// <summary>行内列顺序（区块内全局排序键，1 起）</summary>
    public int ColumnIndex { get; set; }

    /// <summary>列宽权重（相对比例，>0）</summary>
    public int ColumnWeight { get; set; } = 3;
}

/// <summary>
/// 工艺流转卡打印请求
/// </summary>
public class ProcessCardPrintRequest
{
    /// <summary>要打印的批次ID列表（为空则表示全部）</summary>
    public int[] Ids { get; set; } = Array.Empty<int>();

    /// <summary>列定义（仅 Visible=true 的列会被打印）</summary>
    public List<ProcessCardColumnDef> Columns { get; set; } = new();
}
