namespace MES.Data.Entities;

public class SupplierProfile : BaseEntity
{
    /// <summary>
    /// 供应商名称
    /// </summary>
    public string SupplierName { get; set; } = null!;

    /// <summary>
    /// 联系人
    /// </summary>
    public string? ContactPerson { get; set; }

    /// <summary>
    /// 联系电话
    /// </summary>
    public string? ContactPhone { get; set; }

    /// <summary>
    /// 地址
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}
