namespace MES.Data.Entities;

/// <summary>
/// 仓库档案
/// </summary>
public class Warehouse : BaseEntity
{
    /// <summary>
    /// 仓库代码，唯一
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>
    /// 仓库名称
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// 显示顺序
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}
