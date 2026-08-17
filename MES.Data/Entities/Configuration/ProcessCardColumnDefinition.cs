namespace MES.Data.Entities.Configuration;

/// <summary>
/// 工艺卡打印列布局配置表：每个打印字段的显示配置（是否启用/所属行/列顺序/列宽权重），
/// 数据库全局共享（仿 EnumDisplayDefinition 模式）。锚点 = BlockKey + FieldKey。
/// </summary>
public class ProcessCardColumnDefinition : BaseEntity
{
    /// <summary>区块：BatchInfo / Quality / Warehouse / WorkOrder / ProcessGroup</summary>
    public string BlockKey { get; set; } = string.Empty;

    /// <summary>字段 Key 或工段 SectionKey</summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>显示名（可改中文）</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>是否启用</summary>
    public bool Visible { get; set; } = true;

    /// <summary>所属行（区块内局部，1 起；工序组恒 1）</summary>
    public int RowIndex { get; set; } = 1;

    /// <summary>行内列顺序（区块内全局排序键，1 起）</summary>
    public int ColumnIndex { get; set; }

    /// <summary>列宽权重（相对比例，>0）</summary>
    public int ColumnWeight { get; set; } = 3;
}
