namespace MES.Core.DTOs.Batch;

/// <summary>
/// 工艺流转卡列定义（BlockKey=区块标识, Key=字段名, Label=显示名, Visible=是否打印）
/// </summary>
public class ProcessCardColumnDef
{
    /// <summary>区块标识：BatchInfo / Quality / Warehouse / WorkOrder / ProcessGroup</summary>
    public string BlockKey { get; set; } = "";
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public bool Visible { get; set; } = true;
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
