namespace MES.Data.Entities;

/// <summary>
/// 标准号列表
/// </summary>
public class StandardRegister : BaseEntity
{
    /// <summary>标准号</summary>
    public string StandardNo { get; set; } = string.Empty;

    /// <summary>标准名称</summary>
    public string StandardName { get; set; } = string.Empty;

    /// <summary>引用规范</summary>
    public string? RefSpecification { get; set; }

    /// <summary>标准级别（国标/行标/企标等）</summary>
    public string? StandardLevel { get; set; }

    /// <summary>制造方式（焊管/无缝/无缝+焊管）</summary>
    public string? ManufactureMethod { get; set; }

    /// <summary>钢类（奥氏体/双相/镍基合金等）</summary>
    public string? SteelType { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>子项目列表</summary>
    public ICollection<StandardRegisterItem> Items { get; set; } = new List<StandardRegisterItem>();
}
